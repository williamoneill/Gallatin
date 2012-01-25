using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// Interface for network connection factories
    /// </summary>
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
}
