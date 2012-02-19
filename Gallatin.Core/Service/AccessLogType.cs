namespace Gallatin.Core.Service
{
    /// <summary>
    /// Access log type
    /// </summary>
    public enum AccessLogType
    {
        /// <summary>
        /// Client access granted
        /// </summary>
        AccessGranted,

        /// <summary>
        /// Client access blocked
        /// </summary>
        AccessBlocked,

        /// <summary>
        /// HTTPS tunnel established
        /// </summary>
        HttpsTunnel
    }
}