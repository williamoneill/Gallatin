using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    [ContractClass( typeof (ServerSessionContract) )]
    internal interface IServerSession
    {
        bool HasStoppedSendingData { get; set; }

        bool HasClosed { get; }

        INetworkFacade Connection { get; }

        IHttpResponse LastResponseHeader { get; }
        void Start( INetworkFacade serverConnection );
        void Close();

        event EventHandler<HttpDataEventArgs> PartialDataAvailableForClient;

        event EventHandler<HttpResponseHeaderEventArgs> HttpResponseHeaderAvailable;

        event EventHandler FullResponseReadComplete;
    }

    [ContractClassFor( typeof (IServerSession) )]
    internal abstract class ServerSessionContract : IServerSession
    {
        #region IServerSession Members

        public void Start( INetworkFacade serverConnection )
        {
            Contract.Requires( serverConnection != null );
            Contract.Ensures( Connection == serverConnection );
        }

        public abstract bool HasStoppedSendingData { get; set; }
        public abstract bool HasClosed { get; }
        public abstract void Close();
        public abstract INetworkFacade Connection { get; }
        public abstract IHttpResponse LastResponseHeader { get; }
        public abstract event EventHandler<HttpDataEventArgs> PartialDataAvailableForClient;
        public abstract event EventHandler<HttpResponseHeaderEventArgs> HttpResponseHeaderAvailable;
        public abstract event EventHandler FullResponseReadComplete;

        #endregion
    }
}