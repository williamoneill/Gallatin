using System;
using System.Diagnostics.Contracts;
using Gallatin.Core.Net;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for HTTPS tunnels
    /// </summary>
    [ContractClass(typeof(HttpsTunnelContract))]
    public interface IHttpsTunnel
    {
        /// <summary>
        /// Establishes and HTTPS tunnel with the specified host
        /// </summary>
        /// <param name="host">Remote host name</param>
        /// <param name="port">Remote host version</param>
        /// <param name="httpVersion">HTTP version from the client</param>
        /// <param name="client">Reference to the client connection</param>
        void EstablishTunnel(string host, int port, string httpVersion, INetworkConnection client);

        /// <summary>
        /// Raised when the tunnel is closed by either the client or server
        /// </summary>
        event EventHandler TunnelClosed;
    }

    [ContractClassFor(typeof(IHttpsTunnel))]
    internal abstract class HttpsTunnelContract : IHttpsTunnel
    {
        public void EstablishTunnel(string host, int port, string httpVersion, INetworkConnection client)
        {
            Contract.Requires(client != null);
            Contract.Requires(port > 0);
            Contract.Requires(!string.IsNullOrEmpty(host));
            Contract.Requires(!string.IsNullOrEmpty(httpVersion));
        }
        public abstract event EventHandler TunnelClosed;
    }
}
