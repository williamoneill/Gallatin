#region License

// Copyright 2011 Bill O'Neill
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.

#endregion

using System;
using System.Collections.Generic;
using System.Text;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    //internal class SslTunnelActiveState : ProxyClientStateBase
    //{
    //    public SslTunnelActiveState( ProxyClient proxyClient,  ) : base( proxyClient )
    //    {
    //        HttpMessageParser parser = new HttpMessageParser();

    //        parser.AppendData( Encoding.UTF8.GetBytes(string.Format("HTTP/{0} ")) )

    //        ProxyClient.NetworkService.SendMessage(  );
    //    }

    //    public override void HandleSendComplete( INetworkService networkService )
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public override void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    //internal class ConnectToSslServerState : ProxyClientStateBase
    //{
    //    public ConnectToSslServerState( ProxyClient proxyClient, IHttpRequestMessage requestMessage ) : base( proxyClient )
    //    {
    //        Log.Info("Connecting to remote host via SSL");

    //        ProxyClient.NetworkService.SendMessage(ProxyClient, requestMessage);
    //    }

    //    public override void HandleSendComplete( INetworkService networkService )
    //    {
    //        // As per http://curl.haxx.se/rfc/draft-luotonen-web-proxy-tunneling-01.txt
    //        // send a response to the client and then assume tunnel mode
    //    }

    //    public override void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
    //    {
    //        throw new InvalidOperationException(
    //            "Unable to receive data while sending request to remote host");
    //    }
    //}

    internal class ReceiveRequestFromClientState : ProxyClientStateBase
    {
        private readonly HttpMessageParser _parser = new HttpMessageParser();

        public ReceiveRequestFromClientState( ProxyClient proxyClient )
            : base( proxyClient )
        {
            Log.Info( "Transitioning to ReceiveRequestFromClientState" );

            ProxyClient.NetworkService.GetDataFromClient( ProxyClient );
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            throw new InvalidOperationException(
                "Cannot handle sent data while awaiting request from client" );
        }

        public override void HandleNewDataAvailableFromServer(INetworkService networkService, IEnumerable<byte> data)
        {
            throw new InvalidOperationException(
                "Unable to receive data from server in current state" );
        }

        public override void HandleNewDataAvailableFromClient( INetworkService networkService,
                                                     IEnumerable<byte> data )
        {
            IHttpMessage message = _parser.AppendData( data );

            if ( message != null )
            {
                IHttpRequestMessage requestMessage = message as IHttpRequestMessage;

                if ( requestMessage != null )
                {
                    // SSL?
                    if( requestMessage.Method.Equals("Connect", StringComparison.InvariantCultureIgnoreCase) )
                    {
                        // TODO: implement
                        //ProxyClient.State = new ConnectToSslServerState( ProxyClient, requestMessage );
                    }
                    else
                    {
                        ProxyClient.State = new SendDataToRemoteHostState(ProxyClient, requestMessage);
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        "Did not receive an HTTP request while awaiting request from client" );
                }
            }
            else
            {
                ProxyClient.NetworkService.GetDataFromClient( ProxyClient );
            }
        }
    }
}