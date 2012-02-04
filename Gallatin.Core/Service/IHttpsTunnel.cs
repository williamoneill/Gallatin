using System;
using System.Collections.Generic;
using System.Linq;
using Gallatin.Core.Util;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// Interface for HTTPS tunnels
    /// </summary>
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
}
