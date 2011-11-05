using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using Gallatin.Contracts;


namespace Gallatin.Core.Service
{
    [Export(typeof(IProxyFilter))]
    internal class ProxyFilter : IProxyFilter
    {
        [ImportMany]
        public IEnumerable<IConnectionFilter> ConnectionFilters { get; set; }

        public string EvaluateConnectionFilters( IHttpRequest args, string connectionId )
        {
            StringBuilder builder = new StringBuilder();

            foreach (var filter in ConnectionFilters)
            {
                builder.Append( filter.EvaluateFilter( args, connectionId ) );
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

    }
}