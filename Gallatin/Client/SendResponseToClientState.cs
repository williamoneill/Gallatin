using System;
using System.Collections.Generic;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class SendResponseToClientState : ProxyClientStateBase
    {
        public SendResponseToClientState( ProxyClient proxyClient, IHttpResponseMessage responseMessage ) : base( proxyClient )
        {
            Log.Info("Transitioning to SendResponseToClientState");

            ProxyClient.NetworkService.SendMessage(ProxyClient, responseMessage );
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            ProxyClient.State = new ReceiveRequestFromClientState( ProxyClient );
        }

        public override void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
        {
            throw new InvalidOperationException(
                "Unable to receive data while sending response to client" );
        }
    }
}