using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for proxy filter classes that contain all filter collections
    /// </summary>
    [ContractClass( typeof (ProxyFilterContract) )]
    public interface IProxyFilter
    {
        /// <summary>
        /// Gets and sets the outbound connection filters
        /// </summary>
        IEnumerable<IConnectionFilter> ConnectionFilters { get; set; }

        /// <summary>
        /// Gets and sets the response filters
        /// </summary>
        IEnumerable<IResponseFilter> ResponseFilters { get; set; }

        /// <summary>
        /// Gets and sets the whitelist evaluators
        /// </summary>
        IEnumerable<IWhitelistEvaluator> WhitelistEvaluators { get; set; }

        /// <summary>
        /// Evaluates the connection filters before a connection is established. This is not checked again
        /// for persistent connections.
        /// </summary>
        /// <param name="args">HTTP request</param>
        /// <param name="connectionId">Clinet connection ID</param>
        /// <returns><c>null</c> if no filter was applied</returns>
        byte[] EvaluateConnectionFilters( IHttpRequest args, string connectionId );

        /// <summary>
        /// Evaluates the response filters as a response is returned from the server
        /// </summary>
        /// <param name="args">HTTP response</param>
        /// <param name="connectionId">Clinet connection ID</param>
        /// <param name="isBodyRequired"></param>
        byte[] EvaluateResponseFilters( IHttpResponse args, string connectionId, out bool isBodyRequired );

        /// <summary>
        /// Evaluates the response once the HTTP body is available
        /// </summary>
        /// <remarks>
        /// Evaluating the body is an expensive operation and should be done sparingly
        /// </remarks>
        /// <param name="args">HTTP response</param>
        /// <param name="connectionId">Clinet connection ID</param>
        /// <param name="body">HTTP body</param>
        /// <returns><c>null</c> if no filter was applied; otherwise, the filtered response</returns>
        byte[] EvaluateResponseFiltersWithBody( IHttpResponse args, string connectionId, byte[] body );

    }

    [ContractClassFor( typeof (IProxyFilter) )]
    internal abstract class ProxyFilterContract : IProxyFilter
    {
        #region IProxyFilter Members

        public IEnumerable<IConnectionFilter> ConnectionFilters
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                Contract.Requires( value != null );
            }
        }

        public IEnumerable<IResponseFilter> ResponseFilters
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                Contract.Requires( value != null );
            }
        }

        public abstract IEnumerable<IWhitelistEvaluator> WhitelistEvaluators { get; set; }

        public byte[] EvaluateConnectionFilters( IHttpRequest args, string connectionId )
        {
            Contract.Requires(args!=null);
            Contract.Requires(!string.IsNullOrEmpty(connectionId));

            return null;
        }

        public byte[] EvaluateResponseFilters( IHttpResponse args, string connectionId, out bool isBodyRequired )
        {
            Contract.Requires(args != null);
            Contract.Requires(!string.IsNullOrEmpty(connectionId));

            isBodyRequired = false;

            return null;
        }

        public byte[] EvaluateResponseFiltersWithBody( IHttpResponse args, string connectionId, byte[] body )
        {
            Contract.Requires(args != null);
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires(body!=null);

            return null;
        }

        #endregion
    }
}