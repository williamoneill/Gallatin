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

        /// <summary>
        /// Gets the filter speed type. 
        /// </summary>
        FilterSpeedType FilterSpeedType { get; }
    }

    /// <summary>
    /// Interface for classes that evaluate whitelist sessions
    /// </summary>
    public interface IWhitelistEvaluator
    {
        /// <summary>
        /// Determines if the request should be whitelisted for the client
        /// </summary>
        /// <remarks>
        /// When whitelisted, all filters will be skipped
        /// </remarks>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Proxy client connection</param>
        /// <returns><c>true</c> if the connection is whitelisted</returns>
        bool IsWhitlisted( IHttpRequest request, string connectionId );

        /// <summary>
        /// Gets the filter speed type. 
        /// </summary>
        FilterSpeedType FilterSpeedType { get; }
    }
}