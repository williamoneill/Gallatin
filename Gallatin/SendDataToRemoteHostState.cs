using System;
using System.Collections.Generic;

namespace Gallatin.Core
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