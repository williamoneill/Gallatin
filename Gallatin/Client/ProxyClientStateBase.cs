
using System;
using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal abstract class ProxyClientStateBase : IProxyClientState
    {
        protected ProxyClientStateBase( ProxyClient proxyClient )
        {
            ProxyClient = proxyClient;
        }

        protected ProxyClient ProxyClient { get; private set; }

        #region IProxyClientState Members

        public virtual void HandleSendComplete( INetworkService networkService )
        {
            throw new InvalidOperationException(
                "Unable to accept data send ack while in current state");
            
        }

        public virtual void HandleNewDataAvailableFromServer( INetworkService networkService,
                                                               byte[] data )
        {
            throw new InvalidOperationException(
                "Unable to receive data while in current state");
            
        }

        public virtual void HandleNewDataAvailableFromClient( INetworkService networkService,
                                                               byte[] data )
        {
            throw new InvalidOperationException( 
                "Unable to receive data while in current state");
            
        }


        #endregion
    }
}