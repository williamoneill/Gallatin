using System;
using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal class SessionNotStartedState : ProxyClientStateBase
    {
        public SessionNotStartedState( ProxyClient proxyClient )
            : base( proxyClient )
        {
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            throw new InvalidOperationException(
                "Cannot accept sent data when client session has not been started" );
        }

        public override void HandleNewDataAvailableFromServer( INetworkService networkService,
                                                     IEnumerable<byte> data )
        {
            throw new InvalidOperationException(
                "Cannot accept new data when client session has not been started" );
        }
        public override void HandleNewDataAvailableFromClient(INetworkService networkService,
                                                     IEnumerable<byte> data)
        {
            throw new InvalidOperationException(
                "Cannot accept new data when client session has not been started");
        }
    }
}