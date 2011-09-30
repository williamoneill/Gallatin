using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public interface IProxyClient
    {
        void SendComplete();
        void NewDataAvailable( IEnumerable<byte> data );
        void StartSession(INetworkService networkService);
        void EndSession();
    }
}
