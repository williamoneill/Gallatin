using System;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// Event argument class used to publish data received from a network connection
    /// </summary>
    public class DataAvailableEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        /// <param name="data">
        /// Raw data from the network endpoint. May not be <c>null</c>.
        /// </param>
        public DataAvailableEventArgs(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            Data = data;
        }

        /// <summary>
        /// Gets the data from the network endpoint.
        /// </summary>
        public byte[] Data { get; private set; }
    }
}