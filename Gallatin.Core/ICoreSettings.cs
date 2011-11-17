namespace Gallatin.Core
{
    /// <summary>
    /// Interface for proxy service settings
    /// </summary>
    public interface ICoreSettings
    {
        /// <summary>
        /// Proxy server listening port
        /// </summary>
        int ServerPort { get; set; }

        /// <summary>
        /// Maximum number of concurrent clients.
        /// </summary>
        int MaxNumberClients { get; set; }

        /// <summary>
        /// Default receive buffer size
        /// </summary>
        int ReceiveBufferSize { get; set; }

        /// <summary>
        /// Gets and sets the timeout in seconds to wait for a server connection
        /// </summary>
        int ConnectTimeout { get; set; }

        /// <summary>
        /// Gets and sets the IP address the proxy server listens for clients
        /// </summary>
        string ListenAddress { get; set; }

        /// <summary>
        /// Gets and set the maximum length of the pending connections queue 
        /// </summary>
        int ProxyClientListenerBacklog { get; set; }

        /// <summary>
        /// Gets and sets a flag indicating if filtering is applied
        /// </summary>
        bool FilteringEnabled { get; set; }
    }
}