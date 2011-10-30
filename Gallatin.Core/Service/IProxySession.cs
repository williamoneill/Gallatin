using System;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for proxy client sessions
    /// </summary>
    public interface IProxySession
    {
        /// <summary>
        /// Gets the session ID
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Raised when the client session has ended
        /// </summary>
        event EventHandler SessionEnded;

        /// <summary>
        /// Starts the client session
        /// </summary>
        /// <param name="connection">Reference to the client network connection</param>
        void Start( INetworkFacade connection );
    }

    // TODO: place contracts here
}