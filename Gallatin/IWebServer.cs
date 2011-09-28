using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Gallatin.Core
{
    public interface IHttpClient
    {
        void BeginWebRequest( HttpRequest clientRequest, Action<HttpResponse, ProxyClient> callback, ProxyClient client );
    }
}
