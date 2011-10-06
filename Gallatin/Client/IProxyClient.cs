using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    public interface IProxyClient
    {
        void SendComplete();
        void NewDataAvailableFromServer( IEnumerable<byte> data );
        void NewDataAvailableFromClient(IEnumerable<byte> data);
        void StartSession(INetworkService networkService);
        void EndSession();
    }
}