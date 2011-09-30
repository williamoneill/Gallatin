using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal interface IProxyClientState
    {
        void HandleSendComplete( INetworkService networkService );
        void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data );
    }
}