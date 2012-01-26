using System;
using System.Collections.Generic;
using System.Linq;
using Gallatin.Core.Util;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// 
    /// </summary>
    public interface IHttpsTunnel
    {
        /// <summary>
        /// Establishes and HTTPS tunnel with the specified host
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="httpVersion"></param>
        /// <param name="client"></param>
        void EstablishTunnel(string host, int port, string httpVersion, INetworkConnection client);

        /// <summary>
        /// 
        /// </summary>
        event EventHandler TunnelClosed;
    }
}
