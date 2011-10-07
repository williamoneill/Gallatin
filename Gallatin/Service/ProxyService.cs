using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Gallatin.Core.Client;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    public class ProxyService : INetworkService
    {
        private readonly IProxyClientFactory _proxyClientFactory;
        private List<Session> _activeSessions;
        private Socket _serverSocket;

        public ProxyService( IProxyClientFactory proxyClientFactory )
        {
            if ( proxyClientFactory == null )
            {
                throw new ArgumentNullException( "proxyClientFactory" );
            }

            _proxyClientFactory = proxyClientFactory;
        }

        #region INetworkService Members

        private Session FindSessionByClientReference( IProxyClient proxyClient )
        {
            return _activeSessions.FirstOrDefault( s => s.ProxyClient == proxyClient );
        }

        public void SendServerMessage( IProxyClient client, byte[] message, string host, int port )
        {
            Session session = FindSessionByClientReference( client );

            if (session != null)
            {
                try
                {
                    session.ServerBuffer = message;

                    Log.Info( "{0} {1}", session.Id, Encoding.UTF8.GetString( message ) );

                    Log.Info(
                        "{0} ProxyServer::SendMessage -- Sending request to remote host: {1} {2}",
                        session.Id,
                        host,
                        port );

                    if ( session.ServerSocket == null || host != session.Host || port != session.Port )
                    {
                        if(session.ServerSocket != null)
                        {
                            if(session.ServerSocket.Connected)
                            {
                                session.ServerSocket.Shutdown(SocketShutdown.Both);
                                session.ServerSocket.Close();
                            }

                            session.ServerSocket = null;
                        }

                        session.Host = host;
                        session.Port = port;

                        session.ServerSocket = new Socket( AddressFamily.InterNetwork,
                                                           SocketType.Stream,
                                                           ProtocolType.Tcp );

                        session.ServerSocket.BeginConnect( host,
                                                           port,
                                                           HandleConnectToServer,
                                                           session );
                    }
                        // TODO: null check may go away once i figure out ssl
                    else if ( message != null )
                    {
                        session.ServerSocket.BeginSend( session.ServerBuffer,
                                                        0,
                                                        session.ServerBuffer.Length,
                                                        SocketFlags.None,
                                                        HandleSendToServer,
                                                        session );
                    }

                }
                catch ( Exception ex )
                {
                    Log.Exception(session.Id + " Unhandled exception when sending server message.", ex );
                }
            }

        }

        public void SendClientMessage(IProxyClient client, byte[] message)
        {
            Session session = FindSessionByClientReference(client);

            if (session != null)
            {
                try
                {
                    Log.Info( "{0} ProxyServer::SendMessage -- Sending response to client",
                              session.Id );

                    session.ClientBuffer = message;

                    session.ClientSocket.BeginSend( session.ClientBuffer,
                                                    0,
                                                    session.ClientBuffer.Length,
                                                    SocketFlags.None,
                                                    HandleSendToClient,
                                                    session );

                }
                catch ( Exception ex )
                {
                    Log.Exception( session.Id +" Unhandled exception when sending client message.", ex );
                }
            }

        }

        public void GetDataFromClient( IProxyClient client )
        {
            Session session = FindSessionByClientReference(client);

            if ( session != null )
            {
                Log.Info( "{0} ProxyServer::GetDataFromClient -- Receiving data from client",
                          session.Id );

                session.ClientBuffer = new byte[Session.BufferSize];

                session.ClientSocket.BeginReceive( session.ClientBuffer,
                                                   0,
                                                   session.ClientBuffer.Length,
                                                   SocketFlags.None,
                                                   HandleDataFromClient,
                                                   session );
            }
        }

        public void GetDataFromRemoteHost( IProxyClient client )
        {
            Session session = FindSessionByClientReference(client);

            if ( session != null )
            {
                Log.Info(
                    "{0} ProxyServer::GetDataFromRemoteHost -- Receiving data from remote host",
                    session.Id );

                session.ServerBuffer = new byte[Session.BufferSize];

                session.ServerSocket.BeginReceive( session.ServerBuffer,
                                                   0,
                                                   session.ServerBuffer.Length,
                                                   SocketFlags.None,
                                                   HandleDataFromRemoteHost,
                                                   session );
            }
        }

        public void EndClientSession( IProxyClient client )
        {
            Session session = FindSessionByClientReference(client);

            if (session != null)
            {
                EndSession(session, false);
            }
        }

        #endregion

        private void EndSession( Session session, bool inError )
        {
            Log.Info( "{0} Ending session", session.Id );
            session.EndSession( inError );
            _activeSessions.Remove( session );
        }

        public void Start( int port )
        {
            if ( _serverSocket == null )
            {
                _activeSessions = new List<Session>();

                _serverSocket = new Socket( AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp );

                IPAddress hostAddress =
                    ( Dns.Resolve( IPAddress.Any.ToString() ) ).AddressList[0];
                IPEndPoint endPoint = new IPEndPoint( hostAddress, port );

                _serverSocket.Bind( endPoint );

                _serverSocket.Listen( 30 );

                _serverSocket.BeginAccept( HandleNewConnect, null );
            }
        }

        private void HandleNewConnect( IAsyncResult ar )
        {
            try
            {
                Socket clientSocket = _serverSocket.EndAccept( ar );

                // Immediately listen for the next clientSession
                _serverSocket.BeginAccept( HandleNewConnect, null );

                IProxyClient proxyClient = _proxyClientFactory.CreateClient();

                Session session = new Session( clientSocket, proxyClient );

                _activeSessions.Add( session );

                Log.Info( "{0} New client connect", session.Id );

                proxyClient.StartSession( this );
            }
            catch ( Exception ex )
            {
                Log.Exception( "Unable to service new clientSession connection.", ex );
            }
        }

        private void HandleDataFromClient( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;

            if ( session == null )
            {
                throw new InvalidOperationException(
                    "Internal error. Client session was invalid." );
            }

            try
            {
                int bytesReceived = session.ClientSocket.EndReceive( ar );

                Log.Info("{0} Data received from client -- {1} bytes", session.Id, bytesReceived);

                if (bytesReceived > 0)
                {
                    session.ProxyClient.NewDataAvailableFromClient(session.ClientBuffer.Take(bytesReceived).ToArray());
                }
            }
            catch ( Exception ex )
            {
                Log.Exception(
                    session.Id + " An error was encountered when receiving data from the client.",
                    ex);
                EndSession(session, true);
            }
        }

        private void HandleDataFromRemoteHost( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;

            if ( session == null )
            {
                throw new InvalidOperationException(
                    "Internal error. Client session was invalid." );
            }

            try
            {
                int bytesReceived = session.ServerSocket.EndReceive( ar );

                Log.Info( "{0} Data received from remote host. Length {1}",
                          session.Id,
                          bytesReceived );

                Log.Info("{0} {1}", session.Id, Encoding.UTF8.GetString(session.ServerBuffer.Take(bytesReceived).ToArray()));

                if ( bytesReceived > 0 )
                {
                    session.ProxyClient.NewDataAvailableFromServer( session.ServerBuffer.Take( bytesReceived ).ToArray() );
                }
            }
            catch ( Exception ex )
            {
                Log.Exception(
                    session.Id
                    + " An error was encountered when receiving data from the remote host.",
                    ex);
                EndSession(session, true);
            }
        }

        private void HandleSendToClient( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;

            if ( session == null )
            {
                throw new InvalidOperationException(
                    "Internal error. Client session was invalid." );
            }

            try
            {
                Log.Info( "{0} Sending data to client", session.Id );

                SocketError socketError;

                session.ClientSocket.EndSend( ar, out socketError );

                if ( socketError != SocketError.Success )
                {
                    Log.Error("{0} Unable to send message to client: {1}", session.Id, socketError);
                    EndSession(session, true);
                }
                else
                {
                    session.ProxyClient.ClientSendComplete();
                }
            }
            catch ( Exception ex )
            {
                Log.Exception(
                    session.Id + " An error was received when sending message to the client.", ex);
                EndSession(session, true);
            }
        }

        private void HandleConnectToServer( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;

            if ( session == null )
            {
                throw new InvalidOperationException(
                    "Internal error. Client session was invalid." );
            }

            try
            {
                Log.Info( "{0} Connected to remote host", session.Id );

                session.ServerSocket.EndConnect( ar );

                // TODO: this may go away once i figure out ssl
                if( session.ServerBuffer != null )
                {
                    session.ServerSocket.BeginSend(session.ServerBuffer,
                                                    0,
                                                    session.ServerBuffer.Length,
                                                    SocketFlags.None,
                                                    HandleSendToServer,
                                                    session);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception(
                    session.Id + " An error occurred while trying to connect to remote host. ", ex);
                EndSession(session, true);
            }
        }

        private void HandleSendToServer( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;

            if ( session == null )
            {
                throw new InvalidOperationException(
                    "Internal error. Client session was invalid." );
            }

            try
            {
                Log.Info( "{0} Data sent to remote host", session.Id );

                SocketError socketError;

                session.ServerSocket.EndSend( ar, out socketError );

                if ( socketError == SocketError.Success )
                {
                    session.ProxyClient.ServerSendComplete();
                }
                else
                {
                    Log.Error(session.Id
                               + " An error occurred while sending data to the remote host. "
                               + socketError);
                    EndSession(session, true);
                }
            }
            catch ( Exception ex )
            {
                Log.Exception(
                    session.Id + " An error occurred while sending data to the remote host.", ex);
                EndSession(session, true);
            }
        }

        #region Nested type: Session

        private class Session
        {
            public const int BufferSize = 100000;

            public Session( Socket clientSocket, IProxyClient proxyClient )
            {
                ProxyClient = proxyClient;
                ClientSocket = clientSocket;
                Id = Guid.NewGuid();
            }

            public Guid Id { get; private set; }
            public Socket ClientSocket { get; private set; }
            public Socket ServerSocket { get; set; }
            public byte[] ClientBuffer { get; set; }
            public IProxyClient ProxyClient { get; private set; }
            public byte[] ServerBuffer { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }

            public void EndSession( bool inError )
            {
                Log.Info( "{0} Ending client connection. Error: {1} ", Id, inError );

                if ( ClientSocket != null )
                {
                    if ( ClientSocket.Connected )
                    {
                        if ( inError )
                        {
                            ClientSocket.Send(
                                Encoding.UTF8.GetBytes(
                                    "HTTP/1.0 500 Internal Server Error\r\nContent-Length: 11\r\n\r\nProxy error" ) );
                        }

                        ClientSocket.Shutdown( SocketShutdown.Both );
                    }

                    ClientSocket.Close();
                    ClientSocket.Dispose();
                }

                if ( ServerSocket != null )
                {
                    if ( ServerSocket.Connected )
                    {
                        ServerSocket.Shutdown( SocketShutdown.Both );
                    }

                    ServerSocket.Close();
                    ServerSocket.Dispose();
                }
            }
        }

        #endregion
    }

}