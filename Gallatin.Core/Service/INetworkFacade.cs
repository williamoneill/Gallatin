using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for classes that simplify the underlying network
    /// </summary>
    [ContractClass( typeof (NetworkFacadeContract) )]
    public interface INetworkFacade
    {
        /// <summary>
        /// Gets the reference identity
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets a unique identifier for the connection, typically the IP address and port
        /// </summary>
        string ConnectionId { get; }

        /// <summary>
        /// Begins a send operation using the underlying network
        /// </summary>
        /// <param name="buffer">Data to send</param>
        /// <param name="callback">Delegate to invoke when data has been sent</param>
        void BeginSend( byte[] buffer, Action<bool, INetworkFacade> callback );

        /// <summary>
        /// Begins a receive operation using the underlying network
        /// </summary>
        /// <param name="callback">
        /// Delete to invoke when the data becomes available</param>
        void BeginReceive( Action<bool, byte[], INetworkFacade> callback );

        /// <summary>
        /// Begins the operations required to close the underyling network connection
        /// </summary>
        /// <param name="callback">Delegate to invoke when the connection has been closed</param>
        void BeginClose( Action<bool, INetworkFacade> callback );


        /// <summary>
        /// Raised when the connection is closed
        /// </summary>
        event EventHandler ConnectionClosed;
    }

    [ContractClassFor( typeof (INetworkFacade) )]
    internal abstract class NetworkFacadeContract : INetworkFacade
    {
        public abstract DateTime LastActivityTime { get; }

        #region INetworkFacade Members

        public abstract string ConnectionId { get; }

        public void BeginSend( byte[] buffer, Action<bool, INetworkFacade> callback )
        {
            Contract.Requires( buffer != null );
            Contract.Requires( callback != null );
            Contract.Requires( buffer.Length > 0 );
        }

        public void BeginReceive( Action<bool, byte[], INetworkFacade> callback )
        {
            Contract.Requires( callback != null );
        }

        public void BeginClose( Action<bool, INetworkFacade> callback )
        {
            Contract.Requires( callback != null );
        }

        public abstract void CancelPendingReceive();

        public abstract event EventHandler ConnectionClosed;

        public abstract int Id { get; }

        #endregion
    }
}