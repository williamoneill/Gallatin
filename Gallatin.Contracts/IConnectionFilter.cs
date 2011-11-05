namespace Gallatin.Contracts
{
    /// <summary>
    /// Interface for connection filters that are evaluated before a connection is established with the remote host
    /// </summary>
    public interface IConnectionFilter
    {
        /// <summary>
        /// Evaluates the HTTP request to determine if the request should be filtered
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Proxy client connection ID</param>
        /// <returns><c>null</c> if no filter is applicable or HTML code describing the error</returns>
        string EvaluateFilter( IHttpRequest request, string connectionId );
    }
}