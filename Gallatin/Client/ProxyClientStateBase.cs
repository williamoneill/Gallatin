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

using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal abstract class ProxyClientStateBase : IProxyClientState
    {
        protected ProxyClientStateBase( ProxyClient proxyClient )
        {
            ProxyClient = proxyClient;
        }

        protected ProxyClient ProxyClient { get; private set; }

        #region IProxyClientState Members

        public abstract void HandleSendComplete( INetworkService networkService );

        public abstract void HandleNewDataAvailable( INetworkService networkService,
                                                     IEnumerable<byte> data );

        #endregion
    }
}