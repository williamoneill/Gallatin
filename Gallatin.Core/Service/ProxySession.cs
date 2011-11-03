using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Threading;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(IProxySession))]
    internal class ProxySession : IProxySession
    {
        private INetworkFacade _clientConnection;
        private readonly IHttpStreamParser _clientParser;
        private readonly ManualResetEvent _connectingToServer;
        private readonly INetworkFacadeFactory _networkFacadeFactory;
        private string _host;
        private int _port;
        private HttpRequestHeaderEventArgs _requestHeader;
        private HttpResponseHeaderEventArgs _responseHeader;
        private INetworkFacade _serverConnection;
        private IHttpStreamParser _serverParser;

        [ImportingConstructor]
        public ProxySession(  INetworkFacadeFactory factory )
        {
            Contract.Requires( factory != null );

            _networkFacadeFactory = factory;

            _clientParser = new HttpStreamParser();

            _connectingToServer = new ManualResetEvent( true );

            WireClientEvents();
        }

        public string Id
        {
            get
            {
                return string.Format("C {0} S {1} ",
                                      _clientConnection == null ? 0 : _clientConnection.Id,
                                      _serverConnection == null ? 0 : _serverConnection.Id);
            }
        }

        public event EventHandler SessionEnded;

        public void Start(INetworkFacade clientConnection)
        {
            Contract.Requires(clientConnection!=null);

            _clientConnection = clientConnection;

            ServiceLog.Logger.Verbose("{0} Starting proxy session", Id);

            _clientConnection.BeginReceive( HandleClientReceive );
        }

        private void WireClientEvents()
        {
            _clientParser.AdditionalDataRequested += HandleClientParserAdditionalDataRequested;
            _clientParser.ReadRequestHeaderComplete += HandleClientParserReadRequestHeaderComplete;
            _clientParser.PartialDataAvailable += HandleClientParserPartialDataAvailable;
            
        }

        private void UnwireClientEvents()
        {
            if (_clientParser != null)
            {
                _clientParser.AdditionalDataRequested -= HandleClientParserAdditionalDataRequested;
                _clientParser.ReadRequestHeaderComplete -= HandleClientParserReadRequestHeaderComplete;
                _clientParser.PartialDataAvailable -= HandleClientParserPartialDataAvailable;
                
            }
            
        }

        private void WireServerEvents()
        {
            _serverParser.AdditionalDataRequested += HandleServerParserAdditionalDataRequested;
            _serverParser.ReadResponseHeaderComplete += HandleServerParserReadResponseHeaderComplete;
            _serverParser.PartialDataAvailable += HandleServerParserPartialDataAvailable;
            _serverParser.MessageReadComplete += HandleServerParserMessageReadComplete;
        }

        private void UnwireServerEvents()
        {
            if (_serverParser != null)
            {
                _serverParser.AdditionalDataRequested -= HandleServerParserAdditionalDataRequested;
                _serverParser.ReadResponseHeaderComplete -= HandleServerParserReadResponseHeaderComplete;
                _serverParser.PartialDataAvailable -= HandleServerParserPartialDataAvailable;
                _serverParser.MessageReadComplete -= HandleServerParserMessageReadComplete;
            }
        }

        private void EndSession()
        {
            ServiceLog.Logger.Verbose( "{0} Ending session", Id );

            try
            {
                UnwireClientEvents();

                if ( _clientConnection != null )
                {
                    _clientConnection.BeginClose(
                        ( s, f ) =>
                        {
                            if ( !s )
                            {
                                ServiceLog.Logger.Error( "Error closing client connection" );
                            }
                        } );
                }

                UnwireServerEvents();

                if ( _serverConnection != null )
                {
                    _serverConnection.BeginClose(
                        ( s, f ) =>
                        {
                            if ( !s )
                            {
                                ServiceLog.Logger.Error( "Error closing server connection" );
                            }
                        } );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception ending session", Id ), ex );
            }

            EventHandler sessionEnded = SessionEnded;
            if ( sessionEnded != null )
            {
                sessionEnded( this, new EventArgs() );
            }
        }

        private void HandleClientParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            try
            {
                ServiceLog.Logger.Info( "{0} Receiving partial data from client", Id );

                if ( _connectingToServer.WaitOne( 30000 ) )
                {
                    // Wait for pending server connection
                    if ( _connectingToServer.WaitOne( 30000 ) )
                    {
                        // Do not forward the header to the server when SSL. The SSL stream class will
                        // send the appropriate headers for a proxy server.
                        if (!_requestHeader.IsSsl)
                        {
                            _serverConnection.BeginSend(e.Data, HandleServerSendComplete);
                        }
                    }
                    else
                    {
                        ServiceLog.Logger.Error( "Unable to connect to remote host" );
                        EndSession();
                    }
                }
                else
                {
                    ServiceLog.Logger.Error( "{0} Timed out waiting to connect to server", Id );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception receiving partial data from client", Id ), ex );
                EndSession();
            }
        }

        private void HandleServerSendComplete( bool success, INetworkFacade serverSocket )
        {
            try
            {
                ServiceLog.Logger.Verbose( "{0} Server send complete", Id );

                if ( !success )
                {
                    ServiceLog.Logger.Error( "Unable to send data to server" );
                    EndSession();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception sending data to server", Id ), ex );
                EndSession();
            }
        }

        private void HandleClientParserReadRequestHeaderComplete( object sender, HttpRequestHeaderEventArgs e )
        {
            const int HttpPort = 80;

            try
            {
                ServiceLog.Logger.Verbose( "{0} Read request header from client.", Id );

                // Block all threads until we connect
                _connectingToServer.Reset();

                _requestHeader = e;

                string host = e.Headers["Host"];
                int port = HttpPort;

                if ( _requestHeader.IsSsl )
                {
                    string[] tokens = _requestHeader.Path.Split( ':' );

                    if ( tokens.Length == 2 )
                    {
                        port = int.Parse( tokens[1] );
                        host = tokens[0];
                    }
                }

                // Connect/reconnect to server?
                if ( _serverConnection == null || _host != host
                     || _port != port )
                {
                    // At times, the client may change host/port using the same client
                    // connection. Account for that here by disconnecting existing sessions
                    // if host/port changes
                    if ( _serverConnection != null )
                    {
                        ServiceLog.Logger.Verbose( "{0} Closing existing server connection", Id );

                        ManualResetEvent waitForServerDisconnectEvent = new ManualResetEvent( true );

                        waitForServerDisconnectEvent.Reset();

                        _serverConnection.BeginClose(
                            ( s, f ) =>
                            {
                                if ( s )
                                {
                                    waitForServerDisconnectEvent.Set();
                                }
                            } );


                        if ( !waitForServerDisconnectEvent.WaitOne( 10000 ) )
                        {
                            ServiceLog.Logger.Error( "Unable to disconnect new server connection." );
                            EndSession();
                        }
                    }

                    _host = host;
                    _port = port;
                    ServiceLog.Logger.Info("{0} Connecting to remote host {1}:{2}", Id, _host, _port);
                    _networkFacadeFactory.BeginConnect( _host, _port, HandleServerConnect );
                }
                else
                {
                    // Already connected. Send request.
                    SendRequestHeaderToServer();
                }
            }
            catch ( Exception ex )
            {
                _connectingToServer.Set();
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception evaluating server connection", Id ), ex );
                EndSession();
            }
        }

        private void SendRequestHeaderToServer()
        {
            ServiceLog.Logger.Info( "{0} Sending header to server", Id );

            _serverConnection.BeginSend( _requestHeader.GetBuffer(), HandleServerSendComplete );
            _connectingToServer.Set();
        }

        private void HandleServerConnect( bool success, INetworkFacade serverConnection )
        {
            Contract.Requires( serverConnection != null );

            try
            {
                ServiceLog.Logger.Verbose( "{0} Connected to server", Id );

                if ( success )
                {
                    _serverConnection = serverConnection;

                    if ( _requestHeader.IsSsl )
                    {
                        UnwireClientEvents();

                        _connectingToServer.Set();

                        ServiceLog.Logger.Info("{0} Chaning to SSL tunnel", Id);

                        SslTunnel tunnel = new SslTunnel( _clientConnection, _serverConnection, _requestHeader.Version );
                        tunnel.TunnelClosed += HandleSslTunnelClosed;
                        tunnel.EstablishTunnel();
                    }
                    else
                    {
                        UnwireServerEvents();

                        _serverParser = new HttpStreamParser();
                        WireServerEvents();

                        _serverConnection.BeginReceive( HandleDataFromServer );

                        SendRequestHeaderToServer();
                    }
                }
                else
                {
                    ServiceLog.Logger.Error( "Unable to connect to {0} {1}", _host, _port );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception handling server connection", Id ), ex );
                EndSession();
            }
        }

        private void HandleSslTunnelClosed( object sender, EventArgs e )
        {
            EndSession();
        }

        private void HandleDataFromServer( bool success, byte[] data, INetworkFacade server )
        {
            try
            {
                ServiceLog.Logger.Verbose( "{0} Processing data from server", Id );

                if ( success )
                {
                    _serverParser.AppendData( data );
                }
                else
                {
                    EndSession();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception handling data from server", Id ), ex );
                EndSession();
            }
        }

        private void HandleServerParserMessageReadComplete( object sender, EventArgs e )
        {
            try
            {
                ServiceLog.Logger.Verbose( "{0} Complete message sent to client. Evaluating persistent connection.", Id );

                if ( !_responseHeader.IsPersistent )
                {
                    ServiceLog.Logger.Verbose( "{0} Ending connection (explicit close)", Id );
                    EndSession();
                }
                else
                {
                    ServiceLog.Logger.Verbose( "{0} Maintaining persistent connection", Id );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception handling complete message from server", Id ), ex );
                EndSession();
            }
        }

        private void HandleDataSentToClient( bool success, INetworkFacade client )
        {
            try
            {
                ServiceLog.Logger.Verbose( "{0} Data sent to client", Id );

                if ( !success )
                {
                    ServiceLog.Logger.Error( "Unable to send data to client" );
                    EndSession();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception sending data to client", Id ), ex );
                EndSession();
            }
        }

        private void HandleServerParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            try
            {
                ServiceLog.Logger.Info( "{0} Partial data available from server {1}", Id, e.Data.Length );
                _clientConnection.BeginSend( e.Data, HandleDataSentToClient );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception receiving partial data from server", Id ), ex );
                EndSession();
            }
        }

        private void HandleServerParserReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            ServiceLog.Logger.Info( "{0} Read response header from server", Id );
            _responseHeader = e;

            _clientConnection.BeginSend( e.GetBuffer(), HandleDataSentToClient );
        }

        private void HandleServerParserAdditionalDataRequested( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} Additional data needed from server to complete request", Id );
            _serverConnection.BeginReceive( HandleServerReceive );
        }

        private void HandleClientParserAdditionalDataRequested( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} Additional data needed from client to complete request", Id );
            if (_connectingToServer.WaitOne(30000))
            {
                // Ignore the event the parser requires requesting more data if we are changing over to an SSL tunnel
                if (!_requestHeader.IsSsl)
                {
                    _clientConnection.BeginReceive(HandleClientReceive);
                }
            }
            else
            {
                ServiceLog.Logger.Verbose("{0} Timed out waiting for server connection", Id);
                EndSession();
            }
        }

        private void HandleClientReceive( bool success, byte[] data, INetworkFacade client )
        {
            ServiceLog.Logger.Verbose( "{0} Receiving client data", Id );

            if ( success )
            {
                _clientParser.AppendData( data );
            }
            else
            {
                ServiceLog.Logger.Info( "{0} Client closed connection.", Id );
                EndSession();
            }
        }

        private void HandleServerReceive( bool success, byte[] data, INetworkFacade server )
        {
            ServiceLog.Logger.Verbose( "{0} Receiving server data", Id );

            if ( success )
            {
                _serverParser.AppendData( data );
            }
            else
            {
                ServiceLog.Logger.Info( "{0} Server closed connection", Id );
                EndSession();
            }
        }
    }
}