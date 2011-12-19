using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// This class maintains the context for a single client connection. It should
    /// remain as thin as possible.
    /// </summary>
    [PartCreationPolicy( CreationPolicy.NonShared )]
    [Export( typeof (IProxySession) )]
    internal class SessionContext : ISessionContext
    {
        private readonly ISessionStateRegistry _registry;
        private Action<byte[], ISessionContext> _bodyAvailableCallback;

        [ImportingConstructor]
        public SessionContext( ISessionStateRegistry registry )
        {
            Contract.Requires( registry != null );

            _registry = registry;

            ChangeState( SessionStateType.Unconnected );
            Id = Guid.NewGuid().ToString();

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

        #region ISessionContext Members

        public string Id { get; private set; }

        public event EventHandler SessionEnded;

        public void Start( INetworkFacade connection )
        {
            ServiceLog.Logger.Info( "{0} Starting new client session", InternalId );

            SetupClientConnection(connection);
            ChangeState(SessionStateType.ClientConnecting);
        }

        public void Reset()
        {
            ChangeState( SessionStateType.Unconnected );
        }

        public int Port { get; set; }

        public string Host { get; set; }

        public void SetupClientConnection( INetworkFacade clientConnection )
        {
            lock (_mutex)
            {
                try
                {
                    ServiceLog.Logger.Info("{0} SessionContext::SetupClientConnection", InternalId);

                    UnwireClientParserEvents();

                    if (ClientConnection != null)
                    {
                        ClientConnection.BeginClose((s, f) => ServiceLog.Logger.Info("{0} Client connection closed", InternalId));
                    }

                    ClientConnection = clientConnection;

                    if (ClientConnection != null)
                    {
                        ClientParser = new HttpStreamParser();
                        ClientParser.AdditionalDataRequested += HandleClientParserAdditionalDataRequested;
                        ClientParser.PartialDataAvailable += HandleClientParserPartialDataAvailable;
                        ClientParser.ReadRequestHeaderComplete += HandleClientParserReadRequestHeaderComplete;
                        ClientConnection.ConnectionClosed += HandleClientConnectionClosed;

                        ClientConnection.BeginReceive(HandleClientReceive);
                    }
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception setting up client connection", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
            }
        }

        public void UnwireClientParserEvents()
        {
            lock (_mutex)
            {
                if (ClientParser != null)
                {
                    ClientParser.AdditionalDataRequested -= HandleClientParserAdditionalDataRequested;
                    ClientParser.PartialDataAvailable -= HandleClientParserPartialDataAvailable;
                    ClientParser.ReadRequestHeaderComplete -= HandleClientParserReadRequestHeaderComplete;
                    ClientParser = null;
                }

                if (ClientConnection != null)
                {
                    ClientConnection.ConnectionClosed -= HandleClientConnectionClosed;
                }

            }

        }

        public IHttpRequest RecentRequestHeader { get; private set; }

        public IHttpResponse RecentResponseHeader { get; private set; }

        public INetworkFacade ClientConnection { get; private set; }

        public void SendServerData( byte[] data )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::SendServerData", InternalId );

            lock (_mutex)
            {
                if (ServerConnection == null)
                {
                    throw new InvalidOperationException("Cannot send data to server when server connection is closed");
                }

                ServerConnection.BeginSend(data, DataSent);
            }
        }

        public void SendClientData( byte[] data )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::SendClientData", InternalId );

            if ( ClientConnection == null )
            {
                throw new InvalidOperationException("Cannot send data to client when client connection is closed");
            }

            ClientConnection.BeginSend(data, DataSent);
        }

        private object _mutex = new object();

        public void SetupServerConnection( INetworkFacade serverConnection )
        {
            lock (_mutex)
            {
                try
                {
                    ServiceLog.Logger.Verbose("{0} SessionContext::SetupServerConnection", InternalId);

                    if (ServerParser != null)
                    {
                        ServiceLog.Logger.Info("{0} SessionContext --- Releasing server resources", InternalId);
                        ServerParser.AdditionalDataRequested -= HandleServerParserAdditionalDataRequested;
                        ServerParser.PartialDataAvailable -= HandleServerParserPartialDataAvailable;
                        ServerParser.ReadResponseHeaderComplete -= HandleServerParserReadResponseHeaderComplete;
                        ServerParser.MessageReadComplete -= HandleServerParserMessageReadComplete;
                        ServerParser = null;
                    }

                    if (ServerConnection != null)
                    {
                        ServerConnection.ConnectionClosed -= HandleServerConnectionConnectionClosed;
                        ServerConnection.BeginClose((s, f) => ServiceLog.Logger.Info("{0} Server connection closed", InternalId));
                    }

                    ServerConnection = serverConnection;

                    if (serverConnection != null)
                    {
                        ServerParser = new HttpStreamParser();
                        ServerParser.AdditionalDataRequested += HandleServerParserAdditionalDataRequested;
                        ServerParser.PartialDataAvailable += HandleServerParserPartialDataAvailable;
                        ServerParser.ReadResponseHeaderComplete += HandleServerParserReadResponseHeaderComplete;
                        ServerParser.MessageReadComplete += HandleServerParserMessageReadComplete;
                        ServerConnection.ConnectionClosed += HandleServerConnectionConnectionClosed;
                        ServerConnection.BeginReceive(HandleServerReceive);
                    }
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception setting up server connection", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
            }

        }

        public INetworkFacade ServerConnection { get; private set; }

        public IHttpStreamParser ClientParser { get; private set; }

        public IHttpStreamParser ServerParser { get; private set; }

        public ISessionState State { get; private set; }

        public void OnSessionEnded()
        {
            lock (_mutex)
            {
                try
                {
                    ServiceLog.Logger.Verbose("{0} SessionContext::OnSessionEnded", InternalId);

                    EventHandler sessionEnded = SessionEnded;
                    if (sessionEnded != null)
                    {
                        SessionEnded(this, new EventArgs());
                    }
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception when raising Session Ended event", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
            }
        }

        public void HttpResponseBodyRequested( Action<byte[], ISessionContext> bodyAvailableCallback )
        {
            Contract.Requires(ServerParser != null);

            lock (_mutex)
            {
                _bodyAvailableCallback = bodyAvailableCallback;
                ServerParser.BodyAvailable += HandleServerParserBodyAvailable;
            }
        }

        public void ChangeState( SessionStateType newState )
        {
            lock (_mutex)
            {
                try
                {
                    ServiceLog.Logger.Verbose(() => string.Format("{0} Changing state to {1}", InternalId, newState));

                    State = _registry.GetState(newState);

                    State.TransitionToState(this);
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception when changing session state", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
            }
        }

        #endregion

        private void HandleServerParserMessageReadComplete( object sender, EventArgs e )
        {
            lock (_mutex)
            {
                State.SentFullServerResponseToClient(RecentResponseHeader, this);
            }
        }

        private void HandleServerParserBodyAvailable( object sender, HttpDataEventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Requires( _bodyAvailableCallback != null );
            Contract.Ensures( _bodyAvailableCallback == null );

            lock (_mutex)
            {
                try
                {
                    ServerParser.BodyAvailable -= HandleServerParserBodyAvailable;
                    _bodyAvailableCallback(e.Data, this);
                    _bodyAvailableCallback = null;
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception when evaluating response body", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
            }
        }

        private void HandleClientReceive( bool success, byte[] data, INetworkFacade client )
        {
            Contract.Requires( client != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientReceive", InternalId );

            lock (_mutex)
            {
                try
                {
                    if (success && ClientParser != null)
                    {
                        if (data == null)
                        {
                            ServiceLog.Logger.Info("{0} Client is still active but has stopped sending data.", InternalId);
                            UnwireClientParserEvents();
                        }
                        else
                        {
                            ClientParser.AppendData(data);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception receiving data from client", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
            }
        }

        private void HandleServerReceive( bool success, byte[] data, INetworkFacade client )
        {
            Contract.Requires( client != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerReceive", InternalId );

            lock (_mutex)
            {
                try
                {
                    if (success && ServerParser != null)
                    {
                        if (data == null)
                        {
                            ServiceLog.Logger.Info("{0} Server has shutdown the socket and will not be sending more data.");
                            Reset();
                        }
                        else
                        {
                            ServerParser.AppendData(data);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception receiving data from server", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
            }
        }

        private void HandleClientParserReadRequestHeaderComplete( object sender, HttpRequestHeaderEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientParserReadRequestHeaderComplete", InternalId );

            lock (_mutex)
            {
                try
                {
                    RecentRequestHeader = HttpRequest.CreateRequest(e);
                    State.RequestHeaderAvailable(RecentRequestHeader, this);
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception when handling request header", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
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

            lock (_mutex)
            {
                try
                {
                    if (State.ShouldSendPartialDataToServer(e.Data, this) && ServerConnection != null)
                    {
                        SendServerData(e.Data);
                    }
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(
                        string.Format("{0} Unhandled exception when processing partial data from client", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
            }
        }

        private void HandleClientParserAdditionalDataRequested( object sender, EventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Requires( ClientConnection != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientParserAdditionalDataRequested", InternalId );

            ClientConnection.BeginReceive(HandleClientReceive);
        }

        private void HandleServerParserReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserReadResponseHeaderComplete", InternalId );

            lock (_mutex)
            {
                try
                {
                    RecentResponseHeader = HttpResponse.CreateResponse(e);
                    State.ResponseHeaderAvailable(RecentResponseHeader, this);
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception when handling response header", InternalId), ex);
                    Reset();
                }
            }
        }

        private void HandleServerConnectionConnectionClosed( object sender, EventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Ensures( ServerConnection == null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerConnectionConnectionClosed", InternalId );

            Reset();
        }

        private void HandleServerParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserPartialDataAvailable", InternalId );

            lock (_mutex)
            {
                try
                {
                    if (State.ShouldSendPartialDataToClient(e.Data, this)
                         && ClientConnection != null)
                    {
                        SendClientData(e.Data);
                    }
                }
                catch (Exception ex)
                {
                    ServiceLog.Logger.Exception(
                        string.Format("{0} Unhandled exception when processing partial data from server", InternalId), ex);
                    ChangeState(SessionStateType.Error);
                }
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
            Contract.Requires( ServerConnection != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserAdditionalDataRequested", InternalId );

            ServerConnection.BeginReceive( HandleServerReceive );
        }
    }
}