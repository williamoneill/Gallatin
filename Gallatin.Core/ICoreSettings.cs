namespace Gallatin.Core
{

    /// <summary>
    /// Interface for proxy service settings
    /// </summary>
    public interface ICoreSettings
    {
        /// <summary>
        /// Ordinal for the address the server will bind to
        /// </summary>
        int NetworkAddressBindingOrdinal { get; set; }

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
        /// Polling interval for watchdog thread
        /// </summary>
        int MonitorThreadSleepInterval { get; set; }

        /// <summary>
        /// Maximum age in seconds an inactive session remains in memory before it is released.
        /// </summary>
        int SessionInactivityTimeout { get; set; }

        /// <summary>
        /// Gets and sets the timeout in seconds to wait for a server connection
        /// </summary>
        int ConnectTimeout { get; set; }
    }
}