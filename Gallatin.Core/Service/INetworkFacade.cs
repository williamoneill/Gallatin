using System;
using System.Collections.Generic;
using System.Text;

namespace Gallatin.Core.Service
{
    public interface INetworkFacade
    {
        void BeginSend( byte[] buffer, Action<bool> callback );
        void BeginReceive( Action<bool, byte[]> callback );
        void BeginClose( Action<bool> callback );
    }
}
