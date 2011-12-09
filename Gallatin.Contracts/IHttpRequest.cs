namespace Gallatin.Contracts
{
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