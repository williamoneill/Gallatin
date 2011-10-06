using System;
using System.Collections.Generic;
using Gallatin.Core.Service;

namespace Gallatin.Core.Client
{
    internal class ProxyClient : IProxyClient
    {
        private INetworkService _networkService;

        public ProxyClient()
        {
            State = new SessionNotStartedState( this );
        }

        internal IProxyClientState State { get; set; }

        internal INetworkService NetworkService
        {
            get
            {
                return _networkService;
            }
        }

        #region IProxyClient Members

        public void SendComplete()
        {
            State.HandleSendComplete( _networkService );
        }

        public void NewDataAvailableFromServer( IEnumerable<byte> data )
        {
            State.HandleNewDataAvailableFromServer( _networkService, data );
        }

        public void NewDataAvailableFromClient(IEnumerable<byte> data)
        {
            State.HandleNewDataAvailableFromClient(_networkService, data);
        }

        public void StartSession(INetworkService networkService)
        {
            if ( networkService == null )
            {
                throw new ArgumentNullException( "networkService" );
            }

            if ( State is SessionNotStartedState )
            {
                _networkService = networkService;
                State = new ReceiveRequestFromClientState( this );
            }
            else
            {
                throw new InvalidOperationException( "Session has already been started" );
            }
        }

        public void EndSession()
        {
            State = new SessionEndedState( this );
        }

        #endregion
    }
}