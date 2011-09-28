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
        Guid Id { get; }
        void SendComplete();
        void NewDataAvailable( IEnumerable<byte> data );
    }

    public class ProxyClient : IProxyClient
    {
        public ProxyClient(INetworkService networkService)
        {
            
        }

        public Guid Id
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void SendComplete()
        {
            throw new NotImplementedException();
        }

        public void NewDataAvailable( IEnumerable<byte> data )
        {
            throw new NotImplementedException();
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
                Buffer = new byte[8192];
            }

            public Socket ClientSocket { get; set; }
            public Socket ServerSocket { get; set; }
            public byte[] Buffer { get; set; }
            public IProxyClient ProxyClient { get; set; }
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

                this._activeSessions.Add( new ProxyClient(this), new Session(clientSocket) );
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
                session.ProxyClient.NewDataAvailable( session.Buffer.Take( bytesReceived ) );
            }
            catch ( Exception ex )
            {
                client.Client.EndSession(true);
                Log.Exception( client.Client.Id + " An error was encountered when receiving data.", ex);
            }
        }

        public void GetData( IProxyClient proxyClient )
        {
            _Client client = new _Client(proxyClient);

            proxyClient.ActiveSocket.BeginReceive(client.Buffer,
                                 0,
                                 client.Buffer.Length,
                                 SocketFlags.None,
                                 HandleReceive,
                                 client );
        }

        public void SendMessage( IProxyClient client, IHttpRequestMessage message )
        {
            throw new NotImplementedException();
        }

        public void SendMessage( IProxyClient client, IHttpResponseMessage message )
        {
            throw new NotImplementedException();
        }

        public void GetDataFromClient( IProxyClient client )
        {
            Session session;

            if ( _activeSessions.TryGetValue( client, out session ) )
            {
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
            throw new NotImplementedException();
        }
    }
}
