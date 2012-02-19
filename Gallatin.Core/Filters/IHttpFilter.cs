using Gallatin.Contracts;

namespace Gallatin.Core.Filters
{
    /// <summary>
    /// Interface for HTTP filters
    /// </summary>
    public interface IHttpFilter
    {
        /// <summary>
        /// Applies connection filters
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <returns>
        /// The complete HTTP filter response or <c>null</c> if the connection should be allowed
        /// </returns>
        byte[] ApplyConnectionFilters(IHttpRequest request, string connectionId);

        /// <summary>
        /// Creates the HTTP response filter for the particular request
        /// </summary>
        /// <param name="request">
        /// HTTP request
        /// </param>
        /// <param name="connectionId">
        /// Client connection ID
        /// </param>
        /// <returns>
        /// A reference to the connection filter object or <c>null</c> if no filter should be applied
        /// </returns>
        IHttpResponseFilter CreateResponseFilters(IHttpRequest request, string connectionId );
    }
}