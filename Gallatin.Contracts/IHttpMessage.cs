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
}