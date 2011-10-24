namespace Gallatin.Core
{
    // TODO: define default constraints

    public interface ICoreSettings
    {
        /// <summary>
        /// 	Ordinal for the address the server will bind to
        /// </summary>
        int NetworkAddressBindingOrdinal { get; set; }

        /// <summary>
        /// 	Proxy server listening port
        /// </summary>
        int ServerPort { get; set; }

        /// <summary>
        /// 	Maximum number of concurrent clients.
        /// </summary>
        int MaxNumberClients { get; set; }

        /// <summary>
        /// 	Default receive buffer size
        /// </summary>
        int ReceiveBufferSize { get; set; }

        /// <summary>
        /// 	Polling interval for watchdog thread
        /// </summary>
        int WatchdogThreadSleepInterval { get; set; }

        /// <summary>
        /// 	Maximum age in seconds an inactive session remains in memory before it is released.
        /// </summary>
        int SessionInactivityTimeout { get; set; }
    }
}