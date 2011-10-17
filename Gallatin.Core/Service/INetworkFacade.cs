using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace Gallatin.Core.Service
{
    [ContractClass(typeof(INetworkFacadeContract))]
    public interface INetworkFacade
    {
        void BeginSend( byte[] buffer, Action<bool, INetworkFacade> callback );
        void BeginReceive(Action<bool, byte[], INetworkFacade> callback);
        void BeginClose(Action<bool, INetworkFacade> callback);
        DateTime LastActivityTime { get; }
        object Context { get; set; }
    }

    [ContractClassFor(typeof(INetworkFacade))]
    abstract class INetworkFacadeContract : INetworkFacade
    {
        public void BeginSend( byte[] buffer, Action<bool, INetworkFacade> callback )
        {
            Contract.Requires(buffer!=null);
            Contract.Requires(callback!= null);
            Contract.Requires(buffer.Length > 0);
        }

        public void BeginReceive( Action<bool, byte[], INetworkFacade> callback )
        {
            Contract.Requires(callback!=null);
        }

        public void BeginClose( Action<bool, INetworkFacade> callback )
        {
            Contract.Requires(callback!=null);
        }

        public abstract DateTime LastActivityTime { get; }

        public abstract object Context { get; set; }
    }
}
