namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for proxy service classes
    /// </summary>
    public interface IProxyService
    {
        /// <summary>
        /// Starts the proxy service
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the proxy service
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets the number of active client sessions
        /// </summary>
        int ActiveClients { get; }
    }
}