using System.Collections.Generic;

namespace Gallatin.Core
{
    internal interface IProxyClientState
    {
        void HandleSendComplete( INetworkService networkService );
        void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data );
    }
}