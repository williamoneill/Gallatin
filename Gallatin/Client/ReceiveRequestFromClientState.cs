using System;
using System.Collections.Generic;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class ReceiveRequestFromClientState : ProxyClientStateBase
    {
        private HttpMessageParser _parser = new HttpMessageParser();

        public ReceiveRequestFromClientState(ProxyClient proxyClient)
            : base(proxyClient)
        {
            Log.Info("Transitioning to ReceiveRequestFromClientState");

            ProxyClient.NetworkService.GetDataFromClient( ProxyClient );
        }

        public override void HandleSendComplete(INetworkService networkService)
        {
            throw new InvalidOperationException(
                "Cannot handle sent data while awaiting request from client" );
        }

        public override void HandleNewDataAvailable(INetworkService networkService, IEnumerable<byte> data)
        {
            var message = _parser.AppendData( data );

            if( message != null )
            {
                IHttpRequestMessage requestMessage = message as IHttpRequestMessage;

                if( requestMessage != null)
                {
                    ProxyClient.State = new SendDataToRemoteHostState(ProxyClient, requestMessage);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Did not receive an HTTP request while awaiting request from client" );
                }
            }
            else
            {
                ProxyClient.NetworkService.GetDataFromClient(ProxyClient);
            }
        }
    }

}