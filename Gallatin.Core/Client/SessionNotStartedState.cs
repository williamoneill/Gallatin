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

    }
}