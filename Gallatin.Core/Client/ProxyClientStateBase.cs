
using System;
using System.Collections.Generic;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using System.Text;

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
            Log.Warning("Received send notification in unexpected state -- {0}", GetType().ToString());
        }

        public virtual void HandleNewDataAvailableFromServer( INetworkService networkService,
                                                               byte[] data )
        {
            Log.Warning("Received new data from server in unexpected state -- {0}", GetType().ToString());
            Log.Warning( Encoding.UTF8.GetString( data ) );
        }

        public virtual void HandleNewDataAvailableFromClient( INetworkService networkService,
                                                               byte[] data )
        {
            Log.Warning("Received new data from client in unexpected state -- {0}", GetType().ToString());
            Log.Warning(Encoding.UTF8.GetString(data));
        }


        #endregion
    }
}