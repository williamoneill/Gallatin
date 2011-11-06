using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;
using Gallatin.Contracts;
using System.Linq;


namespace Gallatin.Core.Service
{
    [Export(typeof(IProxyFilter))]
    internal class ProxyFilter : IProxyFilter
    {
        [ImportMany]
        public IEnumerable<IConnectionFilter> ConnectionFilters { get; set; }

        public string EvaluateConnectionFilters( IHttpRequest args, string connectionId )
        {
            Contract.Requires(args != null);
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires(ConnectionFilters!=null);

            string errorMessage = null;

            foreach (var filter in ConnectionFilters.OrderBy( s => s.FilterSpeedType ))
            {
                var filterText = filter.EvaluateFilter( args, connectionId );

                if (!string.IsNullOrEmpty(filterText))
                {
                    // Fail fast
                    errorMessage = filterText;
                    break;
                }
            }

            if(!string.IsNullOrEmpty(errorMessage))
            {
                string body = string.Format( "<html><head><title>Gallatin Proxy - Connection Rejected</title></head><body>{0}</body></html>",
                                             errorMessage );

                return string.Format( "HTTP/{0} 200 OK\r\nConnection: close\r\nContent length: {1}\r\nContent-Type: text/html\r\n\r\n{2}",
                                      args.Version,
                                      body.Length,
                                      body );
            }

            return null;
        }

    }
}