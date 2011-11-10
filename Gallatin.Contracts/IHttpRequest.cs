namespace Gallatin.Contracts
{
    /// <summary>
    /// Interface for HTTP message classes
    /// </summary>
    public interface IHttpMessage
    {
        /// <summary>
        /// HTTP version
        /// </summary>
        string Version { get; }

        /// <summary>
        /// HTTP headers
        /// </summary>
        IHttpHeaders Headers { get; }

        /// <summary>
        /// Gets a flag indicating if the message will have an HTTP body
        /// </summary>
        bool HasBody { get; }
    }

    /// <summary>
    /// Interface for HTTP response classes
    /// </summary>
    public interface IHttpResponse : IHttpMessage
    {
        /// <summary>
        /// HTTP status from the server
        /// </summary>
        int Status { get; }

        /// <summary>
        /// HTTP status text from the server
        /// </summary>
        string StatusText { get; }
    }

    /// <summary>
    /// Interface for HTTP request classes
    /// </summary>
    public interface IHttpRequest : IHttpMessage
    {
        /// <summary>
        /// Relative path of the remote resource
        /// </summary>
        string Path { get; set; }


        /// <summary>
        /// HTTP method
        /// </summary>
        string Method { get; }


        /// <summary>
        /// Gets the flag indicating if the request uses SSL (HTTPS)
        /// </summary>
        bool IsSsl { get; }

    }
}