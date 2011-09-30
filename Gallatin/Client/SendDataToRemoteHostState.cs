using System;
using System.Collections.Generic;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal  class SendDataToRemoteHostState : ProxyClientStateBase
    {
        public SendDataToRemoteHostState( ProxyClient proxyClient, IHttpRequestMessage requestMessage ) : base( proxyClient )
        {
            Log.Info("Transitioning to SendDataToRemoteHostState");

            ProxyClient.NetworkService.SendMessage( ProxyClient, requestMessage);
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            ProxyClient.State = new ReceiveResponseFromRemoteHostState(ProxyClient);
        }

        public override void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
        {
            throw new InvalidOperationException(
                "Unable to receive data while sending request to remote host" );
        }
    }
}