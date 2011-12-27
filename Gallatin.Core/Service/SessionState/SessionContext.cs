using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.SessionState
{
    [PartCreationPolicy( CreationPolicy.NonShared )]
    [Export( typeof (IProxySession) )]
    internal class SessionContext : ISessionContext
    {
        private readonly object _changeClientConnectionMutex = new object();
        private readonly object _changeServerConnectionMutex = new object();
        private readonly INetworkFacadeFactory _facadeFactory;
        private readonly ISessionStateRegistry _registry;
        private readonly ManualResetEvent _serverConnectingEvent = new ManualResetEvent( false );
        private readonly ReaderWriterLockSlim _stateUpdateLock = new ReaderWriterLockSlim( LockRecursionPolicy.SupportsRecursion );

        private Action<byte[], ISessionContext> _bodyAvailableCallback;
        private ISessionState _state;


        [ImportingConstructor]
        public SessionContext( ISessionStateRegistry registry, INetworkFacadeFactory factory )
        {
            Contract.Requires( registry != null );
            Contract.Requires( factory != null );

            _registry = registry;
            _facadeFactory = factory;

            ChangeState( SessionStateType.Uninitialized );
            Id = Guid.NewGuid().ToString();
            LastNetworkActivity = DateTime.Now;
            HttpPipelineDepth = 0;

            ServiceLog.Logger.Verbose( "Creating session context {0}", Id );
        }

        private string InternalId
        {
            get
            {
                return string.Format( "[{0}  {1}  {2}]",
                                      Id,
                                      ClientConnection == null ? 0 : ClientConnection.Id,
                                      ServerConnection == null ? 0 : ServerConnection.Id );
            }
        }

        private IHttpStreamParser ClientParser { get; set; }

        private IHttpStreamParser ServerParser { get; set; }

        #region ISessionContext Members

        public string Id { get; private set; }

        public event EventHandler SessionEnded;

        public void Start( INetworkFacade connection )
        {
            ServiceLog.Logger.Info( "{0} Starting new client session", InternalId );

            ChangeState( SessionStateType.ClientConnecting );

            // Wire up events for the client
            SetupClientConnection( connection );
        }

        public void Reset()
        {
            ChangeState( SessionStateType.Unconnected );
            HttpPipelineDepth = 0;
        }

        public int Port { get; private set; }

        public bool HasClientBegunShutdown { get; private set; }

        public bool HasServerBegunShutdown { get; private set; }

        public int HttpPipelineDepth { get; private set; }

        public string Host { get; private set; }

        public void CloseClientConnection()
        {
            SetupClientConnection( null );
        }

        public void UnwireClientParserEvents()
        {
            if ( ClientParser != null )
            {
                ClientParser.AdditionalDataRequested -= HandleClientParserAdditionalDataRequested;
                ClientParser.PartialDataAvailable -= HandleClientParserPartialDataAvailable;
                ClientParser.ReadRequestHeaderComplete -= HandleClientParserReadRequestHeaderComplete;
                ClientParser = null;
            }

            if ( ClientConnection != null )
            {
                ClientConnection.ConnectionClosed -= HandleClientConnectionClosed;
            }
        }

        public void UnwireServerParserEvents()
        {
            if ( ServerParser != null )
            {
                ServerParser.AdditionalDataRequested -= HandleServerParserAdditionalDataRequested;
                ServerParser.PartialDataAvailable -= HandleServerParserPartialDataAvailable;
                ServerParser.ReadResponseHeaderComplete -= HandleServerParserReadResponseHeaderComplete;
                ServerParser.MessageReadComplete -= HandleServerParserMessageReadComplete;
                ServerParser = null;
            }

            if ( ServerConnection != null )
            {
                ServerConnection.ConnectionClosed -= HandleServerConnectionConnectionClosed;
            }
        }

        public IHttpRequest RecentRequestHeader { get; private set; }

        public IHttpResponse RecentResponseHeader { get; private set; }

        public INetworkFacade ClientConnection { get; private set; }

        public void SendServerData( byte[] data )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::SendServerData", InternalId );

            // Don't wait here. This allows the states to send the request header first
            // while the HTTP stream parser is blocked with data that should follow the header.
            // This prevents a race condition where the body is sent before the header.

            // Wait for the server connection
            //_serverConnectingEvent.WaitOne();

            lock ( _changeServerConnectionMutex )
            {
                if ( ServerConnection == null )
                {
                    throw new InvalidOperationException( "Cannot send data to server when server connection is closed" );
                }

                ServerConnection.BeginSend( data, DataSent );

                LastNetworkActivity = DateTime.Now;
            }
        }

        public void SendClientData( byte[] data )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::SendClientData", InternalId );

            lock ( _changeClientConnectionMutex )
            {
                if ( ClientConnection == null )
                {
                    throw new InvalidOperationException( "Cannot send data to client when client connection is closed" );
                }

                ClientConnection.BeginSend( data, DataSent );

                LastNetworkActivity = DateTime.Now;
            }
        }

        public INetworkFacade ServerConnection { get; private set; }

        public ISessionState State
        {
            get
            {
                try
                {
                    _stateUpdateLock.EnterReadLock();
                    return _state;
                }
                finally
                {
                    _stateUpdateLock.ExitReadLock();
                }
            }

            private set
            {
                try
                {
                    _stateUpdateLock.EnterWriteLock();
                    _state = value;
                }
                finally
                {
                    _stateUpdateLock.ExitWriteLock();
                }
            }
        }

        public void BeginConnectToRemoteHost( string host, int port )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::ConnectToRemoteHost", InternalId );

            Host = host;
            Port = port;

            _facadeFactory.BeginConnect( host, port, ConnectionEstablished );
        }

        public void CloseServerConnection()
        {
            // Block all messages to the server until the connection is re-established
            _serverConnectingEvent.Reset();

            ServiceLog.Logger.Verbose( "{0} SessionContext::closeServerConnection", InternalId );

            SetupServerConnection( null );
        }

        public void OnSessionEnded()
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::OnSessionEnded", InternalId );

            try
            {
                EventHandler sessionEnded = SessionEnded;
                if ( sessionEnded != null )
                {
                    SessionEnded( this, new EventArgs() );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception when raising Session Ended event", InternalId ), ex );
                ChangeState( SessionStateType.Error );
            }
        }

        public void HttpResponseBodyRequested( Action<byte[], ISessionContext> bodyAvailableCallback )
        {
            _bodyAvailableCallback = bodyAvailableCallback;
            ServerParser.BodyAvailable += HandleServerParserBodyAvailable;
        }

        public void ChangeState( SessionStateType newState )
        {
            ServiceLog.Logger.Verbose( () => string.Format( "{0} Changing state to {1}", InternalId, newState ) );

            _stateUpdateLock.EnterWriteLock();

            try
            {
                if ( State != null )
                {
                    State.TransitionFromState( this );
                }

                State = _registry.GetState( newState );

                State.TransitionToState( this );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception when changing session state", InternalId ), ex );
                ChangeState( SessionStateType.Error );
            }
            finally
            {
                _stateUpdateLock.ExitWriteLock();
            }
        }

        public DateTime LastNetworkActivity
        {
            get; private set;
        }

        #endregion

        private void SetupClientConnection( INetworkFacade clientConnection )
        {
            ServiceLog.Logger.Info( "{0} SessionContext::SetupClientConnection", InternalId );

            try
            {
                lock ( _changeClientConnectionMutex )
                {
                    UnwireClientParserEvents();

                    if ( ClientConnection != null )
                    {
                        ClientConnection.BeginClose(
                            ( s, f ) => ServiceLog.Logger.Info( "{0} Client connection closed", InternalId ) );
                    }

                    ClientConnection = clientConnection;

                    if ( ClientConnection != null )
                    {
                        ClientParser = new HttpStreamParser();
                        ClientParser.AdditionalDataRequested += HandleClientParserAdditionalDataRequested;
                        ClientParser.PartialDataAvailable += HandleClientParserPartialDataAvailable;
                        ClientParser.ReadRequestHeaderComplete += HandleClientParserReadRequestHeaderComplete;
                        ClientConnection.ConnectionClosed += HandleClientConnectionClosed;

                        HasClientBegunShutdown = false;

                        ClientConnection.BeginReceive( HandleClientReceive );
                    }
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception setting up client connection", InternalId ), ex );
                ChangeState( SessionStateType.Error );
            }
        }

        private void SetupServerConnection( INetworkFacade serverConnection )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::SetupServerConnection", InternalId );

            try
            {
                lock ( _changeServerConnectionMutex )
                {
                    UnwireServerParserEvents();

                    if ( ServerConnection != null )
                    {
                        ServerConnection.BeginClose(
                            ( s, f ) => ServiceLog.Logger.Info( "{0} Server connection closed", InternalId ) );
                    }

                    ServerConnection = serverConnection;

                    if ( serverConnection != null )
                    {
                        ServerParser = new HttpStreamParser();
                        ServerParser.AdditionalDataRequested += HandleServerParserAdditionalDataRequested;
                        ServerParser.PartialDataAvailable += HandleServerParserPartialDataAvailable;
                        ServerParser.ReadResponseHeaderComplete += HandleServerParserReadResponseHeaderComplete;
                        ServerParser.MessageReadComplete += HandleServerParserMessageReadComplete;
                        ServerConnection.ConnectionClosed += HandleServerConnectionConnectionClosed;

                        HasServerBegunShutdown = false;

                        ServerConnection.BeginReceive( HandleServerReceive );
                    }
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception setting up server connection", InternalId ), ex );
                ChangeState( SessionStateType.Error );
            }
        }

        private void ConnectionEstablished( bool success, INetworkFacade server )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::ConnectionEstablished", InternalId );

            if ( success )
            {
                SetupServerConnection( server );

                State.ServerConnectionEstablished( this );

                // Free the HTTP parsers to send HTTP body data
                _serverConnectingEvent.Set();
            }
            else
            {
                ServiceLog.Logger.Warning( "{0} Unable to connect to remote host {1} {2}", InternalId, Host, Port );
                ChangeState( SessionStateType.Error );
            }
        }

        private void HandleServerParserMessageReadComplete( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserMessageReadComplete", InternalId );

            HttpPipelineDepth--;

            State.SentFullServerResponseToClient( RecentResponseHeader, this );

        }

        private void HandleServerParserBodyAvailable( object sender, HttpDataEventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Requires( _bodyAvailableCallback != null );
            Contract.Ensures( _bodyAvailableCallback == null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserBodyAvailable", InternalId );

            //lock (_mutex)
            {
                try
                {
                    ServerParser.BodyAvailable -= HandleServerParserBodyAvailable;
                    _bodyAvailableCallback( e.Data, this );
                    _bodyAvailableCallback = null;
                }
                catch ( Exception ex )
                {
                    ServiceLog.Logger.Exception(
                        string.Format( "{0} Unhandled exception when evaluating response body", InternalId ), ex );
                    ChangeState( SessionStateType.Error );
                }
            }
        }

        private void HandleClientReceive( bool success, byte[] data, INetworkFacade client )
        {
            Contract.Requires( client != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientReceive", InternalId );

            try
            {
                if ( success && ClientParser != null )
                {
                    if ( data == null )
                    {
                        ServiceLog.Logger.Info( "{0} Client is still active but has stopped sending data.",
                                                InternalId );
                        HasClientBegunShutdown = true;
                        State.AcknowledgeClientShutdown( this );
                    }
                    else
                    {
                        LastNetworkActivity = DateTime.Now;
                        ClientParser.AppendData( data );
                    }
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception receiving data from client", InternalId ), ex );
                ChangeState( SessionStateType.Error );
            }
        }

        private void HandleServerReceive( bool success, byte[] data, INetworkFacade client )
        {
            Contract.Requires( client != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerReceive", InternalId );

            try
            {
                if ( success && ServerParser != null )
                {
                    if ( data == null )
                    {
                        ServiceLog.Logger.Info(
                            "{0} Server has shutdown the socket and will not be sending more data.", InternalId );
                        HasServerBegunShutdown = true;
                        State.AcknowledgeServerShutdown( this );
                    }
                    else
                    {
                        LastNetworkActivity = DateTime.Now;
                        ServerParser.AppendData( data );
                    }
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception receiving data from server", InternalId ), ex );
                ChangeState( SessionStateType.Error );
            }
        }

        //private bool HasHostChanged( HttpRequestHeaderEventArgs newRequest )
        //{
        //    if ( RecentRequestHeader == null )
        //    {
        //        return false;
        //    }

        //    HttpRequest request = HttpRequest.CreateRequest( newRequest );

        //    int port;
        //    string host;

        //    if ( SessionStateUtils.TryParseAddress( request, out host, out port ) )
        //    {
        //        return ( RecentRequestHeader.IsSsl != request.IsSsl ||
        //                 Host != host || Port != port );
        //    }

        //    throw new InvalidDataException( "Malformed HTTP request" );
        //}

        //private void WaitForEmptyPipeline()
        //{
        //    while ( HttpPipelineDepth > 0 )
        //    {
        //        ServiceLog.Logger.Verbose( "{0} Waiting for pipeline to clear. Pipeline depth: {1}", InternalId, HttpPipelineDepth );
        //        _pipelineUpdateEvent.WaitOne();
        //    }
        //}

        private void HandleClientParserReadRequestHeaderComplete( object sender, HttpRequestHeaderEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientParserReadRequestHeaderComplete", InternalId );

            try
            {
                // Race condition: it's possible that the existing connection is being evaluated for persistence
                // when a new, concurrent, pipelined request is coming from the client. This new request could
                // have a different host/port but we cannot change the active server connection until the existing
                // pipeline is empty.

                //if ( HasHostChanged( e ) )
                //{
                //    ServiceLog.Logger.Info( "{0} Client has changed hosts. Waiting for pipeline to clear before reconnecting...", InternalId );
                //    WaitForEmptyPipeline();
                //    ServiceLog.Logger.Info( "{0} Pipeline cleared. Continuing with request.", InternalId );
                //}

                IHttpRequest request = HttpRequest.CreateRequest( e );

                lock ( _changeClientConnectionMutex )
                {
                    if (ClientConnection != null)
                    {
                        if (State is ConnectedState)
                        {
                            ChangeState(SessionStateType.EvaluateChangingHosts);
                        }

                        State.RequestHeaderAvailable(request, this);
                    }
                }

                HttpPipelineDepth++;

                // Update the recent request header AFTER sending the request header to the state so the state
                // can compare host changes
                RecentRequestHeader = request;
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception when handling request header", InternalId ), ex );
                ChangeState( SessionStateType.Error );
            }
        }

        private void HandleClientConnectionClosed( object sender, EventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientConnectionClosed", InternalId );

            Reset();
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant( State != null );
        }

        private void HandleClientParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientParserPartialDataAvailable", InternalId );

            try
            {
                _serverConnectingEvent.WaitOne();

                lock ( _changeServerConnectionMutex )
                {
                    if ( State.ShouldSendPartialDataToServer( e.Data, this )
                         && ServerConnection != null )
                    {
                        SendServerData( e.Data );
                    }
                    else
                    {
                        ServiceLog.Logger.Info( "{0} Skipping sending server data. SERVER NULL: {1}",
                                                InternalId,
                                                ( ServerConnection == null ) );
                    }
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception when processing partial data from client", InternalId ),
                    ex );
                ChangeState( SessionStateType.Error );
            }
        }

        private void HandleClientParserAdditionalDataRequested( object sender, EventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientParserAdditionalDataRequested", InternalId );

            lock ( _changeClientConnectionMutex )
            {
                if ( ClientConnection != null )
                {
                    ClientConnection.BeginReceive( HandleClientReceive );
                }
            }
        }

        private void HandleServerParserReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserReadResponseHeaderComplete", InternalId );

            try
            {
                RecentResponseHeader = HttpResponse.CreateResponse( e );
                State.ResponseHeaderAvailable( RecentResponseHeader, this );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception when handling response header", InternalId ), ex );
                Reset();
            }
        }

        private void HandleServerConnectionConnectionClosed( object sender, EventArgs e )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerConnectionConnectionClosed", InternalId );

            Reset();
        }

        private void HandleServerParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserPartialDataAvailable", InternalId );

            try
            {
                lock ( _changeClientConnectionMutex )
                {
                    if ( State.ShouldSendPartialDataToClient( e.Data, this )
                         && ClientConnection != null )
                    {
                        SendClientData( e.Data );
                    }
                    else
                    {
                        ServiceLog.Logger.Info( "{0} Skipping sending client data. CLIENT NULL: {1}",
                                                InternalId,
                                                ( ClientConnection == null ) );
                    }
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception when processing partial data from server", InternalId ),
                    ex );
                ChangeState( SessionStateType.Error );
            }
        }


        private void DataSent( bool success, INetworkFacade facade )
        {
            Contract.Requires( facade != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::DataSent", InternalId );

            if ( !success )
            {
                Reset();
            }
        }

        private void HandleServerParserAdditionalDataRequested( object sender, EventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserAdditionalDataRequested", InternalId );

            lock ( _changeServerConnectionMutex )
            {
                if ( ServerConnection != null )
                {
                    ServerConnection.BeginReceive( HandleServerReceive );
                }
            }
        }
    }
}