using System;
using Gallatin.Core.Util;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// 
    /// </summary>
    public interface IServerDispatcher : IPooledObject
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        /// <param name="callback"></param>
        void ConnectToServer(string host, int port, Action<bool> callback);
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        bool TrySendDataToActiveServer( byte[] data );
        
        /// <summary>
        /// 
        /// </summary>
        event EventHandler<DataAvailableEventArgs> ServerDataAvailable;
        
        /// <summary>
        /// 
        /// </summary>
        ISessionLogger Logger { set; }

        /// <summary>
        /// 
        /// </summary>
        event EventHandler ActiveServerClosedConnection;
    }
}