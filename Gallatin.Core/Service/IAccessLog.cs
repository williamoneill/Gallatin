using System.Diagnostics.Contracts;
using System.IO;
using Gallatin.Contracts;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// Interface for proxy access log
    /// </summary>
    [ContractClass(typeof(AccessLogContract))]
    public interface IAccessLog
    {
        /// <summary>
        /// Starts the proxy log
        /// </summary>
        /// <param name="logDirectory">Path to the log output directory</param>
        void Start(DirectoryInfo logDirectory);
        
        /// <summary>
        /// Stops the proxy log
        /// </summary>
        void Stop();
        
        /// <summary>
        /// Writes an entry to the proxy log
        /// </summary>
        /// <param name="connectionId">Client connection ID</param>
        /// <param name="request">Original HTTP request</param>
        /// <param name="logType">Log type indicating if the request was blocked</param>
        void Write(string connectionId, IHttpRequest request, AccessLogType logType );
    }

    [ContractClassFor(typeof(IAccessLog))]
    internal abstract class AccessLogContract : IAccessLog
    {
        public void Start(DirectoryInfo logDirectory)
        {
            Contract.Requires(logDirectory != null);
        }
        
        public abstract void Stop();
        
        public void Write(string connectionId, IHttpRequest request, AccessLogType logType)
        {
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires(request != null);
        }
    }
}