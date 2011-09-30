using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
        internal abstract class ProxyClientStateBase : IProxyClientState
        {
            protected ProxyClient ProxyClient { get; private set; }

            protected ProxyClientStateBase(ProxyClient proxyClient)
            {
                ProxyClient = proxyClient;
            }

            public abstract void HandleSendComplete(INetworkService networkService);
            public abstract void HandleNewDataAvailable(INetworkService networkService, IEnumerable<byte> data);
        }

}