using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Filters
{
    /// <summary>
    /// Interface for HTTP response filters
    /// </summary>
    [ContractClass(typeof(HttpResponseFilterContract))]
    public interface IHttpResponseFilter
    {
        /// <summary>
        /// Applies the filters using the HTTP response header 
        /// </summary>
        /// <remarks>
        /// Capturing the HTTP body negatively affects performance. If the filter determination can be made using just
        /// the header then the body filters should be circumvented.
        /// </remarks>
        /// <param name="response">HTTP response header</param>
        /// <param name="bodyCallbacks">Callbacks that should be invoked when the body is available</param>
        /// <returns>A filter response that may be returned directly to the client</returns>
        byte[] ApplyResponseHeaderFilters(IHttpResponse response, out IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> bodyCallbacks );


        /// <summary>
        /// Applies the HTTP body filters to the HTTP body
        /// </summary>
        /// <param name="response">HTTP response header</param>
        /// <param name="body">Complete HTTP response body</param>
        /// <param name="bodyCallbacks">HTTP body filter callbacks</param>
        /// <returns>Response that should be returned to the client regardless if any of the filters were executed</returns>
        byte[] ApplyResponseBodyFilter(IHttpResponse response, byte[] body, IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> bodyCallbacks);
    }

    [ContractClassFor(typeof(IHttpResponseFilter))]
    internal abstract class HttpResponseFilterContract : IHttpResponseFilter
    {
        public byte[] ApplyResponseHeaderFilters(IHttpResponse response, out IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> bodyCallbacks)
        {
            Contract.Requires(response != null);

            bodyCallbacks = null;

            return null;
        }

        public byte[] ApplyResponseBodyFilter(IHttpResponse response, byte[] body, IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> bodyCallbacks)
        {
            Contract.Requires(response != null);
            Contract.Requires(bodyCallbacks != null);

            return null;
        }
    }
}