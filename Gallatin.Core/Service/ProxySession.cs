using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    [PartCreationPolicy( CreationPolicy.NonShared )]
    [Export( typeof (IProxySession) )]
    internal class ProxySession : IProxySession
    {
        private readonly IHttpStreamParser _clientParser;
        private readonly ManualResetEvent _connectingToServerEvent;
        private readonly IProxyFilter _filters;
        private readonly INetworkFacadeFactory _networkFacadeFactory;
        private INetworkFacade _clientConnection;
        private string _host;
        private bool _isFiltered;
        private int _port;
        private HttpRequestHeaderEventArgs _requestHeader;
        private HttpResponseHeaderEventArgs _responseHeader;
        private INetworkFacade _serverConnection;
        private IHttpStreamParser _serverParser;

        [ImportingConstructor]
        public ProxySession( INetworkFacadeFactory factory, IProxyFilter filters )
        {
            Contract.Requires( factory != null );
            Contract.Requires( filters != null );

            _filters = filters;

            _networkFacadeFactory = factory;

            _clientParser = new HttpStreamParser();

            _connectingToServerEvent = new ManualResetEvent( true );

            WireClientEvents();
        }

        #region IProxySession Members

        public string Id
        {
            get
            {
                return string.Format( "C {0} S {1} ",
                                      _clientConnection == null ? 0 : _clientConnection.Id,
                                      _serverConnection == null ? 0 : _serverConnection.Id );
            }
        }

        public event EventHandler SessionEnded;

        public void Start( INetworkFacade clientConnection )
        {
            _clientConnection = clientConnection;

            ServiceLog.Logger.Verbose( "{0} Starting proxy session", Id );

            _clientConnection.BeginReceive( HandleClientReceive );
        }

        public void Reset()
        {
            ServiceLog.Logger.Info("{0} Resetting client connection", Id);

            UnwireClientEvents();
            UnwireServerEvents();
            _clientConnection = null;
            _serverConnection = null;
            _clientParser.Reset();
            _host = null;
            _isFiltered = false;
            _port = 0;
            _requestHeader = null;
            _responseHeader = null;

            if ( _serverParser != null )
            {
                _serverParser.Reset();
            }
        }

        #endregion

        private void WireClientEvents()
        {
            _clientParser.AdditionalDataRequested += HandleClientParserAdditionalDataRequested;
            _clientParser.ReadRequestHeaderComplete += HandleClientParserReadRequestHeaderComplete;
            _clientParser.PartialDataAvailable += HandleClientParserPartialDataAvailable;
        }

        private void UnwireClientEvents()
        {
            if ( _clientParser != null )
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
            if ( _serverParser != null )
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
                UnwireServerEvents();

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
            catch
            {
                ServiceLog.Logger.Info("{0} Exception while ending session", Id);
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

                if (_connectingToServerEvent.WaitOne(CoreSettings.Instance.ConnectTimeout))
                {
                    // Do not forward the header to the server when SSL. The SSL stream class will
                    // send the appropriate headers for a proxy server.
                    if ( !_requestHeader.IsSsl )
                    {
                        _serverConnection.BeginSend( e.Data, HandleServerSendComplete );
                    }
                    else
                    {
                        ServiceLog.Logger.Error( "Unable to connect to remote host" );
                        EndSession();
                    }
                }
                else
                {
                    ServiceLog.Logger.Error("{0} Timed out waiting to connect to server", Id);
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
                _connectingToServerEvent.Reset();

                _requestHeader = e;

                string filter = _filters.EvaluateConnectionFilters( HttpRequest.CreateRequest( e ), _clientConnection.ConnectionId );
                if ( filter != null )
                {
                    _isFiltered = true;
                    _clientConnection.BeginSend( Encoding.UTF8.GetBytes( filter ), HandleDataSentToClient );
                }
                else
                {
                    string host = e.Headers["Host"];
                    int port = HttpPort;

                    string[] tokens = host.Split(':');
                    if (tokens.Length == 2)
                    {
                        port = int.Parse(tokens[1]);
                        host = tokens[0];
                    }

                    // With SSL (HTTPS) the path is the host name and port
                    if (_requestHeader.IsSsl)
                    {
                        string[] pathTokens = _requestHeader.Path.Split( ':' );

                        if ( tokens.Length == 2 )
                        {
                            port = int.Parse(pathTokens[1]);
                            host = pathTokens[0];
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


                            if ( !waitForServerDisconnectEvent.WaitOne( CoreSettings.Instance.ConnectTimeout ) )
                            {
                                ServiceLog.Logger.Error( "Unable to disconnect new server connection." );
                                EndSession();
                            }
                        }

                        _host = host;
                        _port = port;
                        ServiceLog.Logger.Info( "{0} Connecting to remote host {1}:{2}", Id, _host, _port );
                        _networkFacadeFactory.BeginConnect( _host, _port, HandleServerConnect );
                    }
                    else
                    {
                        // Already connected. Send request.
                        SendRequestHeaderToServer();
                    }
                }
            }
            catch ( Exception ex )
            {
                _connectingToServerEvent.Set();
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception evaluating server connection", Id ), ex );
                EndSession();
            }
        }

        private void SendRequestHeaderToServer()
        {
            try
            {
                ServiceLog.Logger.Info("{0} Sending header to server", Id);

                _serverConnection.BeginSend(_requestHeader.GetBuffer(), HandleServerSendComplete);
                _connectingToServerEvent.Set();
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format("{0} Exception sending request header to server", Id), ex );
                EndSession();
            }
        }

        private void HandleServerConnect( bool success, INetworkFacade serverConnection )
        {
            try
            {
                ServiceLog.Logger.Verbose( "{0} Connected to server", Id );

                if ( success )
                {
                    _serverConnection = serverConnection;

                    if ( _requestHeader.IsSsl )
                    {
                        UnwireClientEvents();

                        _connectingToServerEvent.Set();

                        ServiceLog.Logger.Info( "{0} Changing to SSL tunnel", Id );

                        ISslTunnel tunnel = CoreFactory.Compose<ISslTunnel>();
                        tunnel.TunnelClosed += HandleSslTunnelClosed;
                        tunnel.EstablishTunnel(_clientConnection, _serverConnection, _requestHeader.Version);
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

                if (_responseHeader != null && !_responseHeader.IsPersistent)
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

                if ( success )
                {
                    if ( _isFiltered )
                    {
                        ServiceLog.Logger.Info( "{0} Proxy returned filtered data. Ending connection.", Id );
                        EndSession();
                    }
                }
                else
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
            try
            {
                ServiceLog.Logger.Info( "{0} Read response header from server", Id );
                _responseHeader = e;

                // Consult the response filters to see if any are interested in the entire body.
                // Don't build the response body unless we have to; it's expensive.
                // If any filters can make the judgement now, before we read the body, then use their response to
                // short-circuit the body evaluation.
                string filterResponse;
                if ( _filters.TryEvaluateResponseFilters( HttpResponse.CreateResponse( _responseHeader ),
                                                          _clientConnection.ConnectionId,
                                                          out filterResponse ) )
                {
                    // Filter active and does not need HTTP body
                    if ( filterResponse != null )
                    {
                        ServiceLog.Logger.Info( "{0} Response filter blocking content", Id );

                        // Stop listening for more data from the server. We are creating our own response.
                        // The session will terminate once this response is sent to the client.
                        UnwireServerEvents();
                        _isFiltered = true;
                        _clientConnection.BeginSend( Encoding.UTF8.GetBytes( filterResponse ), HandleDataSentToClient );
                    }
                    else
                    {
                        // Normal behavior. No filter activated.
                        _clientConnection.BeginSend( e.GetBuffer(), HandleDataSentToClient );
                    }
                }
                else
                {
                    // Prepare to receive the entire HTTP body
                    ServiceLog.Logger.Info( "{0} Response filter requires entire body. Building HTTP body.", Id );

                    _serverParser.PartialDataAvailable -= HandleServerParserPartialDataAvailable;
                    _serverParser.MessageReadComplete -= HandleServerParserMessageReadComplete;
                    _serverParser.BodyAvailable += HandleServerParserBodyAvailable;
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Error evaluating response filter", Id ), ex );
                EndSession();
            }
        }

        private void HandleServerParserBodyAvailable( object sender, HttpDataEventArgs e )
        {
            try
            {
                ServiceLog.Logger.Info( "{0} Evaluating HTTP response filter using HTTP body", Id );

                // Unsubscribe to the body available event because it is expensive. Resume
                // partial read for the next message, if any, in a persistent connection
                _serverParser.BodyAvailable -= HandleServerParserBodyAvailable;
                _serverParser.PartialDataAvailable += HandleServerParserPartialDataAvailable;
                _serverParser.MessageReadComplete += HandleServerParserMessageReadComplete;

                byte[] filter = _filters.EvaluateResponseFiltersWithBody( HttpResponse.CreateResponse( _responseHeader ),
                                                                          _clientConnection.ConnectionId,
                                                                          e.Data );

                _clientConnection.BeginSend( _responseHeader.GetBuffer(), HandleDataSentToClient );

                if ( filter != null && filter.Length > 0 )
                {
                    ServiceLog.Logger.Info( "{0} Response filter activated. Body modified.", Id );
                    _isFiltered = true;
                    _clientConnection.BeginSend( filter, HandleDataSentToClient );
                }
                else
                {
                    _clientConnection.BeginSend( e.Data, HandleDataSentToClient );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Error sending filtered body to client", Id ), ex );
                EndSession();
            }
        }

        private void HandleServerParserAdditionalDataRequested( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} Additional data needed from server to complete request", Id );
            _serverConnection.BeginReceive( HandleDataFromServer );
        }

        private void HandleClientParserAdditionalDataRequested( object sender, EventArgs e )
        {
            try
            {
                ServiceLog.Logger.Verbose("{0} Additional data needed from client to complete request", Id);
                if (_connectingToServerEvent.WaitOne(CoreSettings.Instance.ConnectTimeout))
                {
                    // Ignore the event the parser requires requesting more data if we are changing over to an SSL tunnel
                    if (_requestHeader != null && !_requestHeader.IsSsl)
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
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(string.Format("{0} Error handling additional data request from client", Id), ex);
                EndSession();
            }
        }

        private void HandleClientReceive( bool success, byte[] data, INetworkFacade client )
        {
            try
            {
                ServiceLog.Logger.Verbose("{0} Receiving client data", Id);

                if (success)
                {
                    _clientParser.AppendData(data);
                }
                else
                {
                    ServiceLog.Logger.Info("{0} Client closed connection.", Id);
                    EndSession();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(string.Format("{0} Error handling data from client", Id), ex);
                EndSession();
            }

        }
    }
}