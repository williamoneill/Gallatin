using System.Collections.Generic;
using Gallatin.Contracts;


namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for proxy filter classes that contain all filter collections
    /// </summary>
    public interface IProxyFilter
    {
        /// <summary>
        /// Gets and sets the outbound filters
        /// </summary>
        IEnumerable<IConnectionFilter> ConnectionFilters { get; set; }

        /// <summary>
        /// Evaluates the connection filters before a connection is established
        /// </summary>
        /// <param name="args">HTTP reqeust</param>
        /// <param name="connectionId">Clinet connection ID</param>
        /// <returns><c>null</c> if no filter was applied</returns>
        string EvaluateConnectionFilters(IHttpRequest args, string connectionId);
    }
}