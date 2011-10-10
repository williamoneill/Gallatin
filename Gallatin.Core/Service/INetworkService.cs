
using System.Collections.Generic;
using Gallatin.Core.Client;

namespace Gallatin.Core.Service
{
    public interface INetworkService
    {
        void SendServerMessage(IProxyClient client, byte[] message, string host, int port);
        void SendClientMessage( IProxyClient client, byte[] message );
        //void GetDataFromClient( IProxyClient client );
        //void GetDataFromRemoteHost( IProxyClient client );
        void EndClientSession( IProxyClient client );
    }
}