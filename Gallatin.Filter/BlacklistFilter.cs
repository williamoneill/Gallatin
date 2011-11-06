using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gallatin.Contracts;

namespace Gallatin.Filter
{
    /// <summary>
    /// Implements the default blacklist filter
    /// </summary>
    [Export(typeof(IConnectionFilter))]
    public class BlacklistFilter : IConnectionFilter
    {
        static Regex _ads = new Regex(@"^ad(\S*)\..*$");

        /// <summary>
        /// Evaluates the HTTP request to determine if the host or path is in a blacklist
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <returns>HTML error text or <c>null</c> if no filter should be applied</returns>
        public string EvaluateFilter( IHttpRequest request, string connectionId )
        {
            string host = request.Headers["host"].ToLower();

            if (!string.IsNullOrEmpty(host))
            {
                if (_ads.Match(host).Success)
                {
                    return string.Format("<div style='background:lightgreen'>Gallatin Proxy - Advertisement blocked to host: {0}</div>", host);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the filter speed type which is local and slow
        /// </summary>
        public FilterSpeedType FilterSpeedType
        {
            get
            {
                return FilterSpeedType.LocalAndSlow;
            }
        }
    }
}
