using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Gallatin.Core
{
    public class ProxyServerOld : IProxyServer, INetworkMessageService
    {
        private readonly List<IClientSession> _activeSessions = new List<IClientSession>();
        private readonly ManualResetEvent _shutdown = new ManualResetEvent( false );
        private readonly Mutex _shutdownMutex = new Mutex( false );
        private int _listeningPort;
        private Socket _serverSocket;
        private Thread _worker;

        #region INetworkMessageService Members

        public event EventHandler<ClientRequestArgs> ClientMessagePosted;

        public event EventHandler<ServerResponseArgs> ServerResponsePosted;

        public void SendServerMessage( IHttpRequestMessage message, IClientSession clientSession )
        {
            ClientSession session = clientSession as ClientSession;

            if ( session == null )
            {
                throw new ArgumentException( "Client session not recognized" );
            }

            session.Buffer = message.CreateHttpMessage();
            //Log.Info("{0} Sending message to server", session.SessionId);
            //Log.Info(() => string.Format("{0} {1}", session.SessionId.ToString(), Encoding.UTF8.GetString(session.Buffer)));

            if ( session.ServerSocket == null
                 || !session.ServerSocket.Connected )
            {
                Log.Info("{0} Establishing server connection", session.SessionId);

                session.ServerSocket = new Socket(AddressFamily.InterNetwork,
                                                   SocketType.Stream,
                                                   ProtocolType.Tcp );

                session.ServerSocket.BeginConnect( message.Destination.Host,
                                                   message.Destination.Port,
                                                   HandleServerConnect,
                                                   session );
            }
            else
            {
                Log.Info("{0} Reusing active server connection", session.SessionId);

                session.ActiveSocket = session.ServerSocket;

                session.ActiveSocket.BeginSend( session.Buffer,
                                                0,
                                                session.Buffer.Length,
                                                SocketFlags.None,
                                                HandleSend,
                                                session );
            }
        }


        public void SendClientMessage( IHttpResponseMessage message, IClientSession clientSession )
        {
            ClientSession session = clientSession as ClientSession;

            if ( session == null )
            {
                throw new ArgumentException( "Client session not recognized" );
            }

            if ( session.ClientSocket == null )
            {
                throw new ArgumentException( "Client is not connected" );
            }

            session.ActiveSocket = session.ClientSocket;

            session.Buffer = message.CreateHttpMessage();

            //Log.Info("{0} Returning message to client", session.SessionId);
            //Log.Info(() => string.Format("{0} {1}", session.SessionId.ToString(), Encoding.UTF8.GetString(session.Buffer)));

            session.ActiveSocket.BeginSend( session.Buffer,
                                            0,
                                            session.Buffer.Length,
                                            SocketFlags.None,
                                            HandleSend,
                                            session );
        }

        #endregion

        #region IProxyServer Members

        public void Start( int port )
        {
            if ( _shutdownMutex.WaitOne( 1000 ) )
            {
                try
                {
                    if ( _serverSocket == null )
                    {
                        _listeningPort = port;

                        _serverSocket = new Socket( AddressFamily.InterNetwork,
                                                    SocketType.Stream,
                                                    ProtocolType.Tcp );

                        IPAddress hostAddress =
                            ( Dns.Resolve( IPAddress.Any.ToString() ) ).AddressList[0];
                        IPEndPoint endPoint = new IPEndPoint( hostAddress, _listeningPort );

                        _serverSocket.Bind( endPoint );

                        _serverSocket.Listen( 30 );

                        _serverSocket.BeginAccept( HandleNewConnect, null );

                        //_worker = new Thread(WorkerThread)
                        //          {
                        //              IsBackground = true,
                        //              Name = "Proxy server worker thread"
                        //          };
                        //_worker.Start();
                    }
                }
                finally
                {
                    _shutdownMutex.ReleaseMutex();
                }
            }
            else
            {
                throw new ApplicationException(
                    "Unable to enter critical section. Server cannot start." );
            }
        }

        public void Stop()
        {
            if ( _shutdownMutex.WaitOne( 5000 ) )
            {
                try
                {
                    //_shutdown.Set();

                    //if ( !_worker.Join( 5000 ) )
                    //{
                    //    _worker.Abort();
                    //}

                    _serverSocket.Close();
                }
                finally
                {
                    _shutdownMutex.ReleaseMutex();
                }
            }
            else
            {
                throw new ApplicationException(
                    "Unable to enter critical section to terminate proxy server." );
            }
        }

        #endregion

        private void WorkerThread()
        {
            while ( !_shutdown.WaitOne( 1000 ) )
            {
                DateTime cutoffDate = DateTime.Now.AddSeconds( -30 );

                foreach ( IClientSession session in _activeSessions.ToArray() )
                {
                    if ( session.IsActive )
                    {
                        if ( session.LastActivity < cutoffDate )
                        {
                            session.EndSession(false);
                            _activeSessions.Remove( session );
                        }
                    }
                    else
                    {
                        _activeSessions.Remove( session );
                    }
                }
            }
        }

        private void HandleServerConnect( IAsyncResult ar )
        {
            ClientSession session = ar.AsyncState as ClientSession;

            if ( session == null )
            {
                throw new ArgumentException();
            }

            Log.Info("{0} Connected to remote host", session.SessionId);

            try
            {
                session.ServerSocket.EndConnect( ar );

                session.ActiveSocket = session.ServerSocket;

                session.ServerSocket.BeginSend( session.Buffer,
                                                0,
                                                session.Buffer.Length,
                                                SocketFlags.None,
                                                HandleSend,
                                                session );
            }
            catch ( Exception ex )
            {
                Log.Exception(
                     session.SessionId +  " An error was encountered when attempting to connect to the remote host.", ex );
                session.EndSession(true);
            }
        }

        private void HandleSend( IAsyncResult ar )
        {
            ClientSession session = ar.AsyncState as ClientSession;

            if ( session == null )
            {
                throw new ApplicationException(
                    "Internal error. Client session was of an unexpected type." );
            }

            try
            {
                SocketError socketError;

                long bytesSent = session.ActiveSocket.EndSend( ar, out socketError );

                if ( bytesSent == 0 )
                {
                    Log.Error("{0} Network send failure.", session.SessionId);
                    session.EndSession(true);
                }
                if ( socketError != SocketError.Success )
                {
                    Log.Error( "{0} Socket error encountered while sending data. {1}",
                        session.SessionId, socketError );
                    session.EndSession(true);
                }
                else
                {
                    // Sending data to server...
                    if ( session.ActiveSocket
                         == session.ServerSocket )
                    {
                        Log.Info("{0} Remote host send complete", session.SessionId);

                        session.ResetBuffer();

                        session.ActiveSocket.BeginReceive( session.Buffer,
                                                           0,
                                                           session.Buffer.Length,
                                                           SocketFlags.None,
                                                           HandleReceive,
                                                           session );
                    }
                    else
                    {
                        Log.Info("{0} Proxy client send complete", session.SessionId);

                        IHttpMessage message;

                        session.EndSession(false);

                        if (session.HttpMessageParser.TryGetCompleteMessage(out message))
                        {
                            IHttpResponseMessage response = message as IHttpResponseMessage;

                            if (response == null)
                            {
                                session.EndSession(true);
                                Log.Error(
                                    "{0} The HTTP response was invalid. Expected response when evaluating close.", session.SessionId);
                            }
                            else
                            {
                                Log.Info("{0} Closing client connection", session.SessionId);
                                session.EndSession(false);


                                // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html#sec19.6.2
                                // See RFC 8.1.3 - proxy servers must not establish a HTTP/1.1 persistent connection with 1.0 client. For
                                // now, all 1.0 clients will not get persistent connections from the proxy.
                                if (response.Version == "1.0")
                                {
                                    Log.Info("{0} Closing client connection", session.SessionId);
                                    session.EndSession(false);
                                }

                                    // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec8.html#sec8.1
                                else if (response.Version == "1.1")
                                {
                                    KeyValuePair<string, string> connectionHeader =
                                        response.Headers.SingleOrDefault(
                                            s =>
                                            s.Key.Equals("Connection",
                                                          StringComparison.
                                                              InvariantCultureIgnoreCase));

                                    // Closing the connection if status code != 200 is not part of the HTTP standard,
                                    // at least as far as I can tell, but things don't work correctly if this is not done.
                                    if (response.StatusCode != 200 ||
                                        (!connectionHeader.Equals(
                                            default(KeyValuePair<string, string>))
                                        &&
                                        connectionHeader.Value.Equals("close",
                                                                       StringComparison.
                                                                           InvariantCultureIgnoreCase)))
                                    {
                                        Log.Info("{0} Closing client connection", session.SessionId);
                                        session.EndSession(false);
                                    }
                                }
                                else
                                {
                                    Log.Info("{0} Closing client connection", session.SessionId);
                                    session.EndSession(false);
                                }
                            }
                        }
                    }
                }
            }
            catch ( Exception ex )
            {
                Log.Exception( session.SessionId + " Failed to send HTTP data.", ex );

                session.EndSession(true);
            }
        }

        private void HandleNewConnect( IAsyncResult ar )
        {
            try
            {
                Socket clientSocket = _serverSocket.EndAccept( ar );

                // Immediately listen for the next clientSession
                _serverSocket.BeginAccept( HandleNewConnect, null );

                ClientSession client = new ClientSession( clientSocket );

                Log.Info("{0} New clientSession connection", client.SessionId);

                client.ActiveSocket.BeginReceive(client.Buffer,
                                                  0,
                                                  client.Buffer.Length,
                                                  SocketFlags.None,
                                                  HandleReceive,
                                                  client );
            }
            catch ( Exception ex )
            {
                Log.Exception( "Unable to service new clientSession connection.", ex );
            }
        }

        private void HandleReceive( IAsyncResult ar )
        {
            ClientSession client = ar.AsyncState as ClientSession;
            if ( client == null )
            {
                throw new ApplicationException(
                    "Internal error. Client session was of an unexpected type." );
            }

            Log.Info("{0} Received data", client.SessionId);

            try
            {
                int bytesReceived = client.ActiveSocket.EndReceive( ar );

                if ( bytesReceived > 0 )
                {
                    IHttpMessage httpMessage =
                        client.HttpMessageParser.AppendData( client.Buffer.Take( bytesReceived ) );

                    if ( httpMessage != null )
                    {
                        if ( httpMessage is HttpRequestMessage )
                        {
                            HttpRequestMessage requestMessage = httpMessage as HttpRequestMessage;

                            EventHandler<ClientRequestArgs> clientMessagePosted =
                                ClientMessagePosted;
                            if ( clientMessagePosted != null )
                            {
                                clientMessagePosted( this,
                                                     new ClientRequestArgs( requestMessage, client ) );
                            }
                        }
                        else if ( httpMessage is HttpResponseMessage )
                        {
                            client.ActiveSocket = client.ClientSocket;

                            HttpResponseMessage responseMessage = httpMessage as HttpResponseMessage;

                            EventHandler<ServerResponseArgs> serverResponsePosted =
                                ServerResponsePosted;
                            if ( serverResponsePosted != null )
                            {
                                serverResponsePosted( this,
                                                      new ServerResponseArgs( responseMessage,
                                                                              client ) );
                            }
                        }
                        else
                        {
                            throw new DataException(
                                "The HTTP message was not recognized as a request or response" );
                        }
                    }
                    else
                    {
                        client.ActiveSocket.BeginReceive( client.Buffer,
                                                          0,
                                                          client.Buffer.Length,
                                                          SocketFlags.None,
                                                          HandleReceive,
                                                          client );
                    }
                }
                else
                {
                    Log.Warning( "{0} Proxy server received no data from the client", client.SessionId );
                }
            }
            catch ( Exception ex )
            {
                Log.Exception( client.SessionId + " An error occurred while receiving data over the network.", ex );
                client.EndSession(true);
            }
        }
    }
}