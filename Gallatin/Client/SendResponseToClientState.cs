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
    internal class SendResponseToClientState : ProxyClientStateBase
    {
        public SendResponseToClientState( ProxyClient proxyClient,
                                          IHttpResponseMessage responseMessage )
            : base( proxyClient )
        {
            Log.Info( "Transitioning to SendResponseToClientState" );

            ProxyClient.NetworkService.SendMessage( ProxyClient, responseMessage );
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            Log.Info( "SendResponseToClientState::HandleSendComplete" );

            ProxyClient.State = new ReceiveRequestFromClientState( ProxyClient );
        }

        public override void HandleNewDataAvailable( INetworkService networkService,
                                                     IEnumerable<byte> data )
        {
            Log.Info( "SendResponseToClientState::HandleNewDataAvailable" );

            throw new InvalidOperationException(
                "Unable to receive data while sending response to client" );
        }
    }
}