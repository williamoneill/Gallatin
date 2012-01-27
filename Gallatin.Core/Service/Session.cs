using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Net
{
    [PartCreationPolicy( CreationPolicy.NonShared )]
    [Export( typeof (ISession) )]
    internal class Session : ISession
    {
        private readonly ISessionLogger _logger;
        private readonly object _mutex = new object();
        private readonly Semaphore _serverConnectingEvent;
        private readonly IServerDispatcher _serverDispatcher;
        private INetworkConnection _connection;
        private IHttpRequest _lastRequest;
        private IHttpStreamParser _parser;
        private IAccessLog _accessLog;

        [ImportingConstructor]
        public Session( IAccessLog accessLog, IServerDispatcher dispatcher )
        {
            Contract.Ensures( accessLog != null );
            Contract.Ensures( dispatcher != null );

            _accessLog = accessLog;

            Id = Guid.NewGuid().ToString();

            _serverConnectingEvent = new Semaphore( 1, 1 );
            _logger = new SessionLogger( Id );

            _serverDispatcher = dispatcher;
            _serverDispatcher.ActiveServerClosedConnection += new EventHandler(_serverDispatcher_ActiveServerClosedConnection);
            _serverDispatcher.ServerDataAvailable += ServerDispatcherServerDataAvailable;
            _serverDispatcher.Logger = _logger;

            _logger.Verbose( "Creating session" );
        }

        void _serverDispatcher_ActiveServerClosedConnection(object sender, EventArgs e)
        {
            _logger.Info("Active server closed connection. Resetting session.");
            Reset();
        }

        #region ISession Members

        public void Reset()
        {
            lock ( _mutex )
            {
                if ( _connection != null )
                {
                    _logger.Info( "Resetting proxy client connection" );

                    _connection.ConnectionClosed -= ConnectionConnectionClosed;
                    _connection.DataAvailable -= ConnectionDataAvailable;
                    _connection.Shutdown -= ConnectionReceiveShutdown;
                    _connection.Close();
                    _connection = null;

                    _parser.ReadRequestHeaderComplete -= ParserReadRequestHeaderComplete;
                    _parser.PartialDataAvailable -= ParserPartialDataAvailable;
                    _parser = null;

                    _serverDispatcher.Reset();

                    EventHandler sessionEndedEvent = SessionEnded;
                    if ( sessionEndedEvent != null )
                    {
                        sessionEndedEvent( this, new EventArgs() );
                    }

                    _tunnel = null;
                }
            }
        }

        public string Id { get; private set; }
        public event EventHandler SessionEnded;

        public void Start( INetworkConnection connection )
        {
            Contract.Requires( connection != null );

            _logger.Info( "Starting new proxy client session" );

            lock ( _mutex )
            {
                _parser = new HttpStreamParser();
                _parser.ReadRequestHeaderComplete += ParserReadRequestHeaderComplete;
                _parser.PartialDataAvailable += ParserPartialDataAvailable;

                _connection = connection;
                _connection.Logger = _logger;
                _connection.ConnectionClosed += ConnectionConnectionClosed;
                _connection.DataAvailable += ConnectionDataAvailable;
                _connection.Shutdown += ConnectionReceiveShutdown;
                _connection.Start();
            }
        }

        #endregion

        private void ServerDispatcherServerDataAvailable( object sender, DataAvailableEventArgs e )
        {
            _logger.Verbose("Data available from server dispatcher");

            try
            {
                if ( _connection != null )
                {
                    lock ( _connection )
                    {
                        _connection.SendData( e.Data );
                    }
                }
                else
                {
                    _logger.Info("Client connection closed. Ignoring data from server.");
                }
            }
            catch ( Exception ex )
            {
                _logger.Exception( "Unhandled exception processing server data", ex );
                Reset();
            }
        }

        private void ParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            _logger.Verbose("Partial data available from client");

            try
            {
                // Wait for the server connection before sending data
                _logger.Verbose( "Sending data to remote host" );
                _serverConnectingEvent.WaitOne();
                if( !_serverDispatcher.TrySendDataToActiveServer( e.Data ) )
                {
                    _logger.Info("Unable to send data to active server. No servers may remain.");
                    Reset();
                }
            }
            catch ( Exception ex )
            {
                _logger.Exception( "Unhandled exception sending partial data to server", ex );
                Reset();
            }
            finally
            {
                _serverConnectingEvent.Release();
            }
        }

        private IHttpsTunnel _tunnel;

        private void EstablishSslConnection(string host, int port, string version )
        {
            _logger.Info("Starting SSL tunnel");

            // When establishing an SSL connection, stop processing data from the client. The data
            // will be encrypted and is meaningless to the proxy service. This event is re-wired
            // when the next connection is restarted.
            _connection.DataAvailable -= ConnectionDataAvailable;

            // TODO: consider making this a dependency and allowing it to reset (inherit from IPooledObject)
            _tunnel = CoreFactory.Compose<IHttpsTunnel>();

            _tunnel.TunnelClosed += (sender, args) =>
                                       {
                                           _logger.Info("Releasing HTTPS tunnel");
                                           _serverConnectingEvent.Release();
                                           Reset();
                                       };

            _tunnel.EstablishTunnel(host, port, version, _connection );
        }

        private void ParserReadRequestHeaderComplete( object sender, HttpRequestHeaderEventArgs e )
        {
            _logger.Verbose( () => string.Format( "Read HTTP request header\r\n{0}", Encoding.UTF8.GetString( e.GetBuffer() ) ) );

            try
            {
                _serverConnectingEvent.WaitOne();

                _lastRequest = HttpRequest.CreateRequest( e );

                string host;
                int port;

                if ( TryParseAddress( _lastRequest, out host, out port ) )
                {
                    _logger.Info( string.Format( "Connecting to {0}:{1}", host, port ) );

                    if(_lastRequest.IsSsl)
                    {
                        _accessLog.Write(_connection.Id, _lastRequest, "SSL TUNNEL");
                        EstablishSslConnection(host, port, _lastRequest.Version);
                    }
                    else
                    {
                        _accessLog.Write(_connection.Id, _lastRequest, "ACCESS GRANTED");
                        _serverDispatcher.ConnectToServer(host, port, ServerConnected);
                    }
                }
                else
                {
                    _logger.Error( "Request header not a recognized format" );
                    Reset();
                }
            }
            catch ( Exception ex )
            {
                _logger.Exception( "Unhandled exception parsing request header", ex );
                Reset();
            }
        }

        private void ServerConnected( bool success )
        {
            try
            {
                if ( success )
                {
                    // Send the HTTP request header before letting any HTTP body data be sent
                    _logger.Info( "Connected to server" );
                    if(!_serverDispatcher.TrySendDataToActiveServer( _lastRequest.GetBuffer() ) )
                    {
                        _logger.Error("Unable to send data to active server.");
                        Reset();
                    }
                }
                else
                {
                    _logger.Info( "Failed to connect to server" );

                    // TODO: this may not be what we need. Other connections could still be active.
                    Reset();
                }
            }
            catch ( Exception ex )
            {
                _logger.Exception( "Unhandled exception evaluating server connection", ex );
                Reset();
            }
            finally
            {
                _serverConnectingEvent.Release();
            }
        }

        public static bool TryParseAddress( IHttpRequest e, out string host, out int port )
        {
            const int HttpPort = 80;

            host = null;
            port = 0;

            // With SSL (HTTPS) the path is the host name and port
            if ( e.IsSsl )
            {
                string[] pathTokens = e.Path.Split( ':' );

                if ( pathTokens.Length == 2 )
                {
                    port = Int32.Parse( pathTokens[1] );
                    host = pathTokens[0];
                }
            }
            else
            {
                host = e.Headers["Host"];
                port = HttpPort;

                // Get the port from the host address if it set
                string[] tokens = host.Split( ':' );
                if ( tokens.Length == 2 )
                {
                    host = tokens[0];

                    if ( !int.TryParse( tokens[1], out port ) )
                    {
                        return false;
                    }
                }
            }

            return !string.IsNullOrEmpty( host ) && port > 0;
        }


        private void ConnectionReceiveShutdown( object sender, EventArgs e )
        {
            _logger.Info( "Client stopped receiving data. Close session." );
            Reset();
        }

        private void ConnectionDataAvailable( object sender, DataAvailableEventArgs e )
        {
            _logger.Info("Received data from client");
            _parser.AppendData(e.Data);
        }

        private void ConnectionConnectionClosed( object sender, EventArgs e )
        {
            _logger.Info( "Client connection closed. Close session." );
            Reset();
        }
    }
}