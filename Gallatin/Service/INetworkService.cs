using Gallatin.Core.Client;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    public interface INetworkService
    {
        void SendMessage( IProxyClient client, IHttpRequestMessage message );
        void SendMessage(IProxyClient client, IHttpResponseMessage message);
        void GetDataFromClient(IProxyClient client);
        void GetDataFromRemoteHost(IProxyClient client);
    }

    //public interface IMessageEvaluator
    //{
    //    void EvaluateClientMessage(IHttpRequestMessage request, IProxyServerService proxyServer);
    //    void EvaluateServerMessage(IHttpResponseMessage response, IProxyServerService proxyServer);
    //    IMessageEvaluator Next { get; set; }
    //}

    //public interface IProxyServerService
    //{
    //    void SendClientResponse( IHttpResponseMessage response );
    //    void SendRemoteHostRequest( IHttpRequestMessage request );
    //}
}
