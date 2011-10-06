

using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal interface IProxyClientState
    {
        void HandleSendComplete( INetworkService networkService );
        void HandleNewDataAvailableFromServer( INetworkService networkService, byte[] data );
        void HandleNewDataAvailableFromClient( INetworkService networkService, byte[] data );
    }
}