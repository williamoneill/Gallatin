using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// Interface used to abstract the network connection
    /// </summary>
    [ContractClass(typeof(NetworkConnectionContract))]
    public interface INetworkConnection
    {
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
        /// Sends data to the remote endpoint
        /// </summary>
        /// <param name="data"></param>
        void SendData( byte[] data );

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

    [ContractClassFor(typeof(INetworkConnection))]
    internal abstract class NetworkConnectionContract : INetworkConnection
    {
        public abstract string Id { get; }
        public abstract ISessionLogger Logger { set; }
        public abstract event EventHandler ConnectionClosed;
        public abstract event EventHandler Shutdown;
        public abstract event EventHandler DataSent;
        public abstract event EventHandler<DataAvailableEventArgs> DataAvailable;
        public void SendData( byte[] data )
        {
            Contract.Requires(data!=null);
        }
        public abstract void Close();
        public abstract void Start();
    }
}