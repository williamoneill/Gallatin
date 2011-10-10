using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    public interface IProxyClient
    {
        void ServerSendComplete();
        void ClientSendComplete();
        bool TryCompleteMessageFromServer(byte[] data);
        bool TryCompleteMessageFromClient( byte[] data);
        void StartSession(INetworkService networkService);
    }
}