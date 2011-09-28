using System;
using System.Net;

namespace Gallatin.Core
{
    public interface IRemoteServerProxy
    {
        void BeginSendRequest( HttpRequest request,
                               Action<WebResponse, ProxyClient> handleWebResponse,
                               ProxyClient state );
    }
}