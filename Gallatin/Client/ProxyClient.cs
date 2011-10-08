using System;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal interface IProxyClientState
    {
        void ServerSendComplete( ProxyClient context );
        void ClientSendComplete( ProxyClient context );
        void NewDataFromServer( ProxyClient context, byte[] data );
        void NewDataFromClient( ProxyClient context, byte[] data );
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

        public void NewDataAvailableFromServer( byte[] data )
        {
            State.NewDataFromServer( this, data );
        }


        public void NewDataAvailableFromClient( byte[] data )
        {
            State.NewDataFromClient( this, data );
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
            NetworkService.GetDataFromClient( this );
        }

        #endregion
    }
}