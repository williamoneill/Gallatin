using System;
using System.Collections.Generic;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class ReceiveResponseFromRemoteHostState : ProxyClientStateBase
    {
        private HttpMessageParser _parser = new HttpMessageParser();

        public ReceiveResponseFromRemoteHostState(ProxyClient proxyClient)
            : base(proxyClient)
        {
            Log.Info("Transitioning to ReceiveResponseFromRemoteHostState");

            ProxyClient.NetworkService.GetDataFromRemoteHost(ProxyClient);
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            throw new InvalidOperationException(
                "Unable to acknowledge sent data while waiting for response from server" );
        }

        public override void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
        {
            var message = _parser.AppendData(data);

            if (message != null)
            {
                IHttpResponseMessage responseMessage = message as IHttpResponseMessage;

                if (responseMessage != null)
                {
                    ProxyClient.State = new SendResponseToClientState(ProxyClient, responseMessage);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Did not receive an HTTP response while awaiting response from remote host");
                }
            }
            else
            {
                ProxyClient.NetworkService.GetDataFromRemoteHost(ProxyClient);
            }
        }
    }
}