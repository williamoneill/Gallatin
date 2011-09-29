using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Gallatin.Core
{
    public interface IProxyClient
    {
        void SendComplete(INetworkService networkService);
        void NewDataAvailable( INetworkService networkService, IEnumerable<byte> data );
    }

    internal interface IProxyClientState
    {
        void HandleSendComplete( INetworkService networkService );
        void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data );
    }

    internal interface IProxyClientContext
    {
        IProxyClientState State { get; set; }
    }

    internal abstract class ProxyClientStateBase : IProxyClientState
    {
        protected ProxyClient ProxyClient { get; private set; }

        protected ProxyClientStateBase( ProxyClient proxyClient )
        {
            ProxyClient = proxyClient;
        }

        public abstract void HandleSendComplete(INetworkService networkService);
        public abstract void HandleNewDataAvailable(INetworkService networkService, IEnumerable<byte> data);
    }

    internal class ReceiveRequestFromClientState : ProxyClientStateBase
    {
        private HttpMessageParser _parser = new HttpMessageParser();

        public ReceiveRequestFromClientState(ProxyClient proxyClient)
            : base(proxyClient)
        {
        }

        public override void HandleSendComplete(INetworkService networkService)
        {
            throw new InvalidOperationException(
                "Cannot handle sent data while awaiting request from client" );
        }

        public override void HandleNewDataAvailable(INetworkService networkService, IEnumerable<byte> data)
        {
            var message = _parser.AppendData( data );

            if( message != null )
            {
                IHttpRequestMessage requestMessage = message as IHttpRequestMessage;

                if( requestMessage != null)
                {
                    ProxyClient.State = new SendDataToRemoteHostState(ProxyClient);
                    networkService.SendMessage( base.ProxyClient, requestMessage );
                }
                else
                {
                    throw new InvalidOperationException(
                        "Did not receive an HTTP request while awaiting request from client" );
                }
            }
        }
    }

    internal  class SendDataToRemoteHostState : ProxyClientStateBase
    {
        public SendDataToRemoteHostState( ProxyClient proxyClient ) : base( proxyClient )
        {
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            ProxyClient.State = new ReceiveResponseFromRemoteHostState(ProxyClient);
        }

        public override void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
        {
            throw new InvalidOperationException(
                "Unable to receive data while sending request to remote host" );
        }
    }

    internal class ReceiveResponseFromRemoteHostState : ProxyClientStateBase
    {
        private HttpMessageParser _parser = new HttpMessageParser();

        public ReceiveResponseFromRemoteHostState(ProxyClient proxyClient)
            : base(proxyClient)
        {
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            throw new InvalidOperationException(
                "Unable to acknowledge sent data while waiting for response from server" );
        }

        public override void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
        {
            var message = _parser.AppendData(data);

            if (message != null)
            {
                IHttpResponseMessage responseMessage = message as IHttpResponseMessage;

                if (responseMessage != null)
                {
                    ProxyClient.State = new SendResponseToClientState(ProxyClient);
                    networkService.SendMessage(base.ProxyClient, responseMessage);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Did not receive an HTTP response while awaiting response from remote host");
                }
            }
            
        }
    }

    internal class SendResponseToClientState : ProxyClientStateBase
    {
        public SendResponseToClientState( ProxyClient proxyClient ) : base( proxyClient )
        {
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
            ProxyClient.State = new ReceiveRequestFromClientState( ProxyClient );
        }

        public override void HandleNewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
        {
            throw new InvalidOperationException(
                "Unable to receive data while sending response to client" );
        }
    }

    public class ProxyClient : IProxyClient
    {
        public ProxyClient()
        {
            State = new ReceiveRequestFromClientState(this);
        }

        public void SendComplete(INetworkService networkService)
        {
            State.HandleSendComplete(networkService);
        }

        public void NewDataAvailable( INetworkService networkService, IEnumerable<byte> data )
        {
            State.HandleNewDataAvailable(networkService, data);
        }

        internal IProxyClientState State
        {
            get; set;
        }

    }

    public interface INetworkService
    {
        void SendMessage( IProxyClient client, IHttpRequestMessage message );
        void SendMessage(IProxyClient client, IHttpResponseMessage message);
        void GetDataFromClient(IProxyClient client);
        void GetDataFromRemoteHost(IProxyClient client);
    }

    //public interface IMessageEvaluator
    //{
    //    void EvaluateClientMessage(IHttpRequestMessage request, IProxyServerService proxyServer);
    //    void EvaluateServerMessage(IHttpResponseMessage response, IProxyServerService proxyServer);
    //    IMessageEvaluator Next { get; set; }
    //}

    //public interface IProxyServerService
    //{
    //    void SendClientResponse( IHttpResponseMessage response );
    //    void SendRemoteHostRequest( IHttpRequestMessage request );
    //}

    public class ProxyServer2 : INetworkService
    {
        private Socket _serverSocket;
        private Dictionary<IProxyClient,Session> _activeSessions = new Dictionary<IProxyClient,Session>();

        private class Session
        {
            public Session(Socket clientSocket, IProxyClient proxyClient )
            {
                ProxyClient = proxyClient;
                ClientSocket = clientSocket;
                Buffer = new byte[BufferSize];
                Id = Guid.NewGuid();
            }

            public Guid Id { get; private set; }
            public const int BufferSize = 8192;
            public Socket ClientSocket { get; set; }
            public Socket ServerSocket { get; set; }
            public byte[] Buffer { get; set; }
            public IProxyClient ProxyClient { get; set; }
            public IHttpMessage Message { get; set; }

            public void ResetBufferForReceive()
            {
                Buffer = new byte[BufferSize];
            }

            public void EndSession(bool inError)
            {
                Log.Info("{0} Ending client connection. Error: {1} ", Id, inError);

                if (ClientSocket != null)
                {
                    if (ClientSocket.Connected)
                    {
                        if (inError)
                        {
                            ClientSocket.Send(Encoding.UTF8.GetBytes("HTTP/1.0 500 Internal Server Error\r\nContent-Length: 11\r\n\r\nProxy error"));
                        }

                        ClientSocket.Shutdown(SocketShutdown.Both);
                    }

                    ClientSocket.Close();
                    ClientSocket.Dispose();
                }

                if (ServerSocket != null)
                {
                    if (ServerSocket.Connected)
                        ServerSocket.Shutdown(SocketShutdown.Both);

                    ServerSocket.Close();
                    ServerSocket.Dispose();
                }
            }
        }

        public void Start(int port)
        {
            if (_serverSocket == null)
            {
                _serverSocket = new Socket( AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp );

                IPAddress hostAddress =
                    ( Dns.Resolve( IPAddress.Any.ToString() ) ).AddressList[0];
                IPEndPoint endPoint = new IPEndPoint(hostAddress, port);

                _serverSocket.Bind( endPoint );

                _serverSocket.Listen( 30 );

                _serverSocket.BeginAccept( HandleNewConnect, null );
            }
        }

        private void HandleNewConnect(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = _serverSocket.EndAccept(ar);

                // Immediately listen for the next clientSession
                _serverSocket.BeginAccept(HandleNewConnect, null);

                ProxyClient proxyClient = new ProxyClient();

                this._activeSessions.Add( proxyClient, new Session(clientSocket, proxyClient) );
            }
            catch (Exception ex)
            {
                Log.Exception("Unable to service new clientSession connection.", ex);
            }
        }

        private void HandleDataFromClient(IAsyncResult ar)
        {
            Session session = ar.AsyncState as Session;

            try
            {
                int bytesReceived = session.ClientSocket.EndReceive( ar );
                session.ProxyClient.NewDataAvailable( this, session.Buffer.Take( bytesReceived ) );
            }
            catch ( Exception ex )
            {
                session.EndSession(true);
                Log.Exception( session.Id + " An error was encountered when receiving data from the client.", ex);
            }
        }

        private void HandleDataFromRemoteHost(IAsyncResult ar)
        {
            Session session = ar.AsyncState as Session;

            try
            {
                int bytesReceived = session.ServerSocket.EndReceive(ar);
                session.ProxyClient.NewDataAvailable(this, session.Buffer.Take(bytesReceived));
            }
            catch (Exception ex)
            {
                session.EndSession(true);
                Log.Exception(session.Id + " An error was encountered when receiving data from the remote host.", ex);
            }
        }

        private void HandleSendToClient(IAsyncResult ar)
        {
            Session session = ar.AsyncState as Session;

            try
            {
                SocketError socketError;

                session.ClientSocket.EndSend(ar, out socketError);

                if(socketError != SocketError.Success)
                {
                    session.EndSession(true);
                    Log.Error( "{0} Unable to send message to client: {1}", session.Id, socketError );
                }
                else
                {
                    IHttpResponseMessage responseMessage = session.Message as IHttpResponseMessage;

                    if(responseMessage != null)
                    {
                        // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html#sec19.6.2
                        // See RFC 8.1.3 - proxy servers must not establish a HTTP/1.1 persistent connection with 1.0 client. For
                        // now, all 1.0 clients will not get persistent connections from the proxy.
                        if (responseMessage.Version == "1.0")
                        {
                            Log.Info("{0} Closing client connection", session.Id);
                            session.EndSession(false);
                        }

                            // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec8.html#sec8.1
                        else if (responseMessage.Version == "1.1")
                        {
                            KeyValuePair<string, string> connectionHeader =
                                responseMessage.Headers.SingleOrDefault(
                                    s =>
                                    s.Key.Equals("Connection",
                                                  StringComparison.
                                                      InvariantCultureIgnoreCase));

                            // Closing the connection if status code != 200 is not part of the HTTP standard,
                            // at least as far as I can tell, but things don't work correctly if this is not done.
                            if (responseMessage.StatusCode != 200 ||
                                (!connectionHeader.Equals(
                                    default(KeyValuePair<string, string>))
                                &&
                                connectionHeader.Value.Equals("close",
                                                               StringComparison.
                                                                   InvariantCultureIgnoreCase)))
                            {
                                Log.Info("{0} Closing client connection", session.Id);
                                session.EndSession(false);
                            }
                            else
                            {
                                session.ProxyClient.SendComplete(this);
                            }
                        }
                        else
                        {
                            Log.Info("{0} Closing client connection", session.Id);
                            session.EndSession(false);
                            session.ProxyClient.SendComplete(this);
                        }
                        
                    }
                    else
                    {
                        session.EndSession(true);
                        Log.Error("An internal error occurred. The response to the client was missing or invaild.");
                    }
                }


            }
            catch ( Exception ex )
            {
                session.EndSession(true);
                Log.Exception(session.Id + " An error was received when sending message to the client.", ex);
            }


        }

        private void HandleConnectToServer(IAsyncResult ar)
        {
            Session session = ar.AsyncState as Session;

            try
            {
                session.ServerSocket.EndConnect(ar);

                session.ServerSocket.BeginSend(session.Buffer,
                                                0,
                                                session.Buffer.Length,
                                                SocketFlags.None,
                                                HandleSendToServer,
                                                session);

            }
            catch ( Exception ex )
            {
                session.EndSession(true);
                Log.Exception( session.Id + " An error occurred while trying to connect to remote host. ", ex );
            }
            
        }

        private void HandleSendToServer(IAsyncResult ar)
        {
            Session session = ar.AsyncState as Session;

            try
            {
                SocketError socketError;

                session.ServerSocket.EndSend( ar, out socketError );

                if(socketError == SocketError.Success)
                {
                    session.ProxyClient.SendComplete(this);
                }
                else
                {
                    session.EndSession(true);
                    Log.Error( session.Id + " An error occurred while sending data to the remote host. " + socketError);
                }
            }
            catch(Exception ex)
            {
                session.EndSession(true);
                Log.Exception( session.Id + " An error occurred while sending data to the remote host.", ex );
            }


        }

        public void SendMessage(IProxyClient client, IHttpRequestMessage message)
        {
            Session session;

            if (_activeSessions.TryGetValue(client, out session))
            {
                session.Buffer = message.CreateHttpMessage();
                session.Message = message;

                if(session.ServerSocket == null)
                {
                    if(!session.ServerSocket.Connected)
                    {
                        session.ServerSocket = null;
                    }

                    session.ServerSocket = new Socket(AddressFamily.InterNetwork,
                                                       SocketType.Stream,
                                                       ProtocolType.Tcp);

                    session.ServerSocket.BeginConnect(message.Destination.Host,
                                                       message.Destination.Port,
                                                       HandleConnectToServer,
                                                       session);
                }
                else
                {
                    session.ServerSocket.BeginSend(session.Buffer,
                                                    0,
                                                    session.Buffer.Length,
                                                    SocketFlags.None,
                                                    HandleSendToServer,
                                                    session);
                }

            }
        }

        public void SendMessage( IProxyClient client, IHttpResponseMessage message )
        {
            Session session;

            if (_activeSessions.TryGetValue(client, out session))
            {
                session.Buffer = message.CreateHttpMessage();
                session.Message = message;

                session.ClientSocket.BeginSend( session.Buffer,
                                                0,
                                                session.Buffer.Length,
                                                SocketFlags.None,
                                                HandleSendToClient,
                                                session );
            }
        }

        public void GetDataFromClient( IProxyClient client )
        {
            Session session;

            if ( _activeSessions.TryGetValue( client, out session ) )
            {
                session.ResetBufferForReceive();

                session.ClientSocket.BeginReceive( session.Buffer,
                                                   0,
                                                   session.Buffer.Length,
                                                   SocketFlags.None,
                                                   HandleDataFromClient,
                                                   session);
            }
        }

        public void GetDataFromRemoteHost( IProxyClient client )
        {
            Session session;

            if (_activeSessions.TryGetValue(client, out session))
            {
                session.ResetBufferForReceive();

                session.ServerSocket.BeginReceive(session.Buffer,
                                                   0,
                                                   session.Buffer.Length,
                                                   SocketFlags.None,
                                                   HandleDataFromRemoteHost,
                                                   session);
            }
        }
    }
}
