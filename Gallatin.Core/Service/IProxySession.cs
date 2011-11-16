using System;
using System.Diagnostics.Contracts;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for proxy client sessions
    /// </summary>
    [ContractClass(typeof(ProxySessionContract))]
    public interface IProxySession
    {
        /// <summary>
        /// Gets the session ID
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Raised when the client session has ended
        /// </summary>
        event EventHandler SessionEnded;

        /// <summary>
        /// Starts the client session
        /// </summary>
        /// <param name="connection">Reference to the client network connection</param>
        void Start( INetworkFacade connection );
    }

    [ContractClassFor(typeof(IProxySession))]
    internal abstract class ProxySessionContract : IProxySession
    {
        public abstract string Id
        {
            get;
        }

        public abstract event EventHandler SessionEnded;

        public void Start( INetworkFacade connection )
        {
            Contract.Requires(connection != null);
        }
    }
}