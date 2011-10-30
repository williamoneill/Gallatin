using System;
using System.ComponentModel.Composition;

namespace Gallatin.Core
{
    /// <summary>
    /// Default core service settings
    /// </summary>
    public class CoreSettings : ICoreSettings
    {
        #region ICoreSettings Members

        /// <summary>
        /// Gets and sets the network interface binding ordinal
        /// </summary>
        public int NetworkAddressBindingOrdinal { get; set; }

        /// <summary>
        /// Get and sets the proxy service client port
        /// </summary>
        public int ServerPort { get; set; }

        /// <summary>
        /// Gets and set the maximum number of concurrent clients
        /// </summary>
        public int MaxNumberClients { get; set; }

        /// <summary>
        /// Gets and sets the receive buffer size
        /// </summary>
        public int ReceiveBufferSize { get; set; }

        /// <summary>
        /// Gets and sets the monitor thread sleep interval
        /// </summary>
        public int MonitorThreadSleepInterval { get; set; }

        /// <summary>
        /// Gets and sets the proxy session inactivity timeout in seconds
        /// </summary>
        public int SessionInactivityTimeout { get; set; }

        /// <summary>
        /// Gets and sets the timeout in seconds to wait for a server connection
        /// </summary>
        public int ConnectTimeout { get; set; }

        #endregion
    }
}