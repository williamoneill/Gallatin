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
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class ReceiveResponseFromRemoteHostState : ProxyClientStateBase
    {
        private readonly HttpMessageParser _parser = new HttpMessageParser();

        public ReceiveResponseFromRemoteHostState( ProxyClient proxyClient )
            : base( proxyClient )
        {
            Log.Info( "Transitioning to ReceiveResponseFromRemoteHostState" );

            ProxyClient.NetworkService.GetDataFromRemoteHost( ProxyClient );
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            throw new InvalidOperationException(
                "Unable to acknowledge sent data while waiting for response from server" );
        }

        public override void HandleNewDataAvailable( INetworkService networkService,
                                                     IEnumerable<byte> data )
        {
            IHttpMessage message = _parser.AppendData( data );

            if ( message != null )
            {
                IHttpResponseMessage responseMessage = message as IHttpResponseMessage;

                if ( responseMessage != null )
                {
                    ProxyClient.State = new SendResponseToClientState( ProxyClient, responseMessage );
                }
                else
                {
                    throw new InvalidOperationException(
                        "Did not receive an HTTP response while awaiting response from remote host" );
                }
            }
            else
            {
                ProxyClient.NetworkService.GetDataFromRemoteHost( ProxyClient );
            }
        }
    }
}