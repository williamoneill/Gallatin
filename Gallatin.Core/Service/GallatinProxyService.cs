using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    public class GallatinProxyService : IProxyService
    {
        private class ConnectionContext
        {
            public ConnectionContext()
            {
                Contract.Ensures(ClientMessageParser != null);
                Contract.Ensures(ClientMessageParser != null);

                ClientMessageParser = new HttpMessageParser();
                ServerMessageParser = new HttpMessageParser();
            }

            [ContractInvariantMethod]
            private void ObjectInvariant()
            {
                Contract.Invariant(ClientMessageParser != null);
                Contract.Invariant(ServerMessageParser != null);
            }

            public string Host { get; set; }
            public int Port { get; set; }
            public bool IsSsl { get; set; }
            public IHttpMessageParser ClientMessageParser { get; set; }
            public IHttpMessageParser ServerMessageParser { get; set; }

            public INetworkFacade ClientConnection { get; set; }
            public INetworkFacade ServerConnection { get; set; }
        }

        private INetworkFacadeFactory _facadeFactory;
        private ICoreSettings _settings;

        public GallatinProxyService(INetworkFacadeFactory facadeFactory, ICoreSettings settings)
        {
            Contract.Requires(facadeFactory!=null);
            Contract.Requires(settings!=null);
            Contract.Ensures( _settings != null );
            Contract.Ensures(_facadeFactory != null);

            _facadeFactory = facadeFactory;
            _settings = settings;
        }

        private void SendMessageToServer(ConnectionContext sessionContext)
        {
            Contract.Requires(sessionContext != null);

            if(sessionContext.ServerConnection == null || sessionContext.Host)
            {
                
            }
        }

        private void DataReceivedFromClient( bool success, byte[] data, INetworkFacade clientConnection )
        {
            Contract.Requires(data!=null);
            Contract.Requires(clientConnection!=null);

            if(success)
            {
                ConnectionContext context = clientConnection.Context as ConnectionContext;

                IHttpMessage message = context.ClientMessageParser.AppendData( data );
                if(message!=null)
                {
                    // Read full message. Send to server.
                    SendMessageToServer(clientConnection.Context as ConnectionContext);
                }
                else
                {
                    // Not enough data to complete the message. Get more.
                    clientConnection.BeginReceive( DataReceivedFromClient );
                }
            }
        }

        private void ClientConnected(INetworkFacade clientConnection)
        {
            Contract.Requires(clientConnection!=null);

            clientConnection.BeginReceive(DataReceivedFromClient);
        }

        public void Start(int port)
        {
            _facadeFactory.Listen( _settings.NetworkAddressBindingOrdinal, port, ClientConnected );
        }

        public void Stop()
        {
            
        }
    }
}
