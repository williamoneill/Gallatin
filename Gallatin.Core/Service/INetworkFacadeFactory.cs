using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for factories that generate network facades
    /// </summary>
    [ContractClass( typeof (INetworkFacadeFactoryContract) )]
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
        /// <param name="hostInterfaceIndex">Host machine network interface ID</param>
        /// <param name="port">Port to listen for client connections</param>
        /// <param name="callback">Delegate to invoke when a client connects</param>
        void Listen( int hostInterfaceIndex, int port, Action<INetworkFacade> callback );

        /// <summary>
        /// Stops listening for new client connections
        /// </summary>
        void EndListen();
    }

    [ContractClassFor( typeof (INetworkFacadeFactory) )]
    internal abstract class INetworkFacadeFactoryContract : INetworkFacadeFactory
    {
        #region INetworkFacadeFactory Members

        public void BeginConnect( string host, int port, Action<bool, INetworkFacade> callback )
        {
            Contract.Requires( !string.IsNullOrEmpty( host ) );
            Contract.Requires( port > 0 );
            Contract.Requires( callback != null );
        }

        public void Listen( int hostInterfaceIndex, int port, Action<INetworkFacade> callback )
        {
            Contract.Requires( hostInterfaceIndex >= 0 );
            Contract.Requires( port > 0 );
            Contract.Requires( callback != null );
        }

        public abstract void EndListen();

        #endregion
    }
}