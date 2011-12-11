using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// HTTP new data available event argument class
    /// </summary>
    public class HttpDataEventArgs : EventArgs
    {
        /// <summary>
        /// Create default instance of the class
        /// </summary>
        /// <param name="data">Newly received HTTP data</param>
        public HttpDataEventArgs( byte[] data )
        {
            Contract.Requires( data != null );

            Data = data;
        }

        /// <summary>
        /// Gets the newly received HTTP data
        /// </summary>
        public byte[] Data { get; private set; }
    }
}