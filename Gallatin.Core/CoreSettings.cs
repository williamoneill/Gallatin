using Gallatin.Core.Util;

namespace Gallatin.Core
{
    /// <summary>
    /// Default core service settings
    /// </summary>
    public class CoreSettings : ICoreSettings
    {
        /// <summary>
        /// Gets the default settings instance. This instance should not
        /// be cached so it can be refreshed without restarting the proxy.
        /// </summary>
        public static ICoreSettings Instance
        {
            get
            {
                return CoreFactory.Compose<ICoreSettings>();
            }
        }

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

        /// <summary>
        /// Gets and sets the name of the localhost
        /// </summary>
        public string LocalHostDnsEntry { get; set; }

        /// <summary>
        /// Gets and set the maximum length of the pending connections queue 
        /// </summary>
        public int ProxyClientListenerBacklog { get; set; }

        #endregion
    }
}