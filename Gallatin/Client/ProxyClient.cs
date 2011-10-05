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

namespace Gallatin.Core.Client
{
    internal class ProxyClient : IProxyClient
    {
        private INetworkService _networkService;

        public ProxyClient()
        {
            State = new SessionNotStartedState( this );
        }

        internal IProxyClientState State { get; set; }

        internal INetworkService NetworkService
        {
            get
            {
                return _networkService;
            }
        }

        #region IProxyClient Members

        public void SendComplete()
        {
            State.HandleSendComplete( _networkService );
        }

        public void NewDataAvailable( IEnumerable<byte> data )
        {
            State.HandleNewDataAvailable( _networkService, data );
        }

        public void StartSession( INetworkService networkService )
        {
            if ( networkService == null )
            {
                throw new ArgumentNullException( "networkService" );
            }

            if ( State is SessionNotStartedState )
            {
                _networkService = networkService;
                State = new ReceiveRequestFromClientState( this );
            }
        }

        public void EndSession()
        {
            State = new SessionEndedState( this );
        }

        #endregion
    }
}