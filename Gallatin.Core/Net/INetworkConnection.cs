using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// 
    /// </summary>
    public interface INetworkConnection
    {
        /// <summary>
        /// 
        /// </summary>
        event EventHandler ConnectionClosed;
        
        /// <summary>
        /// 
        /// </summary>
        event EventHandler Shutdown;
        
        /// <summary>
        /// 
        /// </summary>
        event EventHandler DataSent;
        
        /// <summary>
        /// 
        /// </summary>
        event EventHandler<DataAvailableEventArgs> DataAvailable;

        /// <summary>
        /// 
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 
        /// </summary>
        ISessionLogger Logger { set; }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        void SendData(byte[] data);
        
        /// <summary>
        /// 
        /// </summary>
        void Close();
        
        /// <summary>
        /// 
        /// </summary>
        void Start();
    }
}
