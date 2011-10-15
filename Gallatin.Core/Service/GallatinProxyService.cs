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
                if (context.ServerConnection != null)
                {
                    context.ServerConnection.BeginClose(
                        (success, facade) => context.ClientConnection.BeginClose(
                            (success2, facade2) =>
                            {
                                if (success2)
                                {
                                    Log.Info("Successfully disconnected session");

                                    // Possible leak here if we cannot close the socket. I don't see that being a problem.
                                    _connections.Put(context);
                                }
                            }));
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception closing socket", ex);
            }
        }

        private void HandleDataSentToClient( bool success, INetworkFacade clientConnection )
        {
            try
            {
                if (success)
                {
                    ConnectionContext connectionContext = clientConnection.Context as ConnectionContext;

                    Log.Verbose("{0} Handling data sent to client", connectionContext.Id);

                    IHttpResponseMessage response;
                    if (connectionContext.ServerMessageParser.TryGetCompleteResponseMessage(out response))
                    {
                        Log.Verbose(() => string.Format("{0}\r\n{1}", connectionContext.Id, response));

                        // Evaluate connection persistence
                        // According to the spec, HTTP 1.1 should remain persistent until told to close. Always close HTTP 1.0 connections.
                        if (response.Version == "1.1")
                        {
                            string connection = response["connection"];
                            if ((connection != null && connection.Equals("close", StringComparison.InvariantCultureIgnoreCase))
                                 )
                            {
                                Log.Verbose("{0} Ending HTTP/1.1 connection", connectionContext.Id);
                                EndSession(connectionContext);
                            }
                            else
                            {
                                Log.Verbose("{0} Maintaining persistent connection", connectionContext.Id);
                                ResetForNewMessageFromClient(connectionContext);
                            }
                        }
                        else
                        {
                            Log.Verbose("{0} Ending connection", connectionContext.Id);
                            EndSession(connectionContext);
                        }
                    }
                    else
                    {
                        Log.Error("Server did not return valid HTTP response");
                    }

                }
                else
                {
                    Log.Error("Unable to send data to client");
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception sending data to client", ex);
            }

        }

        private void ResetForNewMessageFromClient( ConnectionContext connectionContext )
        {
            connectionContext.ClientMessageParser.Reset();
            connectionContext.ClientConnection.BeginReceive(HandleDataFromClient);
        }

        private void HandleDataFromServer( bool success, byte[] data, INetworkFacade serverConnection )
        {
            try
            {
                if (success)
                {
                    ConnectionContext connectionContext = serverConnection.Context as ConnectionContext;

                    Log.Verbose("{0} Handling data from server", connectionContext.Id);

                    IHttpMessage message = connectionContext.ServerMessageParser.AppendData(data);
                    if (message != null)
                    {
                        Log.Verbose("{0} All data received from server. Sending to client.", connectionContext.Id);

                        // Received all data. Send message to client.
                        connectionContext.ClientConnection.BeginSend(message.CreateHttpMessage(), HandleDataSentToClient);
                    }
                    else
                    {
                        // Evaluate streaming data from server. If streaming, switch modes (firehose)
                        IHttpMessage header;
                        if (connectionContext.ServerMessageParser.TryGetHeader(out header))
                        {
                            string contentType = header["content-type"];
                            if (contentType != null 
                                && !(contentType.StartsWith("text/")))
                            {
                                Log.Verbose("{0} Switching to streaming mode", connectionContext.Id);

                                // Stream all binary content. Let the client interpret chunked data. The client
                                // will call back when all data is read or will possibly close the connect, in 
                                // which case we'll receive an error in the callback.
                                ResetForNewMessageFromClient(connectionContext);

                                connectionContext.ServerStream = new ServerStream2(connectionContext.ClientConnection, connectionContext.ServerConnection);

                                // Send everything we've read so far and then start streaming.
                                connectionContext.ServerStream.StartStreaming(connectionContext.ServerMessageParser.AllData);
                            }
                        }

                        if (connectionContext.ServerStream == null)
                        {
                            Log.Verbose("{0} Requesting more data from server", connectionContext.Id);

                            connectionContext.ServerConnection.BeginReceive(HandleDataFromServer);
                        }

                    }
                }
                else
                {
                    Log.Error("Unable to receive data from server");
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception reading data from server", ex);
            }

        }

        private void SendToServerComplete(bool success, INetworkFacade serverConnection)
        {
            try
            {
                if (success)
                {
                    serverConnection.BeginReceive(HandleDataFromServer);
                }
                else
                {
                    Log.Error("Unable to send request to server");
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
                            serverConnection.BeginSend(request.CreateHttpMessage(), SendToServerComplete);
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
                    sessionContext.ServerMessageParser.Reset();

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

                        sessionContext.ServerConnection.BeginSend(requestMessage.CreateHttpMessage(), SendToServerComplete);
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
            try
            {
                if (success)
                {
                    ConnectionContext context = clientConnection.Context as ConnectionContext;

                    Log.Verbose("{0} Processing data from client", context.Id);

                    IHttpMessage message = context.ClientMessageParser.AppendData(data);
                    if (message != null)
                    {
                        Log.Verbose(() => string.Format("{0} {1}", context.Id, message));

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
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("Unhandled exception reading data from client", ex);
            }

        }

        private void ClientConnected(INetworkFacade clientConnection)
        {
            Contract.Requires(clientConnection!=null);

            Log.Verbose("New client connection");

            try
            {
                ConnectionContext connectionContext = _connections.Get();
                connectionContext.ClientConnection = clientConnection;
                clientConnection.Context = connectionContext;

                clientConnection.BeginReceive(HandleDataFromClient);
            }
            catch
            {
                Log.Error("Unable to accept new client connection. The maximum number of concurrent clients has been reached");
                clientConnection.BeginClose( ( s, f ) => Log.Warning( "Closed client connection. Too many connections." ) );
            }
            
        }

        public void Start(int port)
        {
            Log.Verbose("Starting proxy server");

            _facadeFactory.Listen( _settings.NetworkAddressBindingOrdinal, port, ClientConnected );
        }

        public void Stop()
        {
            
        }
    }
}
