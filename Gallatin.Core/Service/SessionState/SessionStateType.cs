namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Session state type
    /// </summary>
    public enum SessionStateType
    {
        /// <summary>
        /// Unconnected state
        /// </summary>
        Unconnected,

        /// <summary>
        /// Connecting to server state
        /// </summary>
        ClientConnecting,

        /// <summary>
        /// Client connected to server state
        /// </summary>
        Connected,

        /// <summary>
        /// Response filter applied using HTTP response header
        /// </summary>
        ResponseHeaderFilter,

        /// <summary>
        /// Response filter applied using HTTP response body
        /// </summary>
        ResponseBodyFilter,

        /// <summary>
        /// Client is using HTTPS
        /// </summary>
        Https
    }
}