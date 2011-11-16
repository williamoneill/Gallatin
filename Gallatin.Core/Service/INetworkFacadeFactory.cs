using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for factories that generate network facades
    /// </summary>
    [ContractClass( typeof (NetworkFacadeFactoryContract) )]
    public interface INetworkFacadeFactory
    {
        /// <summary>
        /// Begins a connection to the remote host
        /// </summary>
        /// <param name="host">Remote host address</param>
        /// <param name="port">Remote host port</param>
        /// <param name="callback">Delegate to invoke when the connection is complete</param>
        void BeginConnect( string host, int port, Action<bool, INetworkFacade> callback );

        /// <summary>
        /// Listens for new connections on the specified port
        /// </summary>
        /// <param name="address">IP address to bind to. Useful in a multi-homed environment</param>
        /// <param name="port">Port to listen for client connections</param>
        /// <param name="callback">Delegate to invoke when a client connects</param>
        void Listen( string address, int port, Action<INetworkFacade> callback );

        /// <summary>
        /// Stops listening for new client connections
        /// </summary>
        void EndListen();
    }

    [ContractClassFor( typeof (INetworkFacadeFactory) )]
    internal abstract class NetworkFacadeFactoryContract : INetworkFacadeFactory
    {
        #region INetworkFacadeFactory Members

        public void BeginConnect( string host, int port, Action<bool, INetworkFacade> callback )
        {
            Contract.Requires( !string.IsNullOrEmpty( host ) );
            Contract.Requires( port > 0 );
            Contract.Requires( callback != null );
        }

        public void Listen( string address, int port, Action<INetworkFacade> callback )
        {
            Contract.Requires( !string.IsNullOrEmpty(address) );
            Contract.Requires( port > 0 );
            Contract.Requires( callback != null );
        }

        public abstract void EndListen();

        #endregion
    }
}