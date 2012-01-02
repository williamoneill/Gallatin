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
        int Port { get; }

        /// <summary>
        /// 
        /// </summary>
        bool HasClientBegunShutdown { get; }

        /// <summary>
        /// 
        /// </summary>
        //bool HasServerBegunShutdown { get; }

        /// <summary>
        /// 
        /// </summary>
        int HttpPipelineDepth { get; }

        /// <summary>
        /// Remote host name, <c>null</c> if unconnected
        /// </summary>
        string Host { get; }

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
        //INetworkFacade ServerConnection { get; }

        /// <summary>
        /// Gets a reference to the active session state
        /// </summary>
        ISessionState State { get; }

        /// <summary>
        /// Connects to the remote host
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        void BeginConnectToRemoteHost(string host, int port);

        /// <summary>
        /// 
        /// </summary>
        //void CloseServerConnection();

        /// <summary>
        /// 
        /// </summary>
        //void CloseClientConnection();

        /// <summary>
        /// Removes the client parser events from the network facade without closing the connection
        /// </summary>
        //void UnwireClientParserEvents();

        /// <summary>
        /// Removes the server parser events from the network facade without closing the connection
        /// </summary>
        //void UnwireServerParserEvents();

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
        //void OnSessionEnded();

        /// <summary>
        /// Provides a callback method to be invoked when the HTTP response body is available.
        /// </summary>
        /// <remarks>
        /// Building the response body can be an expensive operation. 
        /// </remarks>
        /// <param name="bodyAvailableCallback">
        /// Callback to invoke when the HTTP body is available
        /// </param>
        //void HttpResponseBodyRequested( Action<byte[], ISessionContext> bodyAvailableCallback );

        /// <summary>
        /// Changes the internal state of the session context
        /// </summary>
        /// <param name="newState">New state</param>
        void ChangeState( SessionStateType newState );

        /// <summary>
        /// Gets the last time there was any network activity on the context
        /// </summary>
        DateTime LastNetworkActivity { get; }
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

        public abstract bool HasClientBegunShutdown { get; }
        public abstract bool HasServerBegunShutdown { get; }

        public abstract int HttpPipelineDepth
        {
            get;
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
        public void BeginConnectToRemoteHost(string host, int port)
        {
            Contract.Requires(!string.IsNullOrEmpty(host));
            Contract.Requires(port>0);
        }

        public abstract void CloseServerConnection();

        public abstract void CloseClientConnection();

        public abstract string Id { get; }

        public void SetupServerConnection( INetworkFacade serverConnection )
        {
        }

        public void SetupClientConnection( INetworkFacade clientConnection )
        {
        }

        public abstract void UnwireClientParserEvents();

        public abstract void UnwireServerParserEvents();

        public void SendServerData( byte[] data )
        {
            Contract.Requires( data != null );
        }

        public void SendClientData( byte[] data )
        {
            Contract.Requires( data != null );
        }

        public abstract event EventHandler SessionEnded;

        public void Start( INetworkFacade connection )
        {
            Contract.Requires(connection != null);
            Contract.Requires(ClientConnection == null);
            Contract.Requires(ServerConnection == null);
        }

        public abstract void Reset();

        public abstract void OnSessionEnded();

        public void HttpResponseBodyRequested( Action<byte[], ISessionContext> bodyAvailableCallback )
        {
        }

        public abstract void ChangeState( SessionStateType newState );
        public abstract DateTime LastNetworkActivity { get; }

        #endregion
    }
}