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
        private class ConnectionContext
        {
            public ConnectionContext()
            {
                Contract.Ensures(ClientMessageParser != null);
                Contract.Ensures(ClientMessageParser != null);

                ClientMessageParser = new HttpMessageParser();
                ServerMessageParser = new HttpMessageParser();
            }

            [ContractInvariantMethod]
            private void ObjectInvariant()
            {
                Contract.Invariant(ClientMessageParser != null);
                Contract.Invariant(ServerMessageParser != null);
            }

            public int Id
            {
                get
                {
                    return ClientConnection == null ? 0 : ClientConnection.GetHashCode();
                }
            }

            public string Host { get; set; }
            public int Port { get; set; }
            public bool IsSsl { get; set; }
            public IHttpMessageParser ClientMessageParser { get; set; }
            public IHttpMessageParser ServerMessageParser { get; set; }

            public INetworkFacade ClientConnection { get; set; }
            public INetworkFacade ServerConnection { get; set; }
        }

        private INetworkFacadeFactory _facadeFactory;
        private ICoreSettings _settings;

        public GallatinProxyService(INetworkFacadeFactory facadeFactory, ICoreSettings settings)
        {
            Contract.Requires(facadeFactory!=null);
            Contract.Requires(settings!=null);
            Contract.Ensures( _settings != null );
            Contract.Ensures(_facadeFactory != null);

            _facadeFactory = facadeFactory;
            _settings = settings;
        }

        private void EndSession( ConnectionContext context )
        {
            Log.Info("{0} Closing proxy client session", context.Id);

            if(context.ServerConnection != null)
            {
                context.ServerConnection.BeginClose( 
                    (success,facade) => context.ClientConnection.BeginClose(
                        (success2,facade2) =>
                        {
                            if(success2)
                            {
                                Log.Info("Successfully disconnected session");
                            }
                        }) );
            }
        }

        private void HandleDataSentToClient( bool success, INetworkFacade clientConnection )
        {
            if(success)
            {
                ConnectionContext connectionContext = clientConnection.Context as ConnectionContext;

                Log.Verbose("{0} Handling data sent to client", connectionContext.Id);

                IHttpResponseMessage response;
                if( connectionContext.ServerMessageParser.TryGetCompleteResponseMessage( out response ) )
                {
                    Log.Verbose( () => string.Format("{0}\r\n{1}", connectionContext.Id, response ));

                    // Evaluate connection persistence
                    // According to the spec, HTTP 1.1 should remain persistent until told to close. Always close HTTP 1.0 connections.
                    if(response.Version == "1.1" )
                    {
                        string connection = response["connection"];
                        if((connection != null && connection.Equals("close", StringComparison.InvariantCultureIgnoreCase)) 
                            || response.StatusCode != 200 )
                        {
                            Log.Verbose("{0} Ending HTTP/1.1 connection", connectionContext.Id);
                            EndSession(connectionContext);
                        }
                        else
                        {
                            Log.Verbose("{0} Maintaining persistent connection", connectionContext.Id);
                            connectionContext.ClientMessageParser = new HttpMessageParser();
                            clientConnection.BeginReceive(HandleDataFromClient);
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

        private void HandleDataFromServer( bool success, byte[] data, INetworkFacade serverConnection )
        {
            if(success)
            {
                ConnectionContext connectionContext = serverConnection.Context as ConnectionContext;

                Log.Verbose("{0} Handling data from server", connectionContext.Id);

                IHttpMessage message = connectionContext.ServerMessageParser.AppendData(data);
                if( message != null)
                {
                    Log.Verbose("{0} All data received from server. Sending to client.", connectionContext.Id);

                    // Received all data. Send message to client.
                    connectionContext.ClientConnection.BeginSend( message.CreateHttpMessage(), HandleDataSentToClient );
                }
                else
                {
                    // TODO: Streaming data from server?
                    Log.Verbose("{0} Requesting more data from server", connectionContext.Id);

                    connectionContext.ServerConnection.BeginReceive(HandleDataFromServer);
                }
            }
            else
            {
                Log.Error("Unable to receive data from server");
            }
        }

        private void SendToServerComplete(bool success, INetworkFacade serverConnection)
        {
            if(success)
            {
                serverConnection.BeginReceive( HandleDataFromServer );
            }
            else
            {
                Log.Error("Unable to send request to server");
            }
        }

        private void HandleConnectToServer(bool success, INetworkFacade serverConnection, ConnectionContext state)
        {
            Contract.Requires(serverConnection!= null);
            Contract.Requires(state != null);
            Contract.Requires(state.Host != null);
            Contract.Requires(state.Port > 0);

            if(success)
            {
                Log.Verbose("{0} Connected to server", state.Id);

                state.ServerConnection = serverConnection;
                serverConnection.Context = state;

                IHttpRequestMessage request;
                if(state.ClientMessageParser.TryGetCompleteRequestMessage(out request))
                {
                    if(request.IsSsl)
                    {
                        SslTunnel sslTunnel = new SslTunnel( 
                            state.ClientConnection, state.ServerConnection, request.Version );
                        sslTunnel.EstablishTunnel();
                    }
                    else
                    {
                        serverConnection.BeginSend( request.CreateHttpMessage(), SendToServerComplete );
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

        private void SendMessageToServer(ConnectionContext sessionContext)
        {
            Contract.Requires(sessionContext != null);
            Contract.Requires(sessionContext.ClientConnection != null);

            IHttpRequestMessage requestMessage;
            if (sessionContext.ClientMessageParser.TryGetCompleteRequestMessage(out requestMessage))
            {
                sessionContext.ServerMessageParser = new HttpMessageParser();

                // If not connected, or if the host/port changes, connect to server
                if (sessionContext.ServerConnection == null 
                    || sessionContext.Host != requestMessage.Host 
                    || sessionContext.Port != requestMessage.Port)
                {
                    ManualResetEvent waitForServerDisconnectEvent = new ManualResetEvent(true);

                    sessionContext.Host = requestMessage.Host;
                    sessionContext.Port = requestMessage.Port; 
                    
                    if(sessionContext.ServerConnection != null)
                    {
                        Log.Verbose("{0} Resetting existing connection", sessionContext.Id);

                        waitForServerDisconnectEvent.Reset();

                        sessionContext.ServerConnection.BeginClose
                            ( ( success, facade ) =>
                              {
                                  if ( !success )
                                  {
                                      Log.Error( "An error occurred while disconnecting from remote host" );
                                  }
                                  waitForServerDisconnectEvent.Set();
                              } );
                    }

                    if(waitForServerDisconnectEvent.WaitOne(10000))
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

                    sessionContext.ServerConnection.BeginSend( requestMessage.CreateHttpMessage(), SendToServerComplete);
                }
            }
            else
            {
                Log.Error( "HTTP request expected from client. Invalid HTTP request." );
            }
        }

        private void HandleDataFromClient( bool success, byte[] data, INetworkFacade clientConnection )
        {
            if(success)
            {
                ConnectionContext context = clientConnection.Context as ConnectionContext;

                Log.Verbose("{0} Processing data from client", context.Id);

                IHttpMessage message = context.ClientMessageParser.AppendData(data);
                if(message!=null)
                {
                    Log.Verbose( () => string.Format("{0} {1}", context.Id, message) );

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
                Log.Error("{0} Failed to receive data from client");
            }
        }

        private void ClientConnected(INetworkFacade clientConnection)
        {
            Contract.Requires(clientConnection!=null);

            Log.Verbose("New client connection");

            var connectionContext = new ConnectionContext();
            connectionContext.ClientConnection = clientConnection;
            clientConnection.Context = connectionContext;
            
            clientConnection.BeginReceive(HandleDataFromClient);
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
