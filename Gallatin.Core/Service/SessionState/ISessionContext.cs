using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Interface for proxy client session context
    /// </summary>
    [ContractClass( typeof (SessionContextContract) )]
    public interface ISessionContext : IProxySession
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
        IHttpRequest RecentRequestHeader { get; }

        /// <summary>
        /// Gets the more recent HTTP response header
        /// </summary>
        IHttpResponse RecentResponseHeader { get; }

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

        /// <summary>
        /// Changes the internal state of the session context
        /// </summary>
        /// <param name="newState">New state</param>
        void ChangeState( SessionStateType newState );
    }

    [ContractClassFor( typeof (ISessionContext) )]
    internal abstract class SessionContextContract : ISessionContext
    {
        #region ISessionContext Members

        public int Port
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                Contract.Requires( value > 0 );
            }
        }

        public string Host
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                Contract.Requires( !string.IsNullOrEmpty( value ) );
            }
        }

        public abstract IHttpRequest RecentRequestHeader { get; }

        public abstract IHttpResponse RecentResponseHeader { get; }

        public abstract INetworkFacade ClientConnection { get; }

        public abstract INetworkFacade ServerConnection { get; }

        public abstract IHttpStreamParser ClientParser { get; }

        public abstract IHttpStreamParser ServerParser { get; }

        public abstract ISessionState State { get; }

        public abstract string Id { get; }

        public void SetupServerConnection( INetworkFacade serverConnection )
        {
        }

        public void SetupClientConnection( INetworkFacade clientConnection )
        {
        }

        public void SendServerData( byte[] data )
        {
            Contract.Requires( data != null );
        }

        public void SendClientData( byte[] data )
        {
            Contract.Requires( data != null );
        }

        public abstract event EventHandler SessionEnded;

        public abstract void Start( INetworkFacade connection );

        public abstract void Reset();

        public abstract void OnSessionEnded();

        public void HttpResponseBodyRequested( Action<byte[], ISessionContext> bodyAvailableCallback )
        {
            Contract.Requires( bodyAvailableCallback != null );
            Contract.Requires( ServerParser != null );
            Contract.Requires( ServerConnection != null );
        }

        public abstract void ChangeState( SessionStateType newState );

        #endregion
    }
}