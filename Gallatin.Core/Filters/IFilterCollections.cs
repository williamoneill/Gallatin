using System.Collections.Generic;
using Gallatin.Contracts;

namespace Gallatin.Core.Filters
{
    /// <summary>
    /// Interface for classes that contain the filter collections
    /// </summary>
    public interface IFilterCollections
    {
        /// <summary>
        /// Gets the connection filters
        /// </summary>
        IEnumerable<IConnectionFilter> ConnectionFilters { get; }

        /// <summary>
        /// Gets the response filters
        /// </summary>
        IEnumerable<IResponseFilter> ResponseFilters { get; }

        /// <summary>
        /// Gets the whitelist evaluator delegates
        /// </summary>
        IEnumerable<IWhitelistEvaluator> WhitelistEvaluators { get; }
    }
}