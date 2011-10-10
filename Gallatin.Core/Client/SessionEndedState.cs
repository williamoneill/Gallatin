

using System;
using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal class SessionEndedState : ProxyClientStateBase
    {
        public SessionEndedState( ProxyClient proxyClient ) : base( proxyClient )
        {
        }

    }
}