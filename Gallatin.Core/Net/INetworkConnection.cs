using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// Interface used to abstract the network connection
    /// </summary>
    public interface INetworkConnection
    {
        /// <summary>
        /// Raised when the connection is closed
        /// </summary>
        event EventHandler ConnectionClosed;
        
        /// <summary>
        /// Raised when the connection is shutdown
        /// </summary>
        event EventHandler Shutdown;
        
        /// <summary>
        /// Raised when data is successfully sent to the endpoint
        /// </summary>
        event EventHandler DataSent;
        
        /// <summary>
        /// Raised when data is available from teh endpoint
        /// </summary>
        event EventHandler<DataAvailableEventArgs> DataAvailable;

        /// <summary>
        /// Gets a unique identity for the connection
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Sets the <see cref="ISessionLogger"/> instance so that communication in the log file shares
        /// the original session ID. Without this, the default logger is used.
        /// </summary>
        ISessionLogger Logger { set; }
        
        /// <summary>
        /// Sends data to the remote endpoint
        /// </summary>
        /// <param name="data"></param>
        void SendData(byte[] data);
        
        /// <summary>
        /// Closes the underlying socket connection
        /// </summary>
        void Close();
        
        /// <summary>
        /// Starts receiving data from the endpoint. This must be called once, and only once, for 
        /// a network connection.
        /// </summary>
        void Start();
    }
}
