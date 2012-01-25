using System;
using System.Threading;

namespace Gallatin.Core.Net
{
    internal interface IHttpServer
    {
        event EventHandler SessionClosed;
        event EventHandler<DataAvailableEventArgs> DataAvailable;
        void Send( byte[] data );
        void Close();
        //event EventHandler ReceivedCompleteHttpResponse;
    }
}