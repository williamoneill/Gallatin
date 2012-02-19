using System;
using System.Diagnostics.Contracts;
using Gallatin.Core.Filters;
using Gallatin.Core.Util;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// Interface for HTTP server dispatchers
    /// </summary>
    /// <remarks>
    /// Used to support HTTP pipelining and connection pooling. Only one active server exists at a time although
    /// previous server connections may still be returning data.
    /// </remarks>
    [ContractClass(typeof(ServerDispatcherContract))]
    public interface IServerDispatcher : IPooledObject
    {
        /// <summary>
        /// Connects to the remote host if needed
        /// </summary>
        /// <param name="host">Host to connect to</param>
        /// <param name="port">Host port to connect to</param>
        /// <param name="filter">Response filter that should be applied. May be <c>null</c> if no filter should be applied</param>
        /// <param name="callback">Callback to invoke when the connection is established</param>
        void ConnectToServer(string host, int port, IHttpResponseFilter filter, Action<bool> callback);
        
        /// <summary>
        /// Tries to send data to the active server connection
        /// </summary>
        /// <param name="data">Data to send to the server</param>
        /// <returns><c>True</c> if data was sent to the server</returns>
        bool TrySendDataToActiveServer( byte[] data );
        
        /// <summary>
        /// Raised when data is available from one of the servers in the pool
        /// </summary>
        event EventHandler<DataAvailableEventArgs> ServerDataAvailable;
        
        /// <summary>
        /// Sets to session logger. Useful in writing all log messages under the client session id in the log.
        /// </summary>
        ISessionLogger Logger { set; }

        /// <summary>
        /// Raised when the active server closes a connection
        /// </summary>
        event EventHandler ActiveServerClosedConnection;
    }

    [ContractClassFor(typeof(IServerDispatcher))]
    internal abstract class ServerDispatcherContract : IServerDispatcher
    {
        public abstract void Reset();
        public void ConnectToServer(string host, int port, IHttpResponseFilter filter, Action<bool> callback)
        {
            Contract.Requires(!string.IsNullOrEmpty(host));
            Contract.Requires(port > 0);
            Contract.Requires(callback != null);
        }

        public bool TrySendDataToActiveServer(byte[] data)
        {
            Contract.Requires(data != null);

            return false;
        }

        public abstract event EventHandler<DataAvailableEventArgs> ServerDataAvailable;
        
        public ISessionLogger Logger
        {
            set
            {
                Contract.Requires(value!=null);
            }
        }
        
        public abstract event EventHandler ActiveServerClosedConnection;
    }
}