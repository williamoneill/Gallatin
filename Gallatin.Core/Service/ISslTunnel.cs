using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for SSL (HTTPS) secure tunnels
    /// </summary>
    [ContractClass(typeof(SslTunnelContract))]
    public interface ISslTunnel
    {
        /// <summary>
        /// Raised when the tunnel is closed by the server or client
        /// </summary>
        event EventHandler TunnelClosed;

        /// <summary>
        /// Establishes a secure tunnel between the two end-points
        /// </summary>
        /// <param name="client">Established client connection</param>
        /// <param name="server">Established server connection</param>
        /// <param name="httpVersion">HTTP version for the connection</param>
        void EstablishTunnel(INetworkFacade client, INetworkFacade server, string httpVersion);
    }

    [ContractClassFor(typeof(ISslTunnel))]
    internal abstract class SslTunnelContract : ISslTunnel
    {
        public abstract event EventHandler TunnelClosed;
        public void EstablishTunnel( INetworkFacade client, INetworkFacade server, string httpVersion )
        {
            Contract.Requires(client != null);
            Contract.Requires(server != null);
            Contract.Requires(!string.IsNullOrEmpty(httpVersion));
            Contract.Requires(httpVersion == "1.0" || httpVersion == "1.1");
        }
    }
}