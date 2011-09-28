using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public class ClientRequestArgs : EventArgs
    {
        public ClientRequestArgs(IHttpRequestMessage requestMessage, IClientSession session)
        {
            RequestMessage = requestMessage;
            ClientSession = session;
        }

        public IHttpRequestMessage RequestMessage { get; private set; }
        public IClientSession ClientSession { get; private set; }
    }

    public class ServerResponseArgs : EventArgs
    {
        public ServerResponseArgs(IHttpResponseMessage responseMessage, IClientSession session)
        {
            ResponseMessage = responseMessage;
            ClientSession = session;
        }

        public IHttpResponseMessage ResponseMessage { get; private set; }
        public IClientSession ClientSession { get; private set; }
    }

    public interface INetworkMessageService
    {
        event EventHandler<ClientRequestArgs> ClientMessagePosted;

        event EventHandler<ServerResponseArgs> ServerResponsePosted;

        void SendServerMessage(IHttpRequestMessage message, IClientSession clientSession);

        void SendClientMessage(IHttpResponseMessage message, IClientSession clientSession);
    }
}
