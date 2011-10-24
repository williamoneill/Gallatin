using System;
using System.Diagnostics.Contracts;
using System.Threading;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    public class ProxySession
    {
        private readonly INetworkFacade _clientConnection;
        private readonly IHttpStreamParser _clientParser;
        private readonly ManualResetEvent _connectingToServer;
        private readonly Guid _id;
        private readonly INetworkFacadeFactory _networkFacadeFactory;
        private string _host;
        private int _port;
        private HttpResponseHeaderEventArgs _responseHeader;
        private INetworkFacade _serverConnection;
        private IHttpStreamParser _serverParser;

        public ProxySession( INetworkFacade clientConnection, INetworkFacadeFactory factory )
        {
            Contract.Requires( clientConnection != null );
            Contract.Requires( factory != null );

            _networkFacadeFactory = factory;

            _id = Guid.NewGuid();

            _clientConnection = clientConnection;
            _clientParser = new HttpStreamParser();

            _connectingToServer = new ManualResetEvent( true );

            _clientParser.AdditionalDataRequested += _clientParser_AdditionalDataRequested;
            _clientParser.ReadRequestHeaderComplete += _clientParser_ReadRequestHeaderComplete;
            _clientParser.PartialDataAvailable += _clientParser_PartialDataAvailable;
        }

        public event EventHandler SessionEnded;

        public void Start()
        {
            Log.Logger.Verbose( "{0} Starting proxy session", _id );

            _clientConnection.BeginReceive( ClientReceive );
        }

        private void EndSession()
        {
            Log.Logger.Verbose( "{0} Ending session", _id );

            try
            {
                if ( _clientConnection != null )
                {
                    _clientConnection.BeginClose(
                        ( s, f ) =>
                        {
                            if ( !s )
                            {
                                Log.Logger.Error( "Error closing client connection" );
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
                                Log.Logger.Error( "Error closing server connection" );
                            }
                        } );
                }

                EventHandler sessionEnded = SessionEnded;
                if ( sessionEnded != null )
                {
                    sessionEnded( this, new EventArgs() );
                }
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception( string.Format( "{0} Unhandled exception ending session", _id ), ex );
                EndSession();
            }
        }

        private void _clientParser_PartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            try
            {
                Log.Logger.Verbose( "{0} Receiving partial data from client", _id );

                if( _connectingToServer.WaitOne(30000) )
                {
                    // Wait for pending server connection
                    if ( _connectingToServer.WaitOne( 30000 ) )
                    {
                        _serverConnection.BeginSend( e.Data, ServerSendComplete );
                    }
                    else
                    {
                        Log.Logger.Error( "Unable to connect to remote host" );
                        EndSession();
                    }
                }
                else
                {
                    Log.Logger.Error("{0} Timed out waiting to connect to server", _id);
                }
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception( string.Format( "{0} Unhandled exception receiving partial data from client", _id ), ex );
                EndSession();
            }
        }

        private void ServerSendComplete( bool success, INetworkFacade serverSocket )
        {
            try
            {
                Log.Logger.Verbose( "{0} Server send complete", _id );

                if ( !success )
                {
                    Log.Logger.Error( "Unable to send data to server" );
                    EndSession();
                }
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception( string.Format( "{0} Unhandled exception sending data to server", _id ), ex );
                EndSession();
            }
        }

        private HttpRequestHeaderEventArgs _requestHeader;

        private void _clientParser_ReadRequestHeaderComplete( object sender, HttpRequestHeaderEventArgs e )
        {
            const int HttpPort = 80;

            try
            {
                Log.Logger.Verbose( "{0} Read request header from client.", _id );

                // Block all threads until we connect
                _connectingToServer.Reset();

                _requestHeader = e;

                string host = e.Headers["Host"];
                int port = HttpPort;
                string[] tokens = host.Split( ':' );

                if ( tokens.Length == 2 )
                {
                    port = int.Parse( tokens[1] );
                    host = tokens[0];
                }

                // TODO: handle SSL

                // Connect/reconnect to server?
                if ( _serverConnection == null || _host != host
                        || _port != port )
                {
                    // At times, the client may change host/port using the same client
                    // connection. Account for that here by disconnecting existing sessions
                    // if host/port changes
                    if ( _serverConnection != null )
                    {
                        Log.Logger.Verbose("{0} Closing existing server connection", _id);

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
                            Log.Logger.Error( "Unable to disconnect new server connection." );
                            EndSession();
                        }
                    }

                    _host = host;
                    _port = port;
                    _networkFacadeFactory.BeginConnect( _host, _port, HandleServerConnect );
                }
                else
                {
                    SendRequestHeaderToServer();
                }
            }
            catch ( Exception ex )
            {
                _connectingToServer.Set();
                Log.Logger.Exception(string.Format("{0} Unhandled exception evaluating server connection", _id), ex);
                EndSession();
            }
        }

        private void SendRequestHeaderToServer()
        {
            Log.Logger.Verbose("{0} Sending header to server", _id);

            _serverConnection.BeginSend( _requestHeader.GetBuffer(), ServerSendComplete );
            _connectingToServer.Set();
        }

        private void HandleServerConnect( bool success, INetworkFacade serverConnection )
        {
            Contract.Requires( serverConnection != null );

            // TODO: tweak socket settings

            try
            {
                Log.Logger.Verbose( "{0} Connected to server", _id );

                if ( success )
                {
                    _serverConnection = serverConnection;

                    if ( _serverParser != null )
                    {
                        _serverParser.AdditionalDataRequested -= _serverParser_AdditionalDataRequested;
                        _serverParser.ReadResponseHeaderComplete -= _serverParser_ReadResponseHeaderComplete;
                        _serverParser.PartialDataAvailable -= _serverParser_PartialDataAvailable;
                        _serverParser.MessageReadComplete -= _serverParser_MessageReadComplete;
                    }

                    _serverParser = new HttpStreamParser();
                    _serverParser.AdditionalDataRequested += _serverParser_AdditionalDataRequested;
                    _serverParser.ReadResponseHeaderComplete += _serverParser_ReadResponseHeaderComplete;
                    _serverParser.PartialDataAvailable += _serverParser_PartialDataAvailable;
                    _serverParser.MessageReadComplete += _serverParser_MessageReadComplete;

                    // Send initial data to server

                    _serverConnection.BeginReceive( HandleDataFromServer );

                    SendRequestHeaderToServer();
                }
                else
                {
                    Log.Logger.Error( "Unable to connect to {0} {1}", _host, _port );
                }
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception( string.Format( "{0} Unhandled exception handling server connection", _id ), ex );
                EndSession();
            }
        }

        private void HandleDataFromServer( bool success, byte[] data, INetworkFacade server )
        {
            try
            {
                Log.Logger.Verbose( "{0} Processing data from server", _id );

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
                Log.Logger.Exception( string.Format( "{0} Unhandled exception handling data from server", _id ), ex );
                EndSession();
            }
        }

        private void _serverParser_MessageReadComplete( object sender, EventArgs e )
        {
            try
            {
                Log.Logger.Verbose( "{0} Complete message sent to client. Evaluating persistent connection.", _id );
                // Evaluate persistent connections

                // HTTP 1.1, assume persistent connection
                if ( _responseHeader.Version == "1.1" )
                {
                    string persistentConnection = _responseHeader.Headers["connection"];

                    if ( persistentConnection != null
                         && persistentConnection.Equals( "close", StringComparison.InvariantCultureIgnoreCase ) )
                    {
                        Log.Logger.Verbose("{0} Ending connection (explicit close)", _id);
                        EndSession();
                    }
                    else
                    {
                        Log.Logger.Verbose("{0} Maintaining persistent connection", _id);
                    }
                }
                else
                {
                    Log.Logger.Verbose("{0} Ending connection (not HTTP 1.1)", _id);
                    EndSession();
                }
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception( string.Format( "{0} Unhandled exception handling complete message from server", _id ), ex );
                EndSession();
            }
        }

        private void HandleDataSentToClient( bool success, INetworkFacade client )
        {
            try
            {
                Log.Logger.Verbose( "{0} Data sent to client", _id );

                if ( !success )
                {
                    Log.Logger.Error( "Unable to send data to client" );
                    EndSession();
                }
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception( string.Format( "{0} Unhandled exception sending data to client", _id ), ex );
                EndSession();
            }
        }

        private void _serverParser_PartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            try
            {
                Log.Logger.Verbose( "{0} Partial data available from server", _id );

                _clientConnection.BeginSend( e.Data, HandleDataSentToClient );
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception( string.Format( "{0} Unhandled exception receiving partial data from server", _id ), ex );
                EndSession();
            }
        }

        private void _serverParser_ReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            Log.Logger.Verbose( "{0} Read response header from server", _id );
            _responseHeader = e;

            _clientConnection.BeginSend(e.GetBuffer(), HandleDataSentToClient);
        }

        private void _serverParser_AdditionalDataRequested( object sender, EventArgs e )
        {
            Log.Logger.Verbose( "{0} Additional data needed from server to complete request", _id );
            _serverConnection.BeginReceive( ServerReceive );
        }

        private void _clientParser_AdditionalDataRequested( object sender, EventArgs e )
        {
            Log.Logger.Verbose( "{0} Additional data needed from client to complete request", _id );
            _clientConnection.BeginReceive( ClientReceive );
        }

        private void ClientReceive( bool success, byte[] data, INetworkFacade client )
        {
            Log.Logger.Verbose( "{0} Receiving client data", _id );

            if ( success )
            {
                _clientParser.AppendData( data );
            }
            else
            {
                Log.Logger.Error( "{0} Failed to receive data from client", _id );
                EndSession();
            }
        }

        private void ServerReceive( bool success, byte[] data, INetworkFacade server )
        {
            Log.Logger.Verbose( "{0} Receiving server data", _id );

            if ( success )
            {
                _serverParser.AppendData( data );
            }
            else
            {
                Log.Logger.Error( "{0} Failed to receive data from server", _id );
                EndSession();
            }
        }
    }
}