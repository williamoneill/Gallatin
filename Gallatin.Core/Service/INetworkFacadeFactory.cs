using System;

namespace Gallatin.Core.Service
{
    public interface INetworkFacadeFactory
    {
        void BeginConnect( string host, int port, Action<bool,INetworkFacade> callback );
        void Listen(int hostInterfaceIndex, int port, Action<INetworkFacade> callback);
    }
}