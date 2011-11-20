using Gallatin.Core.Util;

namespace Gallatin.Core
{
    /// <summary>
    /// Default core service settings
    /// </summary>
    public class CoreSettings : ICoreSettings
    {
        #region ICoreSettings Members

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
        /// Gets and sets the timeout in seconds to wait for a server connection
        /// </summary>
        public int ConnectTimeout { get; set; }

        /// <summary>
        /// Gets and sets the IP address the proxy server listens for clients
        /// </summary>
        public string ListenAddress
        {
            get; set; 
        }

        /// <summary>
        /// Gets and set the maximum length of the pending connections queue 
        /// </summary>
        public int ProxyClientListenerBacklog { get; set; }

        /// <summary>
        /// Gets and sets a flag indicating if filtering is applied
        /// </summary>
        public bool? FilteringEnabled
        {
            get; set;
        }

        #endregion
    }
}