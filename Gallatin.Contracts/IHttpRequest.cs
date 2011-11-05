namespace Gallatin.Contracts
{
    /// <summary>
    /// Interface for HTTP request classes
    /// </summary>
    public interface IHttpRequest
    {
        /// <summary>
        /// Relative path of the remote resource
        /// </summary>
        string Path { get; }

        /// <summary>
        /// HTTP version
        /// </summary>
        string Version { get; }

        /// <summary>
        /// HTTP method
        /// </summary>
        string Method { get; }

        /// <summary>
        /// HTTP headers
        /// </summary>
        IHttpHeaders Headers { get; }

        /// <summary>
        /// Gets the flag indicating if the request uses SSL (HTTPS)
        /// </summary>
        bool IsSsl { get; }

        /// <summary>
        /// Gets the HTTP body
        /// </summary>
        byte[] Body { get; }
    }
}