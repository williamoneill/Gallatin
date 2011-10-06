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
        private Dictionary<IProxyClient, Session> _activeSessions;
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

        public void SendServerMessage( IProxyClient client, IEnumerable<byte> message, string host, int port )
        {
            // TODO: verify host:port does not change if socket is connected

            Session session;

            if ( _activeSessions.TryGetValue( client, out session ) )
            {
                //string destination = message.Destination.Host;
                //int port = message.Destination.Port;

                //// Not the standard port 80?
                //if ( port == -1 )
                //{
                //    string[] tokens = message.Destination.AbsoluteUri.Split( ':' );
                //    if ( tokens.Length == 2 )
                //    {
                //        destination = tokens[0];
                //        port = int.Parse( tokens[1] );
                //    }
                //}

                session.Buffer = message.ToArray();

                Log.Info(
                    "{0} ProxyServer::SendMessage -- Sending request to remote host: {1} {2}",
                    session.Id,
                    host,
                    port );

                if ( session.ServerSocket == null )
                {
                    session.ServerSocket = new Socket( AddressFamily.InterNetwork,
                                                       SocketType.Stream,
                                                       ProtocolType.Tcp );

                    session.ServerSocket.BeginConnect( host,
                                                       port,
                                                       HandleConnectToServer,
                                                       session );
                }
                else
                {
                    session.ServerSocket.BeginSend( session.Buffer,
                                                    0,
                                                    session.Buffer.Length,
                                                    SocketFlags.None,
                                                    HandleSendToServer,
                                                    session );
                }
            }
        }

        public void SendClientMessage(IProxyClient client, IEnumerable<byte> message)
        {
            Session session;

            if ( _activeSessions.TryGetValue( client, out session ) )
            {
                Log.Info( "{0} ProxyServer::SendMessage -- Sending response to client", session.Id );

                session.Buffer = message.ToArray();

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
                Log.Info( "{0} ProxyServer::GetDataFromClient -- Receiving data from client",
                          session.Id );

                session.ResetBufferForReceive();

                session.ClientSocket.BeginReceive( session.Buffer,
                                                   0,
                                                   session.Buffer.Length,
                                                   SocketFlags.None,
                                                   HandleDataFromClient,
                                                   session );
            }
        }

        public void GetDataFromRemoteHost( IProxyClient client )
        {
            Session session;

            if ( _activeSessions.TryGetValue( client, out session ) )
            {
                Log.Info(
                    "{0} ProxyServer::GetDataFromRemoteHost -- Receiving data from remote host",
                    session.Id );

                session.ResetBufferForReceive();

                session.ServerSocket.BeginReceive( session.Buffer,
                                                   0,
                                                   session.Buffer.Length,
                                                   SocketFlags.None,
                                                   HandleDataFromRemoteHost,
                                                   session );
            }
        }

        #endregion

        private void EndSession( Session session, bool inError )
        {
            Log.Info( "{0} Ending session", session.Id );
            session.ProxyClient.EndSession();
            session.EndSession( inError );
            _activeSessions.Remove( session.ProxyClient );
        }

        public void Start( int port )
        {
            if ( _serverSocket == null )
            {
                _activeSessions = new Dictionary<IProxyClient, Session>();

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

                _activeSessions.Add( proxyClient, session );

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
                Log.Info( "{0} Data received from client", session.Id );

                int bytesReceived = session.ClientSocket.EndReceive( ar );
                session.ProxyClient.NewDataAvailableFromClient( session.Buffer.Take( bytesReceived ) );
            }
            catch ( Exception ex )
            {
                EndSession( session, true );
                Log.Exception(
                    session.Id + " An error was encountered when receiving data from the client.",
                    ex );
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

                if ( bytesReceived > 0 )
                {
                    session.ProxyClient.NewDataAvailableFromServer( session.Buffer.Take( bytesReceived ) );
                }
            }
            catch ( Exception ex )
            {
                EndSession( session, true );
                Log.Exception(
                    session.Id
                    + " An error was encountered when receiving data from the remote host.",
                    ex );
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
                    EndSession( session, true );
                    Log.Error( "{0} Unable to send message to client: {1}", session.Id, socketError );
                }
                else
                {
                    Log.Info( "{0} Closing client connection", session.Id );
                    EndSession( session, false );

                    //if (responseMessage != null)
                    //{
                    //    // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html#sec19.6.2
                    //    // See RFC 8.1.3 - proxy servers must not establish a HTTP/1.1 persistent connection with 1.0 client. For
                    //    // now, all 1.0 clients will not get persistent connections from the proxy.
                    //    if ( responseMessage.Version == "1.0" )
                    //    {
                    //        Log.Info( "{0} Closing client connection", session.Id );
                    //        EndSession( session, false );
                    //    }

                    //        // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec8.html#sec8.1
                    //    else if ( responseMessage.Version == "1.1" )
                    //    {
                    //        KeyValuePair<string, string> connectionHeader =
                    //            responseMessage.Headers.SingleOrDefault(
                    //                s =>
                    //                s.Key.Equals( "Connection",
                    //                              StringComparison.
                    //                                  InvariantCultureIgnoreCase ) );

                    //        // Closing the connection if status code != 200 is not part of the HTTP standard,
                    //        // at least as far as I can tell, but things don't work correctly if this is not done.
                    //        if ( responseMessage.StatusCode != 200
                    //             ||
                    //             ( !connectionHeader.Equals(
                    //                 default( KeyValuePair<string, string> ) )
                    //               &&
                    //               connectionHeader.Value.Equals( "close",
                    //                                              StringComparison.
                    //                                                  InvariantCultureIgnoreCase ) ) )
                    //        {
                    //            Log.Info( "{0} Closing client connection", session.Id );
                    //            EndSession( session, false );
                    //        }
                    //        else
                    //        {
                    //            Log.Info("{0} Maintaining persistent client connection", session.Id);
                    //            session.ProxyClient.SendComplete();
                    //        }
                    //    }
                    //    else
                    //    {
                    //        Log.Info( "{0} Closing client connection", session.Id );
                    //        EndSession( session, false );
                    //    }
                    //}
                    //else
                    //{
                    //    EndSession( session, true );
                    //    Log.Error(
                    //        "An internal error occurred. The response to the client was missing or invaild." );
                    //}
                }
            }
            catch ( Exception ex )
            {
                EndSession( session, true );
                Log.Exception(
                    session.Id + " An error was received when sending message to the client.", ex );
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

                session.ServerSocket.BeginSend( session.Buffer,
                                                0,
                                                session.Buffer.Length,
                                                SocketFlags.None,
                                                HandleSendToServer,
                                                session );
            }
            catch ( Exception ex )
            {
                EndSession( session, true );
                Log.Exception(
                    session.Id + " An error occurred while trying to connect to remote host. ", ex );
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
                    session.ProxyClient.SendComplete();
                }
                else
                {
                    EndSession( session, true );
                    Log.Error( session.Id
                               + " An error occurred while sending data to the remote host. "
                               + socketError );
                }
            }
            catch ( Exception ex )
            {
                EndSession( session, true );
                Log.Exception(
                    session.Id + " An error occurred while sending data to the remote host.", ex );
            }
        }

        #region Nested type: Session

        private class Session
        {
            private const int BufferSize = 8192;

            public Session( Socket clientSocket, IProxyClient proxyClient )
            {
                ProxyClient = proxyClient;
                ClientSocket = clientSocket;
                Buffer = new byte[BufferSize];
                Id = Guid.NewGuid();
            }

            public Guid Id { get; private set; }
            public Socket ClientSocket { get; private set; }
            public Socket ServerSocket { get; set; }
            public byte[] Buffer { get; set; }
            public IProxyClient ProxyClient { get; private set; }

            public void ResetBufferForReceive()
            {
                Buffer = new byte[BufferSize];
            }

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