using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    public class GallatinProxyService : IProxyService
    {
        private INetworkFacadeFactory _facadeFactory;
        private ICoreSettings _settings;
        private Pool<ConnectionContext> _connections; 
        
        public GallatinProxyService(INetworkFacadeFactory facadeFactory, ICoreSettings settings)
        {
            Contract.Requires(facadeFactory!=null);
            Contract.Requires(settings!=null);
            Contract.Ensures( _settings != null );
            Contract.Ensures(_facadeFactory != null);

            _facadeFactory = facadeFactory;
            _settings = settings;
            _connections = new Pool<ConnectionContext>();
            _connections.Init(_settings.MaxNumberClients);
        }

        private void EndSession( ConnectionContext context )
        {
            Log.Info("{0} Closing proxy client session", context.Id);

            try
            {
                lock(context)
                {
                    if (context.ServerConnection != null)
                    {
                        context.ServerConnection.BeginClose((s, f) => Log.Verbose("{0} Server connection closed", context.Id));

                        context.ClientConnection.BeginClose((s, f) => Log.Verbose("{0} Client connection closed", context.Id));

                        _connections.Put(context);
                    }
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception closing socket", ex);
            }
        }

        private void HandleDataSentToClient( bool success, INetworkFacade clientConnection )
        {
            Contract.Requires(clientConnection!=null);

            try
            {
                ConnectionContext connectionContext = clientConnection.Context as ConnectionContext;

                if (success)
                {

                    Log.Verbose("{0} Handling data sent to client", connectionContext.Id);

                    connectionContext.ServerConnection.BeginReceive(HandleDataFromServer);
                }
                else
                {
                    Log.Error("Unable to send data to client");
                    EndSession(connectionContext);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception sending data to client", ex);
            }

        }

        private void HandleDataFromServer( bool success, byte[] data, INetworkFacade serverConnection )
        {
            Contract.Requires(serverConnection!=null);

            try
            {
                ConnectionContext connectionContext = serverConnection.Context as ConnectionContext;

                if (success)
                {
                    Log.Verbose("{0} Handling data from server", connectionContext.Id);

                    connectionContext.ClientConnection.BeginSend( data, HandleDataSentToClient );
                }
                else
                {
                    Log.Error("Unable to receive data from server");

                    // TODO: evaluate later
                    //EndSession(connectionContext);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception reading data from server", ex);
            }

        }

        private void HandleDataSentToServer(bool success, INetworkFacade serverConnection)
        {
            Contract.Requires(serverConnection!= null);

            try
            {
                var context = serverConnection.Context as ConnectionContext;

                if (success)
                {
                    Log.Verbose("{0} Data sent to server. Resetting client message parser. Data to be streamed to client.", context.Id);
                    context.ClientMessageParser.Reset();

                    context.ClientConnection.BeginReceive(HandleDataFromClient);
                }
                else
                {
                    Log.Error("Unable to send request to server");
                    EndSession(context);
                }
            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception sending data to server", ex);
            }

        }

        private void HandleConnectToServer(bool success, INetworkFacade serverConnection, ConnectionContext state)
        {
            Contract.Requires(serverConnection!= null);
            Contract.Requires(state != null);
            Contract.Requires(state.Host != null);
            Contract.Requires(state.Port > 0);

            try
            {
                if (success)
                {
                    Log.Verbose("{0} Connected to server", state.Id);

                    state.ServerConnection = serverConnection;
                    serverConnection.Context = state;

                    IHttpRequestMessage request;
                    if (state.ClientMessageParser.TryGetCompleteRequestMessage(out request))
                    {
                        if (request.IsSsl)
                        {
                            SslTunnel sslTunnel = new SslTunnel(
                                state.ClientConnection, state.ServerConnection, request.Version);
                            sslTunnel.EstablishTunnel();
                        }
                        else
                        {
                            serverConnection.BeginSend(request.CreateHttpMessage(), HandleDataSentToServer);
                            serverConnection.BeginReceive(HandleDataFromServer);
                        }
                    }
                    else
                    {
                        Log.Error("Connected to server and attempted to send request but unable to create request from HTTP data");
                    }
                }
                else
                {
                    Log.Error("Unable to connect to remote host {0}  {1}", state.Host, state.Port);
                    EndSession(state);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception connecting to server", ex);
            }
        }

        private void SendMessageToServer(ConnectionContext sessionContext)
        {
            Contract.Requires(sessionContext != null);
            Contract.Requires(sessionContext.ClientConnection != null);

            try
            {
                IHttpRequestMessage requestMessage;
                if (sessionContext.ClientMessageParser.TryGetCompleteRequestMessage(out requestMessage))
                {
                    // If not connected, or if the host/port changes, connect to server
                    if (sessionContext.ServerConnection == null
                        || sessionContext.Host != requestMessage.Host
                        || sessionContext.Port != requestMessage.Port)
                    {
                        ManualResetEvent waitForServerDisconnectEvent = new ManualResetEvent(true);

                        sessionContext.Host = requestMessage.Host;
                        sessionContext.Port = requestMessage.Port;

                        if (sessionContext.ServerConnection != null)
                        {
                            Log.Verbose("{0} Resetting existing connection", sessionContext.Id);

                            waitForServerDisconnectEvent.Reset();

                            sessionContext.ServerConnection.BeginClose
                                ((success, facade) =>
                                {
                                    if (!success)
                                    {
                                        Log.Error("An error occurred while disconnecting from remote host");
                                    }
                                    waitForServerDisconnectEvent.Set();
                                });
                        }

                        if (waitForServerDisconnectEvent.WaitOne(10000))
                        {
                            Log.Verbose("{0} Connecting to {1}:{2}", sessionContext.Id, sessionContext.Host, sessionContext.Port);

                            _facadeFactory.BeginConnect(
                                sessionContext.Host,
                                sessionContext.Port,
                                HandleConnectToServer,
                                sessionContext);
                        }
                        else
                        {
                            Log.Error("Unable to re-establish new server connection. Closing client session.");
                        }
                    }
                    else
                    {
                        Contract.Assert(sessionContext.ServerConnection != null);
                        Contract.Assert(sessionContext.ServerConnection.Context != null);

                        Log.Verbose("{0} Sending request to server using open connection", sessionContext.Id);

                        sessionContext.ServerConnection.BeginSend(requestMessage.CreateHttpMessage(), HandleDataSentToServer);
                    }
                }
                else
                {
                    Log.Error("HTTP request expected from client. Invalid HTTP request.");
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception sending data to server",ex);
            }

        }

        private void HandleDataFromClient( bool success, byte[] data, INetworkFacade clientConnection )
        {
            Contract.Requires(clientConnection!=null);

            try
            {
                ConnectionContext context = clientConnection.Context as ConnectionContext;

                if (success)
                {
                    Log.Verbose( "{0} Processing data from client", context.Id );

                    IHttpMessage message = context.ClientMessageParser.AppendData(data);
                    if (message != null)
                    {
                        Log.Verbose(() => string.Format("{0} Read full message from client\r\n{1}", context.Id, message));

                        // Read full message. Send to server.
                        SendMessageToServer(context);
                    }
                    else
                    {
                        // Not enough data to complete the message. Get more.
                        Log.Verbose("{0} Requesting additional data from client", context.Id);
                        clientConnection.BeginReceive(HandleDataFromClient);
                    }
                }
                else
                {
                    Log.Info("{0} Failed to receive data from client. Client may have disconnected.");
                    EndSession(context);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception reading data from client", ex);
            }

        }

        private void HandleClientConnected(INetworkFacade clientConnection)
        {
            Contract.Requires(clientConnection!=null);

            try
            {
                ConnectionContext connectionContext = _connections.Get();
                connectionContext.ClientConnection = clientConnection;
                clientConnection.Context = connectionContext;

                Log.Verbose("{0} New client connection", connectionContext.Id);

                clientConnection.BeginReceive(HandleDataFromClient);
            }
            catch
            {
                Log.Error("Unable to accept new client connection. The maximum number of concurrent clients has been reached");
                clientConnection.BeginClose( ( s, f ) => Log.Warning( "Closed client connection. Too many connections." ) );
            }
            
        }

        private ManualResetEvent _isActiveEvent;

        private void WorkerMethod()
        {
            Log.Info("Starting worker thread in proxy server");

            while(!_isActiveEvent.WaitOne(30000))
            {
                Log.Verbose("Running worker thread");

                try
                {
                    DateTime cutoffTime = DateTime.Now.AddMinutes(-5);

                    foreach (var connectionContext in _connections.OutlierCollection.ToArray())
                    {
                        if ( (connectionContext.ServerConnection == null || connectionContext.ServerConnection.LastActivityTime > cutoffTime) &&
                            connectionContext.ClientConnection.LastActivityTime > cutoffTime)
                        {
                            Log.Warning("{0} Forcing close on hung session", connectionContext.Id);
                            EndSession(connectionContext);
                        }
                    }
                }
                catch(Exception ex)
                {
                    Log.Exception("Exception in worker thread", ex);
                }

            }

            Log.Info("Ending worker thread in proxy server");
        }

        private object _mutex = new object();
        private Thread _worker;

        public void Start(int port)
        {
            Log.Verbose("Starting proxy server");

            lock(_mutex)
            {
                if(_isActiveEvent == null )
                {
                    _isActiveEvent = new ManualResetEvent(false);

                    _worker = new Thread(WorkerMethod);
                    _worker.IsBackground = true;
                    _worker.Name = "Gallatin proxy server worker";
                    // TODO:
                    //_worker.Start();

                    _facadeFactory.Listen(_settings.NetworkAddressBindingOrdinal, port, HandleClientConnected);
                }
                else
                {
                    Log.Warning("Ignoring duplicate request to start proxy server");
                }
            }
        }

        public void Stop()
        {
            lock(_mutex)
            {
                if(_isActiveEvent != null)
                {
                    _isActiveEvent.Set();

                    if(!_worker.Join( 1000 ))
                        Log.Error("Worker thread did not respond");

                    _isActiveEvent = null;

                    // TODO: stop factory from listening
                    //_facadeFactory = null;
                }
            }
            
        }
    }
}
