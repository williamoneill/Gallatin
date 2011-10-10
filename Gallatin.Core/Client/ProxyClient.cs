using System;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal interface IProxyClientState
    {
        void ServerSendComplete( ProxyClient context );
        void ClientSendComplete( ProxyClient context );
        bool TryCompleteMessageFromServer(ProxyClient context, byte[] data);
        bool TryCompleteMessageFromClient(ProxyClient context, byte[] data);
    }

    internal class ProxyClient : IProxyClient
    {
        public ProxyClient()
        {
            State = new DisconnectedState();
        }

        internal string Host { get; set; }
        internal int Port { get; set; }

        public IProxyClientState State { get; set; }
        public INetworkService NetworkService { get; private set; }

        #region IProxyClient Members

        public void ServerSendComplete()
        {
            State.ServerSendComplete( this );
        }

        public void ClientSendComplete()
        {
            State.ClientSendComplete( this );
        }

        public bool TryCompleteMessageFromServer( byte[] data )
        {
            return State.TryCompleteMessageFromServer( this, data );
        }

        public bool TryCompleteMessageFromClient( byte[] data )
        {
            return State.TryCompleteMessageFromClient( this, data );
        }

        public void StartSession( INetworkService networkService )
        {
            if ( networkService == null )
            {
                throw new ArgumentNullException( "networkService" );
            }

            if ( NetworkService != null )
            {
                throw new InvalidOperationException( "Client session already started" );
            }

            State = new DefaultClientState();
            NetworkService = networkService;
        }

        #endregion
    }
}