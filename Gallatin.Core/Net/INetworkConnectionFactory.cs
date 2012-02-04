using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// Interface for network connection factories
    /// </summary>
    [ContractClass(typeof(NetworkConnectionFactoryContract))]
    public interface INetworkConnectionFactory
    {
        /// <summary>
        /// Begins a connection to the remote host
        /// </summary>
        /// <param name="host">Remote host address</param>
        /// <param name="port">Remote host port</param>
        /// <param name="callback">Callback to invoke when connected</param>
        void BeginConnect(string host, int port, Action<bool, INetworkConnection> callback);

        /// <summary>
        /// Listens for new client connections
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <param name="callback"></param>
        void Listen(string address, int port, Action<INetworkConnection> callback);

        /// <summary>
        /// Stops listening for new client connections
        /// </summary>
        void EndListen();
    }

    [ContractClassFor(typeof(INetworkConnectionFactory))]
    internal abstract class NetworkConnectionFactoryContract : INetworkConnectionFactory
    {
        public void BeginConnect( string host, int port, Action<bool, INetworkConnection> callback )
        {
            Contract.Requires(!string.IsNullOrEmpty(host));
            Contract.Requires(port > 0);
            Contract.Requires(callback!=null);
        }

        public void Listen( string address, int port, Action<INetworkConnection> callback )
        {
            Contract.Requires(!string.IsNullOrEmpty(address));
            Contract.Requires(port > 0);
            Contract.Requires(callback != null);
        }

        public abstract void EndListen();
    }
}
