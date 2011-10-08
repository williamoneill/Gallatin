using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Gallatin.Core.Client;
using Gallatin.Core.Util;
using System.Threading;

namespace Gallatin.Core.Service
{
    public class ProxyService : INetworkService, IProxyService
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

        public void SendServerMessage( IProxyClient client, byte[] message, string host, int port )
        {
            Session session = FindSessionByClientReference( client );

            if ( session == null )
            {
                throw new ArgumentException( "Invalid client session" );
            }
            if(session.IsActive)
            {
                session.ServerBuffer = message;

                if (message != null)
                {
                    Log.Verbose(
                        () => string.Format("{0} {1}", session.Id, Encoding.UTF8.GetString(message)));
                }
                else
                {
                    Log.Info("{0} Not sending any data to remote host, only establishing a connnection.", session.Id);
                }

                // Is the server socket connected? If so, has the client changed hosts/ports since the last
                // requests. I don't see why this should happen, but it does happen with some web browsers
                // I've tested. Maybe it should be an error?
                if (session.ServerSocket == null || host != session.Host
                        || port != session.Port)
                {
                    Log.Info("{0} Establishing new connection to {1}:{2}", session.Id, host, port);

                    if (session.ServerSocket != null)
                    {
                        Log.Warning("Client changed host/port using same session. Establishing connect with new host.");

                        if (session.ServerSocket.Connected)
                        {
                            session.ServerSocket.Shutdown(SocketShutdown.Both);
                            session.ServerSocket.Close();
                        }

                        session.ServerSocket = null;
                    }

                    session.Host = host;
                    session.Port = port;

                    session.ServerSocket = new Socket(AddressFamily.InterNetwork,
                                                        SocketType.Stream,
                                                        ProtocolType.Tcp);

                    session.ServerSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1 );

                    session.ServerSocket.BeginConnect(host,
                                                        port,
                                                        HandleConnectToServer,
                                                        session);
                }

                else
                {
                    Log.Info("{0} Sending additional data to the previously connected host {1}:{2}", session.Id, host, port);

                    // Why would we be sending a null message to a connected server? Internal error.
                    if (message == null)
                    {
                        throw new InvalidOperationException(
                            "Attempting to re-connect to the remote host. This is an internal error in the proxy.");
                    }

                    // TODO: remove after testing
                    Log.Verbose(Encoding.UTF8.GetString(message));

                    session.ServerSocket.BeginSend(session.ServerBuffer,
                                                    0,
                                                    session.ServerBuffer.Length,
                                                    SocketFlags.None,
                                                    HandleSendToServer,
                                                    session);
                }
                
            }

        }

        public void SendClientMessage( IProxyClient client, byte[] message )
        {
            Session session = FindSessionByClientReference( client );

            if ( session == null )
            {
                throw new ArgumentException( "Invalid client session" );
            }

            if(session.IsActive)
            {
                Log.Info("{0} ProxyServer::SendMessage -- Sending response to client",
                            session.Id);

                session.ClientBuffer = message;

                session.ClientSocket.BeginSend(session.ClientBuffer,
                                                0,
                                                session.ClientBuffer.Length,
                                                SocketFlags.None,
                                                HandleSendToClient,
                                                session);
            }

        }

        public void GetDataFromClient( IProxyClient client )
        {
            Session session = FindSessionByClientReference( client );

            if ( session == null )
            {
                throw new ArgumentException( "Invalid client session" );
            }

            if(session.IsActive)
            {
                Log.Info("{0} ProxyServer::GetDataFromClient -- Receiving data from client",
                                session.Id);

                session.ClientBuffer = new byte[Session.BufferSize];

                session.ClientSocket.BeginReceive(session.ClientBuffer,
                                                    0,
                                                    session.ClientBuffer.Length,
                                                    SocketFlags.None,
                                                    HandleDataFromClient,
                                                    session);
                
            }


        }

        public void GetDataFromRemoteHost( IProxyClient client )
        {
            Session session = FindSessionByClientReference( client );

            if ( session == null )
            {
                throw new ArgumentException("Invalid client session");
            }

            if(session.IsActive)
            {
                Log.Info(
                    "{0} ProxyServer::GetDataFromRemoteHost -- Receiving data from remote host",
                    session.Id);

                session.ServerBuffer = new byte[Session.BufferSize];

                session.ServerSocket.BeginReceive(session.ServerBuffer,
                                                   0,
                                                   session.ServerBuffer.Length,
                                                   SocketFlags.None,
                                                   HandleDataFromRemoteHost,
                                                   session);
                
            }
        }

        public void EndClientSession( IProxyClient client )
        {
            Session session = FindSessionByClientReference( client );

            if (session == null)
            {
                throw new ArgumentException("Invalid client session");
            }

            if(session.IsActive)
            {
                EndSession(session, false);
            }
        }

        #endregion

        private Session FindSessionByClientReference( IProxyClient proxyClient )
        {
            return _activeSessions.FirstOrDefault( s => s.ProxyClient == proxyClient );
        }

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
                    ( Dns.GetHostEntry( "127.0.0.1" ) ).AddressList[0];
                IPEndPoint endPoint = new IPEndPoint( hostAddress, port );

                _serverSocket.Bind( endPoint );

                _serverSocket.Listen( 30 );

                _serverSocket.BeginAccept( HandleNewConnect, null );
            }
        }

        public void Stop()
        {
            // TODO: make this thread safe

            if(_serverSocket.Connected)
                _serverSocket.Shutdown(SocketShutdown.Both);
            _serverSocket.Close();
            _serverSocket = null;
        }

        private void HandleNewConnect( IAsyncResult ar )
        {
            try
            {
                // Server may be in the process of shutting down. Ignore connections.
                if(_serverSocket != null)
                {
                    Socket clientSocket = _serverSocket.EndAccept(ar);

                    // Immediately listen for the next clientSession
                    _serverSocket.BeginAccept(HandleNewConnect, null);

                    IProxyClient proxyClient = _proxyClientFactory.CreateClient();

                    Session session = new Session(clientSocket, proxyClient);

                    _activeSessions.Add(session);

                    Log.Info("{0} New client connect", session.Id);

                    proxyClient.StartSession(this);
                    
                }
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

                Log.Info( "{0} Data received from client -- {1} bytes", session.Id, bytesReceived );

                if ( bytesReceived > 0 )
                {
                    session.ProxyClient.NewDataAvailableFromClient(
                        session.ClientBuffer.Take( bytesReceived ).ToArray() );
                }
                else
                {
                    Log.Error("{0} No data received from client. Terminating session.", session.Id);
                    EndSession(session, true);
                }
            }
            catch ( Exception ex )
            {
                if(session.IsActive)
                {
                    Log.Exception(
                        session.Id + " An error was encountered when receiving data from the client.",
                        ex);
                    EndSession(session, true);
                }
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

                Log.Info("{0} Data received from remote host. Length {1}.",
                          session.Id,
                          bytesReceived);

                Log.Verbose(
                    () =>
                    string.Format( "{0} {1}",
                                   session.Id,
                                   Encoding.UTF8.GetString(
                                       session.ServerBuffer.Take( bytesReceived ).ToArray() ) ) );

                if ( bytesReceived > 0 )
                {
                    session.ProxyClient.NewDataAvailableFromServer(
                        session.ServerBuffer.Take( bytesReceived ).ToArray() );
                }
                else
                {
                    // Server shutdown.
                    Log.Info("{0} Remote host is shutting down.", session.Id);
                    EndSession(session, false);
                }
            }
            catch ( Exception ex )
            {
                if(session.IsActive)
                {
                    Log.Exception(
                        session.Id
                        + " An error was encountered when receiving data from the remote host.",
                        ex);
                    EndSession(session, true);
                }
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

                int dataSent = session.ClientSocket.EndSend( ar, out socketError );

                if ( socketError != SocketError.Success )
                {
                    Log.Error( "{0} Unable to send message to client: {1}", session.Id, socketError );
                    EndSession( session, true );
                }
                else if(dataSent == 0)
                {
                    Log.Warning("0 bytes sent to client");
                    EndSession(session, false);
                }
                else
                {
                    session.ProxyClient.ClientSendComplete();
                }
            }
            catch ( Exception ex )
            {
                if(session.IsActive)
                {
                    Log.Exception(
                        session.Id + " An error was received when sending message to the client.", ex);
                    EndSession(session, true);
                }
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

                if ( session.ServerBuffer == null )
                {
                    Log.Info("{0} Established connection with remote host. Not data sent as expected (connect only).", session.Id);
                    session.ProxyClient.ServerSendComplete();

                }
                else
                {
                    Log.Info("{0} Connection established. Sending data to remote host.");
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
                if(session.IsActive)
                {
                    Log.Exception(
                        session.Id + " An error occurred while trying to connect to remote host. ", ex);
                    EndSession(session, true);
                }
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
                    Log.Error( session.Id
                               + " An error occurred while sending data to the remote host. "
                               + socketError );
                    EndSession( session, true );
                }
            }
            catch ( Exception ex )
            {
                if(session.IsActive)
                {
                    Log.Exception(
                        session.Id + " An error occurred while sending data to the remote host.", ex);
                    EndSession(session, true);
                }
            }
        }

        #region Nested type: Session

        private class Session
        {
            public const int BufferSize = 8000;

            public Session( Socket clientSocket, IProxyClient proxyClient )
            {
                ProxyClient = proxyClient;
                ClientSocket = clientSocket;
                Id = Guid.NewGuid();
                IsActive = true;
            }

            public Guid Id { get; private set; }
            public Socket ClientSocket { get; private set; }
            public Socket ServerSocket { get; set; }
            public byte[] ClientBuffer { get; set; }
            public IProxyClient ProxyClient { get; private set; }
            public byte[] ServerBuffer { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }
            public bool IsActive { get; private set; }
            private Mutex _mutex = new Mutex(false);

            public void EndSession( bool inError )
            {
                _mutex.WaitOne();

                try
                {
                    if(IsActive)
                    {
                        Log.Info("{0} Ending client connection. Error: {1} ", Id, inError);

                        if (ClientSocket != null)
                        {
                            if (ClientSocket.Connected)
                            {
                                if (inError)
                                {
                                    ClientSocket.Send(
                                        Encoding.UTF8.GetBytes(
                                            "HTTP/1.0 500 Internal Server Error\r\nContent-Length: 11\r\n\r\nProxy error"));
                                }

                                ClientSocket.Shutdown(SocketShutdown.Both);
                            }

                            ClientSocket.Close();
                        }

                        if (ServerSocket != null)
                        {
                            if (ServerSocket.Connected)
                            {
                                ServerSocket.Shutdown(SocketShutdown.Both);
                            }

                            ServerSocket.Close();
                        }

                        IsActive = false;
                    }
                }
                finally 
                {
                    _mutex.ReleaseMutex();
                }

            }
        }

        #endregion
    }
}