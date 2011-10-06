

using System;
using System.Collections.Generic;
using System.Linq;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class SendResponseToClientState : ProxyClientStateBase
    {
        private IHttpResponseMessage _responseMessage;

        public SendResponseToClientState( ProxyClient proxyClient,
                                          IHttpResponseMessage responseMessage )
            : base( proxyClient )
        {
            Log.Info( "Transitioning to SendResponseToClientState" );

            _responseMessage = responseMessage;
            ProxyClient.NetworkService.SendClientMessage( ProxyClient, responseMessage.CreateHttpMessage() );
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            Log.Info( "SendResponseToClientState::HandleSendComplete -- HTTP version {0}", _responseMessage.Version );

            // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html#sec19.6.2
            // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec8.html#sec8.1
            // See RFC 8.1.3 - proxy servers must not establish a HTTP/1.1 persistent connection with 1.0 client. For
            // now, all 1.0 clients will not get persistent connections from the proxy.
            if (_responseMessage.Version == "1.1")
            {
                KeyValuePair<string, string> connectionHeader =
                    _responseMessage.Headers.SingleOrDefault(
                        s =>
                        s.Key.Equals("Connection",
                                        StringComparison.
                                            InvariantCultureIgnoreCase));

                if (!connectionHeader.Equals(default(KeyValuePair<string, string>)) 
                     && !connectionHeader.Value.Equals("close", StringComparison.InvariantCultureIgnoreCase))
                {
                    Log.Info("Maintaining client connection");
                    ProxyClient.State = new ReceiveRequestFromClientState(ProxyClient);
                }
                else
                {
                    Log.Info("Closing client connection");
                    ProxyClient.State = new SessionEndedState(ProxyClient);
                    ProxyClient.NetworkService.EndClientSession(ProxyClient);
                }
            }
            else
            {
                Log.Info("Closing client connection");
                ProxyClient.State = new SessionEndedState(ProxyClient);
                ProxyClient.NetworkService.EndClientSession(ProxyClient);
            }

        }

    }
}