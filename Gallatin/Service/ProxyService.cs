using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Gallatin.Core.Client;
using Gallatin.Core.Util;

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
                throw new ArgumentNullException( "proxyClientFactory" );

            _proxyClientFactory = proxyClientFactory;
        }

        #region INetworkService Members

        public void SendServerMessage( IProxyClient client, byte[] message, string host, int port )
        {
            Session session = FindSessionByClientReference( client );
            if ( session == null )
                throw new ArgumentException( "Invalid client session" );

            if ( client == null )
                throw new ArgumentNullException( "client" );
            if ( message == null )
                throw new ArgumentNullException( "message" );
            if ( host == null )
                throw new ArgumentNullException( "host" );
            if ( port <= 0
                 || port > 65536 )
                throw new ArgumentException( "Invalid port range" );

            if ( session.IsActive )
            {
                session.LastActivityTime = DateTime.Now;
                session.ServerBuffer = message;

                Log.Verbose(
                    () =>
                    string.Format( "{0} {1}", session.Id, Encoding.UTF8.GetString( message ) ) );

                // Is the server socket connected? If so, has the client changed hosts/ports since the last
                // requests. I don't see why this should happen, but it does happen with some web browsers
                // I've tested. Maybe it should be an error?
                if ( session.ServerSocket == null || host != session.Host
                     || port != session.Port )
                {
                    Log.Info( "{0} Establishing new connection to {1}:{2}", session.Id, host, port );

                    if ( session.ServerSocket != null )
                    {
                        Log.Warning(
                            "Client changed host/port using same session. Establishing connect with new host." );

                        if ( session.ServerSocket.Connected )
                        {
                            session.ServerSocket.Shutdown( SocketShutdown.Both );
                            session.ServerSocket.Close();
                        }

                        session.ServerSocket = null;
                    }

                    session.Host = host;
                    session.Port = port;

                    session.ServerSocket = new Socket( AddressFamily.InterNetwork,
                                                       SocketType.Stream,
                                                       ProtocolType.Tcp );

                    // TODO: re-evaluate
                    session.ServerSocket.SetSocketOption( SocketOptionLevel.Socket,
                                                          SocketOptionName.KeepAlive,
                                                          1 );

                    session.ServerSocket.BeginConnect( host,
                                                       port,
                                                       HandleConnectToServer,
                                                       session );
                }

                else
                {
                    Log.Info(
                        "{0} Sending additional data to the previously connected host {1}:{2}",
                        session.Id,
                        host,
                        port );

                    session.ServerSocket.BeginSend( session.ServerBuffer,
                                                    0,
                                                    session.ServerBuffer.Length,
                                                    SocketFlags.None,
                                                    HandleSendToServer,
                                                    session );
                }
            }
        }

        public void SendClientMessage( IProxyClient client, byte[] message )
        {
            if ( client == null )
                throw new ArgumentNullException( "client" );
            if ( message == null )
                throw new ArgumentNullException( "message" );

            Session session = FindSessionByClientReference( client );
            if ( session == null )
                throw new ArgumentException( "Invalid client session" );

            if ( session.IsActive )
            {
                session.LastActivityTime = DateTime.Now;

                Log.Info("{0} ProxyServer::SendMessage -- Sending response to client",
                          session.Id );

                session.ClientBuffer = message;

                session.ClientSocket.BeginSend( session.ClientBuffer,
                                                0,
                                                session.ClientBuffer.Length,
                                                SocketFlags.None,
                                                HandleSendToClient,
                                                session );
            }
        }


        public void EndClientSession( IProxyClient client )
        {
            if ( client == null )
                throw new ArgumentNullException( "client" );

            Session session = FindSessionByClientReference( client );
            if ( session == null )
                throw new ArgumentException( "Invalid client session" );

            if ( session.IsActive )
                EndSession( session, false );
        }

        #endregion

        #region IProxyService Members

        private object _startStopMutex = new object();
        private Thread _troll;

        public void Start( int port )
        {
            if (_serverSocket != null)
                throw new InvalidOperationException( "Server has already been started" );

            lock(_startStopMutex)
            {
                _troll = new Thread( SessionTrollMethod )
                         {
                             IsBackground = true,
                             Name = "Proxy service session management thread"
                         };
                _troll.Start();

                Log.Info("Starting Proxy Server. Rock on, my friend, rock on!");

                _activeSessions = new List<Session>();

                _serverSocket = new Socket(AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp);

                IPAddress hostAddress =
                    (Dns.GetHostEntry("127.0.0.1")).AddressList[0];
                IPEndPoint endPoint = new IPEndPoint(hostAddress, port);

                _serverSocket.Bind(endPoint);

                _serverSocket.Listen(30);

                _serverSocket.BeginAccept(HandleNewConnect, null);
            }

        }

        private ManualResetEvent _stopTheTroll = new ManualResetEvent(false);

        private void SessionTrollMethod()
        {
            while (!_stopTheTroll.WaitOne(30000))
            {
                Log.Verbose("Troll active");

                // Trolling for little, dead sessions...
                lock( _activeSessions )
                {
                    foreach(var session in _activeSessions.ToArray())
                    {
                        if(session.LastActivityTime < DateTime.Now.AddSeconds(-10))
                        {
                            Log.Warning("{0} Killing session due to inactivity.", session.Id);
                            EndSession(session, true);
                        }
                    }
                }

                Log.Verbose("Troll sleeping...");
            }

            Log.Verbose("Troll stopped");
        }

        public void Stop()
        {
            if (_serverSocket == null)
                throw new InvalidOperationException( "Server has not been started" );

            _stopTheTroll.Set();

            lock (_startStopMutex)
            {
                if (_serverSocket.Connected)
                    _serverSocket.Shutdown(SocketShutdown.Both);
                _serverSocket.Close();
                _serverSocket = null;
            }

            if (!_troll.Join(5000))
            {
                Log.Warning("Failed to terminate session monitor thread. Attempting to abort thread...");
                _troll.Abort();
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

            lock(_activeSessions)
            {
                _activeSessions.Remove(session);
            }
        }

        private void HandleNewConnect( IAsyncResult ar )
        {
            try
            {
                // Server may be in the process of shutting down. Ignore connections.
                if ( _serverSocket != null )
                {
                    Socket clientSocket = _serverSocket.EndAccept( ar );

                    // Immediately listen for the next clientSession
                    _serverSocket.BeginAccept( HandleNewConnect, null );

                    IProxyClient proxyClient = _proxyClientFactory.CreateClient();

                    Session session = new Session( clientSocket, proxyClient );

                    lock(_activeSessions)
                    {
                        _activeSessions.Add(session);
                    }

                    Log.Info( "{0} New client connect", session.Id );

                    proxyClient.StartSession( this );

                    session.ClientBuffer = new byte[Session.BufferSize];

                    session.ClientSocket.BeginReceive( session.ClientBuffer,
                                                       0,
                                                       session.ClientBuffer.Length,
                                                       SocketFlags.None,
                                                       HandleDataFromClient,
                                                       session );
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
            Trace.Assert( session != null );

            try
            {
                session.LastActivityTime = DateTime.Now;

                int bytesReceived = session.ClientSocket.EndReceive(ar);

                if ( bytesReceived > 0 )
                {
                    Log.Info( "{0} Data received from client -- {1} bytes",
                              session.Id,
                              bytesReceived );

                    Log.Verbose(
                        () =>
                        string.Format( "{0}\r\n{1}",
                                       session.Id,
                                       Encoding.UTF8.GetString(
                                           session.ClientBuffer.Take( bytesReceived ).ToArray() ) ) );

                    if ( !session.ProxyClient.TryCompleteMessageFromClient(
                        session.ClientBuffer.Take( bytesReceived ).ToArray() ) )
                        session.ClientSocket.BeginReceive( session.ClientBuffer,
                                                           0,
                                                           session.ClientBuffer.Length,
                                                           SocketFlags.None,
                                                           HandleDataFromClient,
                                                           session );
                }
                else
                {
                    Log.Error(
                        "{0} No data received from client. Client closed connection. Terminating session.",
                        session.Id );
                    EndSession( session, true );
                }
            }
            catch ( Exception ex )
            {
                if ( session.IsActive )
                {
                    Log.Exception(
                        session.Id
                        + " An error was encountered when receiving data from the client.",
                        ex );
                    EndSession( session, true );
                }
            }
        }

        private void HandleDataFromRemoteHost( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;
            Trace.Assert( session != null );

            try
            {
                session.LastActivityTime = DateTime.Now;

                int bytesReceived = session.ServerSocket.EndReceive(ar);

                if ( bytesReceived > 0 )
                {
                    Log.Info( "{0} Data received from remote host. Length {1}.",
                              session.Id,
                              bytesReceived );

                    Log.Verbose(
                        () =>
                        string.Format( "{0} {1}",
                                       session.Id,
                                       Encoding.UTF8.GetString(
                                           session.ServerBuffer.Take( bytesReceived ).ToArray() ) ) );

                    if ( !session.ProxyClient.TryCompleteMessageFromServer(
                        session.ServerBuffer.Take( bytesReceived ).ToArray() ) )
                        session.ServerSocket.BeginReceive( session.ServerBuffer,
                                                           0,
                                                           session.ServerBuffer.Length,
                                                           SocketFlags.None,
                                                           HandleDataFromRemoteHost,
                                                           session );
                }
                else
                {
                    // Server shutdown.
                    Log.Info( "{0} Remote host is shutting down.", session.Id );
                    EndSession( session, false );
                }
            }
            catch ( Exception ex )
            {
                if ( session.IsActive )
                {
                    Log.Exception(
                        session.Id
                        + " An error was encountered when receiving data from the remote host.",
                        ex );
                    EndSession( session, true );
                }
            }
        }

        private void HandleSendToClient( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;
            Trace.Assert( session != null );

            try
            {
                session.LastActivityTime = DateTime.Now;
                
                SocketError socketError;
                int dataSent = session.ClientSocket.EndSend( ar, out socketError );

                if ( socketError != SocketError.Success )
                {
                    Log.Error( "{0} Unable to send message to client: {1}", session.Id, socketError );
                    EndSession( session, true );
                }
                else if ( dataSent == 0 )
                {
                    Log.Warning( "{0} 0 bytes sent to client. Closing session.", session.Id );
                    EndSession( session, false );
                }
                else
                {
                    Log.Info( "{0} Completed data send to client.", session.Id );
                    session.ProxyClient.ClientSendComplete();
                }
            }
            catch ( Exception ex )
            {
                if ( session.IsActive )
                {
                    Log.Exception(
                        session.Id + " An error was received when sending message to the client.",
                        ex );
                    EndSession( session, true );
                }
            }
        }

        private void HandleConnectToServer( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;
            Trace.Assert(session != null);

            try
            {
                session.LastActivityTime = DateTime.Now;

                session.ServerSocket.EndConnect(ar);

                Log.Info( "{0} Connection established. Sending data to remote host.", session.Id );

                session.ServerSocket.BeginSend( session.ServerBuffer,
                                                0,
                                                session.ServerBuffer.Length,
                                                SocketFlags.None,
                                                HandleSendToServer,
                                                session );
            }
            catch ( Exception ex )
            {
                if ( session.IsActive )
                {
                    Log.Exception(
                        session.Id + " An error occurred while trying to connect to remote host. ",
                        ex );
                    EndSession( session, true );
                }
            }
        }

        private void HandleSendToServer( IAsyncResult ar )
        {
            Session session = ar.AsyncState as Session;
            Trace.Assert(session != null);

            try
            {
                session.LastActivityTime = DateTime.Now;

                SocketError socketError;
                session.ServerSocket.EndSend( ar, out socketError );

                if ( socketError == SocketError.Success )
                {
                    Log.Info("{0} Data sent to remote host", session.Id);

                    session.ProxyClient.ServerSendComplete();

                    session.ServerBuffer = new byte[Session.BufferSize];

                    session.ServerSocket.BeginReceive( session.ServerBuffer,
                                                       0,
                                                       session.ServerBuffer.Length,
                                                       SocketFlags.None,
                                                       HandleDataFromRemoteHost,
                                                       session );
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
                if ( session.IsActive )
                {
                    Log.Exception(
                        session.Id + " An error occurred while sending data to the remote host.", ex );
                    EndSession( session, true );
                }
            }
        }

        #region Nested type: Session

        private class Session
        {
            public const int BufferSize = 8000;
            private readonly Mutex _mutex = new Mutex( false );

            public Session( Socket clientSocket, IProxyClient proxyClient )
            {
                ProxyClient = proxyClient;
                ClientSocket = clientSocket;
                Id = Guid.NewGuid();
                IsActive = true;
                LastActivityTime = DateTime.Now;
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
            public DateTime LastActivityTime { get; set; }

            public void EndSession( bool inError )
            {
                _mutex.WaitOne();

                try
                {
                    if ( IsActive )
                    {
                        IsActive = false;

                        Log.Info( "{0} Ending client connection. Error: {1} ", Id, inError );

                        if ( ClientSocket != null )
                        {
                            if ( ClientSocket.Connected )
                            {
                                if ( inError )
                                    ClientSocket.Send(
                                        Encoding.UTF8.GetBytes(
                                            "HTTP/1.0 500 Internal Server Error\r\nContent-Length: 11\r\n\r\nProxy error" ) );

                                ClientSocket.Shutdown( SocketShutdown.Both );
                            }

                            ClientSocket.Close();
                        }

                        if ( ServerSocket != null )
                        {
                            if ( ServerSocket.Connected )
                                ServerSocket.Shutdown( SocketShutdown.Both );

                            ServerSocket.Close();
                        }
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