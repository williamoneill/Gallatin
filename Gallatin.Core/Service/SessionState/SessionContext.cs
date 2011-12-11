using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Linq;
using Gallatin.Contracts;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Session state type
    /// </summary>
    public enum SessionStateType
    {
        /// <summary>
        /// Unconnected state
        /// </summary>
        Unconnected,

        /// <summary>
        /// Connecting to server state
        /// </summary>
        ClientConnecting,

        /// <summary>
        /// Client connected to server state
        /// </summary>
        Connected,

        /// <summary>
        /// Response filter applied using HTTP response header
        /// </summary>
        ResponseHeaderFilter,

        /// <summary>
        /// Response filter applied using HTTP response body
        /// </summary>
        ResponseBodyFilter,

        /// <summary>
        /// Client is using HTTPS
        /// </summary>
        Https
    }

    /// <summary>
    /// Interface for proxy client session context
    /// </summary>
    public interface ISessionContext
    {
        /// <summary>
        /// Server port, 0 if unconnected
        /// </summary>
        int Port { get; set; }

        /// <summary>
        /// Remote host name, <c>null</c> if unconnected
        /// </summary>
        string Host { get; set; }

        /// <summary>
        /// Gets the most recent HTTP request header
        /// </summary>
        IHttpRequest LastRequestHeader { get; }

        /// <summary>
        /// Gets the more recent HTTP response header
        /// </summary>
        IHttpResponse LastResponseHeader { get; }

        /// <summary>
        /// Gets  the client connection
        /// </summary>
        INetworkFacade ClientConnection { get; }

        /// <summary>
        /// Gets the server connection
        /// </summary>
        INetworkFacade ServerConnection { get; }

        /// <summary>
        /// Gets a reference to the active client parser
        /// </summary>
        IHttpStreamParser ClientParser { get; }

        /// <summary>
        /// Gets a reference to the active server parser
        /// </summary>
        IHttpStreamParser ServerParser { get; }

        /// <summary>
        /// Gets a reference to the active session state
        /// </summary>
        ISessionState State { get; }

        /// <summary>
        /// Gets the client session identity
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Initializes the context for the new server connection, or releases
        /// the server resources if the connection is <c>null</c>
        /// </summary>
        /// <param name="serverConnection">Server connection</param>
        void SetupServerConnection( INetworkFacade serverConnection );

        /// <summary>
        /// Initializes the context for the new client connection, or releases
        /// the client resources if the connection is <c>null</c>
        /// </summary>
        /// <param name="clientConnection">Server connection</param>
        void SetupClientConnection( INetworkFacade clientConnection );

        /// <summary>
        /// Sends data to the connected server 
        /// </summary>
        /// <param name="data">Data to send</param>
        void SendServerData( byte[] data );

        /// <summary>
        /// Sends data to the connected client
        /// </summary>
        /// <param name="data">Data to send</param>
        void SendClientData( byte[] data );

        /// <summary>
        /// Raised when the current client session has ended
        /// </summary>
        event EventHandler SessionEnded;

        /// <summary>
        /// Starts a new client session
        /// </summary>
        /// <param name="connection">Reference to the client connection</param>
        void Start( INetworkFacade connection );

        /// <summary>
        /// Resets the session to an uninitialized state. Useful in pooling.
        /// </summary>
        void Reset();

        /// <summary>
        /// Raises the Session Ended event 
        /// </summary>
        void OnSessionEnded();

        /// <summary>
        /// Provides a callback method to be invoked when the HTTP response body is available.
        /// </summary>
        /// <remarks>
        /// Building the response body can be an expensive operation. 
        /// </remarks>
        /// <param name="bodyAvailableCallback">
        /// Callback to invoke when the HTTP body is available
        /// </param>
        void HttpResponseBodyRequested( Action<byte[], ISessionContext> bodyAvailableCallback );
    }

    /// <summary>
    /// Session state metadata, used by <see cref="SessionStateRegistry"/>
    /// </summary>
    public interface ISessionStateMetadata
    {
        /// <summary>
        /// Gets the session state type
        /// </summary>
        SessionStateType SessionStateType { get; }
    }

    /// <summary>
    /// Registry that maintains all active session states
    /// </summary>
    public interface ISessionStateRegistry
    {
        /// <summary>
        /// Gets and sets the session states
        /// </summary>
        [ImportMany]
        IEnumerable<Lazy<ISessionState, ISessionStateMetadata>> States { get; set; }

        /// <summary>
        /// Gets the specified state
        /// </summary>
        /// <param name="sessionStateType">Target state type</param>
        /// <returns>Instance for the specified state type</returns>
        ISessionState GetState( SessionStateType sessionStateType );
    }

    /// <summary>
    /// Session state registry, maintains instances of active session states
    /// </summary>
    [Export( typeof (ISessionStateRegistry) )]
    internal class SessionStateRegistry : ISessionStateRegistry
    {
        #region ISessionStateRegistry Members

        [ImportMany]
        public IEnumerable<Lazy<ISessionState, ISessionStateMetadata>> States { get; set; }

        public ISessionState GetState( SessionStateType sessionStateType )
        {
            return States.Where( v => v.Metadata.SessionStateType.Equals( sessionStateType ) ).Single().Value;
        }

        #endregion
    }

    /// <summary>
    /// This class maintains the context for a single client connection. It should
    /// remain as thin as possible.
    /// </summary>
    [PartCreationPolicy( CreationPolicy.NonShared )]
    [Export( typeof (IProxySession) )]
    internal class SessionContext : IProxySession, ISessionContext
    {
        private static readonly ISessionStateRegistry _registry = CoreFactory.Compose<ISessionStateRegistry>();
        private Action<byte[],ISessionContext> _bodyAvailableCallback;

        public SessionContext()
        {
            ChangeState( SessionStateType.Unconnected, this );
            Id = Guid.NewGuid().ToString();

            ServiceLog.Logger.Verbose( "Creating session context {0}", Id );
        }

        private string InternalId
        {
            get
            {
                return string.Format( "[{0}   {1}   {2}]",
                                      Id,
                                      ClientConnection == null ? 0 : ClientConnection.Id,
                                      ServerConnection == null ? 0 : ServerConnection.Id );
            }
        }

        #region IProxySession Members

        public string Id { get; private set; }

        public event EventHandler SessionEnded;

        public void Start( INetworkFacade connection )
        {
            Contract.Requires( connection != null );
            Contract.Requires( State is UnconnectedState );

            ServiceLog.Logger.Info( "{0} Starting new client session", InternalId );

            ChangeState( SessionStateType.ClientConnecting, this );
            SetupClientConnection( connection );
        }

        public void Reset()
        {
            ChangeState( SessionStateType.Unconnected, this );
        }

        #endregion

        #region ISessionContext Members

        public int Port { get; set; }

        public string Host { get; set; }

        public void SetupClientConnection( INetworkFacade clientConnection )
        {
            ServiceLog.Logger.Info( "{0} SessionContext::SetupClientConnection", InternalId );

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
                ClientConnection.BeginClose( ( s, f ) => ServiceLog.Logger.Info( "{0} Client connection closed", InternalId ) );
            }

            ClientConnection = clientConnection;

            if ( ClientConnection != null )
            {
                ClientParser = new HttpStreamParser();
                ClientParser.AdditionalDataRequested += HandleClientParserAdditionalDataRequested;
                ClientParser.PartialDataAvailable += HandleClientParserPartialDataAvailable;
                ClientParser.ReadRequestHeaderComplete += HandleClientParserReadRequestHeaderComplete;
                ClientConnection.ConnectionClosed += HandleClientConnectionClosed;
                ClientConnection.BeginReceive( HandleClientReceive );
            }
        }

        public IHttpRequest LastRequestHeader { get; private set; }

        public IHttpResponse LastResponseHeader { get; private set; }

        public INetworkFacade ClientConnection { get; private set; }

        public void SendServerData( byte[] data )
        {
            Contract.Requires( data != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::SendServerData", InternalId );

            if ( ServerConnection != null )
            {
                ServerConnection.BeginSend( data, DataSent );
            }
        }

        public void SendClientData( byte[] data )
        {
            Contract.Requires( data != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::SendClientData", InternalId );

            if ( ClientConnection != null )
            {
                ClientConnection.BeginSend( data, DataSent );
            }
        }

        public void SetupServerConnection( INetworkFacade serverConnection )
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::SetupServerConnection", InternalId );


            if ( ServerParser != null )
            {
                ServerParser.AdditionalDataRequested -= HandleServerParserAdditionalDataRequested;
                ServerParser.PartialDataAvailable -= HandleServerParserPartialDataAvailable;
                ServerParser.ReadResponseHeaderComplete -= HandleServerParserReadResponseHeaderComplete;
                ServerParser = null;
            }

            if ( ServerConnection != null )
            {
                ServerConnection.ConnectionClosed -= HandleServerConnectionConnectionClosed;
                ServerConnection.BeginClose( ( s, f ) => ServiceLog.Logger.Info( "{0} Server connection closed", InternalId ) );
            }

            ServerConnection = serverConnection;

            if ( serverConnection != null )
            {
                ServerParser = new HttpStreamParser();
                ServerParser.AdditionalDataRequested += HandleServerParserAdditionalDataRequested;
                ServerParser.PartialDataAvailable += HandleServerParserPartialDataAvailable;
                ServerParser.ReadResponseHeaderComplete += HandleServerParserReadResponseHeaderComplete;
                ServerConnection.ConnectionClosed += HandleServerConnectionConnectionClosed;
                ServerConnection.BeginReceive( HandleServerReceive );
            }
        }

        public INetworkFacade ServerConnection { get; private set; }

        public IHttpStreamParser ClientParser { get; private set; }

        public IHttpStreamParser ServerParser { get; private set; }

        public ISessionState State { get; private set; }

        public void OnSessionEnded()
        {
            ServiceLog.Logger.Verbose( "{0} SessionContext::OnSessionEnded", InternalId );

            EventHandler sessionEnded = SessionEnded;
            if ( sessionEnded != null )
            {
                SessionEnded( this, new EventArgs() );
            }
        }

        #endregion

        public void HttpResponseBodyRequested( Action<byte[], ISessionContext> bodyAvailableCallback )
        {
            Contract.Requires( bodyAvailableCallback != null );

            _bodyAvailableCallback = bodyAvailableCallback;
            ServerParser.BodyAvailable += HandleServerParserBodyAvailable;
        }

        private void HandleServerParserBodyAvailable( object sender, HttpDataEventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Requires(_bodyAvailableCallback != null);
            Contract.Ensures( _bodyAvailableCallback == null );

            ServerParser.BodyAvailable -= HandleServerParserBodyAvailable;
            _bodyAvailableCallback( e.Data, this );
            _bodyAvailableCallback = null;
        }

        public static void ChangeState( SessionStateType newState, ISessionContext contex )
        {
            Contract.Requires( contex is SessionContext );

            ServiceLog.Logger.Verbose( () => string.Format( "{0} Changing state to {1}", contex.Id, newState ) );

            SessionContext concreteContext = contex as SessionContext;

            concreteContext.State = _registry.GetState( newState );

            contex.State.TransitionToState( contex );
        }

        private void HandleClientReceive( bool success, byte[] data, INetworkFacade client )
        {
            Contract.Requires( client != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientReceive", InternalId );

            try
            {
                if ( success && ClientParser != null )
                {
                    ServiceLog.Logger.Verbose( "{0} HandleClientReceive -- {1} -- {2}", InternalId, data.Length, success );

                    ClientParser.AppendData( data );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception receiving data from client", InternalId ), ex );
                Reset();
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
                    ServiceLog.Logger.Verbose( "{0} HandleServerReceive -- {1} -- {2}", InternalId, data.Length, success );

                    ServerParser.AppendData( data );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception receiving data from server", InternalId ), ex );
                Reset();
            }
        }

        private void HandleClientParserReadRequestHeaderComplete( object sender, HttpRequestHeaderEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientParserReadRequestHeaderComplete", InternalId );

            try
            {
                LastRequestHeader = HttpRequest.CreateRequest( e );
                State.RequestHeaderAvailable( LastRequestHeader, this );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception when handling request header", InternalId ), ex );
                Reset();
            }
        }

        private void HandleClientConnectionClosed( object sender, EventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Ensures( ClientConnection == null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientConnectionClosed", InternalId );

            ChangeState( SessionStateType.Unconnected, this );

            ClientConnection = null;
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
                if ( State.ShouldSendClientData( e.Data, this ) )
                {
                    SendClientData( e.Data );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception when processing partial data from client", InternalId ), ex );
                Reset();
            }
        }

        private void HandleClientParserAdditionalDataRequested( object sender, EventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Requires( ClientConnection != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleClientParserAdditionalDataRequested", InternalId );
            ClientConnection.BeginReceive( HandleClientReceive );
        }

        private void HandleServerParserReadResponseHeaderComplete( object sender, HttpResponseHeaderEventArgs e )
        {
            Contract.Requires( e != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserReadResponseHeaderComplete", InternalId );

            try
            {
                LastResponseHeader = HttpResponse.CreateResponse( e );
                State.ResponseHeaderAvailable( LastResponseHeader, this );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception when handling response header", InternalId ), ex );
                Reset();
            }
        }

        private void HandleServerConnectionConnectionClosed( object sender, EventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Ensures( ServerConnection == null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerConnectionConnectionClosed", InternalId );

            ChangeState( SessionStateType.Unconnected, this );

            ServerConnection = null;
        }

        private void HandleServerParserPartialDataAvailable( object sender, HttpDataEventArgs e )
        {
            Contract.Requires( e != null );
            Contract.Requires( ServerConnection != null );

            ServiceLog.Logger.Verbose( "{0} SessionContext::HandleServerParserPartialDataAvailable", InternalId );

            try
            {
                if ( State.ShouldSendServerData( e.Data, this )
                     && ServerConnection != null )
                {
                    ServerConnection.BeginSend( e.Data, DataSent );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(
                    string.Format( "{0} Unhandled exception when processing partial data from server", InternalId ), ex );
                Reset();
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