using System.Collections.Generic;
using System.ComponentModel.Composition;
using Gallatin.Contracts;

namespace Gallatin.Core.Filters
{
    [Export(typeof (IFilterCollections))]
    internal class FilterCollections : IFilterCollections
    {
        [ImportMany]
        public IEnumerable<IConnectionFilter> ConnectionFilters { get; set; }

        [ImportMany]
        public IEnumerable<IResponseFilter> ResponseFilters { get; set; }

        [ImportMany]
        public IEnumerable<IWhitelistEvaluator> WhitelistEvaluators { get; set; }
    }
}