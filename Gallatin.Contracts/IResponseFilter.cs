using System;

namespace Gallatin.Contracts
{
    /// <summary>
    /// Filter for HTTP responses
    /// </summary>
    public interface IResponseFilter
    {
        /// <summary>
        /// Gets the filter speed type. 
        /// </summary>
        FilterSpeedType FilterSpeedType { get; }

        /// <summary>
        /// Evaluates the HTTP response content
        /// </summary>
        /// <remarks>
        /// This is a potential two-stage filter. Some filters only require the HTTP header to make 
        /// the filtering decision while other filters require the HTTP body. Assembling the HTTP
        /// body is expensive so it must be requested explicitly. The request for the body is made
        /// by returning a callback delegate using the out parameter.
        /// <para>
        /// The parameters to the delegate are the HTTP response, connection ID, HTTP body, and the return
        /// value is the new body value or <c>null</c> if the body was not modified.
        /// </para>
        /// </remarks>
        /// <param name="response">HTTP response</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <param name="bodyAvailableCallback">Delegate to be invoked when the HTTP body is available. Requestion the body has a negative impact on performance</param>
        /// <returns><c>null</c> if no filter is applied or valid HTML describing the filter condition</returns>
        string EvaluateFilter( IHttpResponse response,
                               string connectionId,
                               out Func<IHttpResponse, string, byte[], byte[]> bodyAvailableCallback );
    }
}