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

        /// <summary>
        /// Gets the raw network representation of the response
        /// </summary>
        /// <returns>
        /// Raw bytes for the network response
        /// </returns>
        byte[] GetBuffer();
    }
}