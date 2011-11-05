using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Gallatin.Contracts;

namespace Gallatin.Filter
{
    /// <summary>
    /// Implements the default blacklist filter
    /// </summary>
    [Export(typeof(IConnectionFilter))]
    public class BlacklistFilter : IConnectionFilter
    {
        /// <summary>
        /// Evaluates the HTTP request to determine if the host or path is in a blacklist
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <returns>HTML error text or <c>null</c> if no filter should be applied</returns>
        public string EvaluateFilter( IHttpRequest request, string connectionId )
        {
            if (request.Headers["host"].EndsWith("playboy.com"))
            {
                return "This URL is banned";
            }

            return null;
        }
    }
}
