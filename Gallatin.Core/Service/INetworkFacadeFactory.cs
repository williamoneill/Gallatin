using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Service
{
    [ContractClass(typeof(INetworkFacadeFactoryContract))]
    public interface INetworkFacadeFactory
    {
        void BeginConnect( string host, int port, Action<bool,INetworkFacade> callback );
        void Listen(int hostInterfaceIndex, int port, Action<INetworkFacade> callback);
    }

    [ContractClassFor(typeof(INetworkFacadeFactory))]
    abstract class INetworkFacadeFactoryContract : INetworkFacadeFactory
    {
        public void BeginConnect( string host, int port, Action<bool, INetworkFacade> callback )
        {
            Contract.Requires(!string.IsNullOrEmpty(host));
            Contract.Requires(port > 0);
            Contract.Requires(callback != null);

        }

        public void Listen( int hostInterfaceIndex, int port, Action<INetworkFacade> callback )
        {
            Contract.Requires(hostInterfaceIndex >= 0);
            Contract.Requires(port > 0);
            Contract.Requires(callback != null);
        }
    }
}