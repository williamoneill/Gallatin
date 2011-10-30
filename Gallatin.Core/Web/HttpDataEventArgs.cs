using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Web
{
    internal class HttpDataEventArgs : EventArgs
    {
        public HttpDataEventArgs( byte[] data )
        {
            Contract.Requires( data != null );

            Data = data;
        }

        public byte[] Data { get; private set; }
    }
}