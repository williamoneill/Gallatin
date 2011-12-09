namespace Gallatin.Contracts
{
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
}