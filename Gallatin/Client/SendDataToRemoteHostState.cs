

using System;
using System.Collections.Generic;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class SendDataToRemoteHostState : ProxyClientStateBase
    {
        public SendDataToRemoteHostState( ProxyClient proxyClient,
                                          IHttpRequestMessage requestMessage ) : base( proxyClient )
        {
            string host = requestMessage.Destination.Host;
            int port = requestMessage.Destination.Port;

            // Not the standard port 80? Probably not if we are SSL.
            if (requestMessage.Destination.Port == -1)
            {
                string[] tokens = requestMessage.Destination.AbsoluteUri.Split(':');
                if (tokens.Length == 2)
                {
                    host = tokens[0];
                    port = int.Parse(tokens[1]);
                }
            }

            Log.Info("Transitioning to SendDataToRemoteHostState");

            ProxyClient.NetworkService.SendServerMessage( ProxyClient,
                                                          requestMessage.CreateHttpMessage(),
                                                          host,
                                                          port );
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            ProxyClient.State = new ReceiveResponseFromRemoteHostState( ProxyClient );
        }

    }
}