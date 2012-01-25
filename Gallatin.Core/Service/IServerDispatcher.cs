using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    [ContractClass(typeof(ServerDispatcherContract))]
    internal interface IServerDispatcher : IPooledObject
    {
        void BeginConnect( IHttpRequest requestHeader, Action<bool, IHttpRequest> serverConnectedCallback );
        void SendServerData( byte[] data, Action<bool> sendDataCallback );
        
        // TODO: this session id thing is stupid. There needs to be a logging facility that handles this.
        string SessionId { set; }
        int PipeLineDepth { get; }

        event EventHandler<HttpResponseHeaderEventArgs> ReadResponseHeaderComplete;
        event EventHandler<HttpDataEventArgs> PartialDataAvailable;
        event EventHandler FatalErrorOccurred;

        // TODO: unsure if this is needed
        event EventHandler AllServersInactive;
        event EventHandler EmptyPipeline;
    }

    [ContractClassFor(typeof(IServerDispatcher))]
    internal abstract class ServerDispatcherContract : IServerDispatcher
    {
        public abstract void Reset();
        public void BeginConnect( IHttpRequest requestHeader, Action<bool, IHttpRequest> serverConnectedCallback )
        {
            Contract.Requires(requestHeader!=null);
            Contract.Requires(serverConnectedCallback!=null);
        }
        public void SendServerData( byte[] data, Action<bool> sendDataCallback )
        {
            Contract.Requires(data!=null);
            Contract.Requires(sendDataCallback!=null);
        }
        public abstract string SessionId { set; }
        public abstract int PipeLineDepth { get; }
        public abstract event EventHandler<HttpResponseHeaderEventArgs> ReadResponseHeaderComplete;
        public abstract event EventHandler<HttpDataEventArgs> PartialDataAvailable;
        public abstract event EventHandler FatalErrorOccurred;
        public abstract event EventHandler AllServersInactive;
        public abstract event EventHandler EmptyPipeline;
    }
}