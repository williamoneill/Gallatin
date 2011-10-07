using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    public interface IProxyClient
    {
        void ServerSendComplete();
        void ClientSendComplete();
        void NewDataAvailableFromServer(byte[] data);
        void NewDataAvailableFromClient( byte[] data);
        void StartSession(INetworkService networkService);
    }
}