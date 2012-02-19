using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Threading;
using Gallatin.Core.Filters;
using Gallatin.Core.Service;

namespace Gallatin.Core.Net
{
    [Export( typeof (IServerDispatcher) )]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class ServerDispatcher : IServerDispatcher
    {
        private readonly Semaphore _connectingToRemoteHost = new Semaphore( 1, 1 );
        private readonly INetworkConnectionFactory _factory;
        private readonly List<IHttpServer> _serverConnections;
        private IHttpServer _activeServer;
        private Action<bool> _connectCallback;

        private string _host;
        private int _port;

        [ImportingConstructor]
        public ServerDispatcher( INetworkConnectionFactory factory )
        {
            Contract.Requires( factory != null );

            Logger = new DefaultSessionLogger();

            _factory = factory;
            _serverConnections = new List<IHttpServer>();
        }

        #region IServerDispatcher Members

        public void ConnectToServer( string host, int port, IHttpResponseFilter filter, Action<bool> callback )
        {
            Logger.Verbose("Waiting for HTTP response semaphore");

            if ( host == _host
                 && port == _port
                && _activeServer != null)
            {
                Logger.Info( "Reusing existing connection" );
                callback( true );
            }
            else
            {
                // Block other connections until this is established. Protect member variables...
                _connectingToRemoteHost.WaitOne();

                Logger.Info("Server Dispatcher -- Connecting to remote host");

                _host = host;
                _port = port;
                _connectCallback = callback;
                _responseFilter = filter;
                _factory.BeginConnect( host, port, HandleConnect );
            }
        }

        private IHttpResponseFilter _responseFilter;

        public bool TrySendDataToActiveServer( byte[] data )
        {
            Logger.Verbose( "Sending data to active server" );

            if ( _activeServer != null )
            {
                _activeServer.Send( data );
                return true;
            }

            return false;
        }

        public void Reset()
        {
            Logger.Verbose( "Resetting server dispatcher" );

            lock ( _serverConnections )
            {
                foreach ( IHttpServer serverConnection in _serverConnections.ToArray() )
                {
                    RemoveServer( serverConnection );
                }
            }
        }

        public event EventHandler<DataAvailableEventArgs> ServerDataAvailable;

        public ISessionLogger Logger { private get; set; }
        public event EventHandler ActiveServerClosedConnection;

        #endregion


        private void HandleConnect( bool success, INetworkConnection connection )
        {
            Logger.Info( "Connected to remote host" );

            try
            {
                if ( success )
                {
                    lock ( _serverConnections )
                    {
                        if (_activeServer != null)
                        {
                            Logger.Verbose("Unwiring from previous server connection");

                            // Do NOT unsubscribe from the session closed event here. We need that to trigger the clean up.
                            _activeServer.DataAvailable -= ActiveServerDataAvailable;

                            //_activeServer.ReceivedCompleteHttpResponse -= new EventHandler(_activeServer_ReceivedCompleteHttpResponse);
                        }

                        connection.Logger = Logger;
                        _activeServer = new HttpServer( connection, _responseFilter );
                        _serverConnections.Add( _activeServer );

                        _activeServer.SessionClosed += ServerConnectionClosed;
                        _activeServer.DataAvailable += ActiveServerDataAvailable;
                        //_activeServer.ReceivedCompleteHttpResponse += new EventHandler(_activeServer_ReceivedCompleteHttpResponse);

                        connection.Start();

                        _connectCallback( true );
                    }
                }
                else
                {
                    Logger.Error( "Unable to connect to remote host. Closing proxy session." );
                    _connectCallback( false );
                }
            }
            catch ( Exception ex )
            {
                Logger.Exception( "Unhandled exception handling connection to remote host", ex );
                _connectCallback( false );
            }
            finally
            {
                _connectingToRemoteHost.Release();
            }
        }

        private void ActiveServerDataAvailable( object sender, DataAvailableEventArgs e )
        {
            Logger.Verbose( "Data available from server" );

            EventHandler<DataAvailableEventArgs> ev = ServerDataAvailable;
            if ( ev != null )
            {
                ev( this, e );
            }
        }

        private void RemoveServer( IHttpServer server )
        {
            lock ( _serverConnections )
            {
                _serverConnections.Remove( server );
            }

            if ( _activeServer == server )
            {
                var ev = ActiveServerClosedConnection;
                if(ev != null)
                    ev(this, new EventArgs());

                Logger.Verbose( "Active server closed connection. Clearing active server reference." );
                _activeServer = null;
            }

            server.SessionClosed -= ServerConnectionClosed;
            server.DataAvailable -= ActiveServerDataAvailable;
        }

        private void ServerConnectionClosed( object sender, EventArgs e )
        {
            Logger.Verbose( "Server connection closed. Removing from dispatcher." );

            IHttpServer server = sender as IHttpServer;

            RemoveServer( server );

            // Let the server fall out of scope
        }
    }
}