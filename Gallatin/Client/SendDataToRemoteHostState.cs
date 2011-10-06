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
    internal class SendDataToRemoteHostState : ProxyClientStateBase
    {
        public SendDataToRemoteHostState( ProxyClient proxyClient,
                                          IHttpRequestMessage requestMessage ) : base( proxyClient )
        {
            Log.Info( "Transitioning to SendDataToRemoteHostState" );

            ProxyClient.NetworkService.SendServerMessage( ProxyClient, requestMessage.CreateHttpMessage(), requestMessage.Destination.Host, requestMessage.Destination.Port );
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            ProxyClient.State = new ReceiveResponseFromRemoteHostState( ProxyClient );
        }

        public override void HandleNewDataAvailableFromServer( INetworkService networkService,
                                                     IEnumerable<byte> data )
        {
            throw new InvalidOperationException(
                "Unable to receive data while sending request to remote host" );
        }

        public override void HandleNewDataAvailableFromClient(INetworkService networkService,
                                                     IEnumerable<byte> data)
        {
            throw new InvalidOperationException(
                "Unable to receive data while sending request to remote host");
        }
    }
}