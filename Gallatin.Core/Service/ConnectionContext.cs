using System;
using System.Diagnostics.Contracts;
using Gallatin.Core.Web;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    internal class ConnectionContext : IPooledObject
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

        public string Id
        {
            get
            {
                int client = ClientConnection == null ? 0 : ClientConnection.GetHashCode();
                int server = ServerConnection == null ? 0 : ServerConnection.GetHashCode();

                return string.Format( "[{0}.{1}]", client, server );
            }
        }

        public ServerStream ServerStream { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public bool IsSsl { get; set; }
        public IHttpMessageParser ClientMessageParser { get; set; }
        public IHttpMessageParser ServerMessageParser { get; set; }
        
        public INetworkFacade ClientConnection { get; set; }
        public INetworkFacade ServerConnection { get; set; }
        
        public void Reset()
        {
            ServerStream = null;
            ClientMessageParser.Reset();
            ServerMessageParser.Reset();
            Host = null;
            Port = 0;
            IsSsl = false;
            ClientConnection = null;
            ServerConnection = null;
        }
    }
}