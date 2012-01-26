namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for proxy services
    /// </summary>
    public interface IProxyService
    {
        /// <summary>
        /// Starts the proxy service
        /// </summary>
        void Start();

        /// <summary>
        /// Ends the proxy service sesison
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets the number of active clients
        /// </summary>
        int ActiveClients { get; }
    }
}