using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace Gallatin.Core
{
    public class ProxyClient : IProxyClient
    {
        private INetworkService _networkService;

        public ProxyClient()
        {
        }

        public void SendComplete()
        {
            State.HandleSendComplete(_networkService);
        }

        public void NewDataAvailable( IEnumerable<byte> data )
        {
            State.HandleNewDataAvailable(_networkService, data);
        }

        public void StartSession(INetworkService networkService)
        {
            _networkService = networkService;
            State = new ReceiveRequestFromClientState(this);
        }

        public void EndSession()
        {
            // TODO: create a state that does not accept data or send data
            State = null;
        }

        internal IProxyClientState State
        {
            get; set;
        }

        internal INetworkService NetworkService 
        { 
            get
        {
            return _networkService;
        } 
        }
    }


}