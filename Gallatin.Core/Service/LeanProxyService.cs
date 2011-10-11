using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    public class LeanProxyService : IProxyService
    {
        private const int BufferSize = 8192;
        private Socket _serverSocket;

        #region IProxyService Members

        private ICoreSettings _settings;

        [ImportingConstructor]
        public LeanProxyService(ICoreSettings settings)
        {
            _settings = settings;
        }

        public void Start( int port )
        {
            _serverSocket = new Socket( AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp );

            var dnsEntry = Dns.GetHostEntry("localhost");

            IPEndPoint endPoint = new IPEndPoint( dnsEntry.AddressList[_settings.NetworkAddressBindingOrdinal], port );

            _serverSocket.Bind( endPoint );

            _serverSocket.Listen( 30 );

            _serverSocket.BeginAccept( HandleNewClientConnect, null );
        }

        public void Stop()
        {
        }

        #endregion

        private bool TryGetHostAddress( ProxySession session,
                                        out string host,
                                        out int port,
                                        out bool isSsl )
        {
            host = null;
            port = 0;
            isSsl = false;

            IHttpMessage message;
            if ( session.ClientMessageParser.TryGetHeader( out message ) )
            {
                IHttpRequestMessage requestMessage = message as IHttpRequestMessage;
                if ( requestMessage == null )
                {
                    Log.Error( "{0} Client did not send valid HTTP request", session );
                    EndSession( session, true );
                    return false;
                }

                isSsl = requestMessage.Method.Equals( "CONNECT",
                                                      StringComparison.
                                                          InvariantCultureIgnoreCase );

                host = requestMessage.Destination.Host;
                port = requestMessage.Destination.Port;

                // Watch for non-standard port
                if ( port == -1 )
                {
                    const int HTTPS_PORT = 443;
                    const int SNEWS_PORT = 563;

                    string[] tokens = requestMessage.Destination.AbsoluteUri.Split( ':' );
                    if ( tokens.Length == 2 )
                    {
                        host = tokens[0];
                        port = int.Parse( tokens[1] );
                    }

                    // Only allow SSL on well-known ports. This is the general guidance for HTTPS.
                    if ( port != HTTPS_PORT
                         && port != SNEWS_PORT && isSsl )
                    {
                        Log.Error(
                            "{0} Client attempted to connect via SSL to an unsupported port {1}",
                            session,
                            port );
                        EndSession( session, true );
                        return false;
                    }
                }
            }

            return true;
        }

        private void HandleConnectToServer( IAsyncResult ar )
        {
            ProxyRequest request = ar.AsyncState as ProxyRequest;
            Trace.Assert( request != null );

            try
            {
                request.Session.ServerSocket.EndConnect( ar );

                Log.Info("{0} Connection established. Sending data to remote host.",
                            request.Session.Id);

                ProxyRequest newRequest = new ProxyRequest(request.Session);

                IHttpMessage message;
                if (newRequest.Session.ClientMessageParser.TryGetCompleteMessage(
                        out message))
                {
                    if(request.Session.IsSsl)
                    {
                        SslTunnel sslTunnel = new SslTunnel(request.Session.ClientSocket, request.Session.ServerSocket, message.Version, request.Session.Id);
                        sslTunnel.EstablishTunnel();
                    }
                    else
                    {
                        newRequest.Buffer = message.CreateHttpMessage();

                        newRequest.Session.ServerSocket.BeginSend(
                            newRequest.Buffer,
                            0,
                            newRequest.Buffer.Length,
                            SocketFlags.None,
                            HandleDataSentToServer,
                            newRequest);
                    }
                }
            }
            catch ( Exception ex )
            {
                if ( request.Session.IsActive )
                {
                    Log.Exception(
                        request.Session.Id
                        + " An error occurred while trying to connect to remote host. ",
                        ex );
                    EndSession( request.Session, true );
                }
            }
        }

        private void HandleDataSentToClient( IAsyncResult ar )
        {
            ProxyRequest request = ar.AsyncState as ProxyRequest;
            Trace.Assert( request != null );

            try
            {
                SocketError socketError;
                int dataSent = request.Session.ClientSocket.EndSend( ar, out socketError );

                if ( socketError != SocketError.Success )
                {
                    Log.Error( "{0} Unable to send message to client: {1}",
                               request.Session.Id,
                               socketError );
                    EndSession( request.Session, true );
                }
                else if ( dataSent == 0 )
                {
                    Log.Warning( "{0} 0 bytes sent to client. Closing session.", request.Session.Id );
                    EndSession( request.Session, false );
                }
                else
                {
                    Log.Info( "{0} Completed data send to client.", request.Session.Id );

                    IHttpMessage message;

                    // TODO: add overload so we don't have to type check
                    if ( request.Session.ServerMessageParser.TryGetCompleteMessage( out message ) )
                    {
                        IHttpResponseMessage responseMessage = message as IHttpResponseMessage;
                        if ( responseMessage == null )
                        {
                            Log.Error( "{0} Server did not return a valid HTTP response",
                                       request.Session.Id );
                            EndSession( request.Session, true );
                        }
                        else
                        {
                            string connectionValue = responseMessage["connection"];
                            if ( connectionValue == null
                                 ||
                                 connectionValue.Equals( "close",
                                                         StringComparison.
                                                             InvariantCultureIgnoreCase ) )
                            {
                                Log.Info( "{0} Closing HTTP connection", request.Session.Id );
                                EndSession( request.Session, false );
                            }
                            else
                            {
                                Log.Verbose( "{0} Maintaining persistent connection",
                                             request.Session.Id );

                                // Reset the buffers for next message
                                request.Session.ClientMessageParser = new HttpMessageParser();
                                request.Session.ServerMessageParser = new HttpMessageParser();

                                ProxyRequest newRequest = new ProxyRequest( request.Session );

                                newRequest.Session.ClientSocket.BeginReceive(
                                    newRequest.Buffer,
                                    0,
                                    newRequest.Buffer.
                                        Length,
                                    SocketFlags.None,
                                    HandleDataFromClient,
                                    newRequest );
                            }
                        }
                    }

                    //// Now that the client has received the data, begin to receive more from the server
                    //ProxyRequest newRequest = new ProxyRequest( request.Session );
                    //newRequest.Buffer = new byte[BufferSize];
                    //newRequest.Session.ServerSocket.BeginReceive(
                    //    newRequest.Buffer,
                    //    0,
                    //    newRequest.Buffer.Length,
                    //    SocketFlags.None,
                    //    HandleDataFromServer,
                    //    newRequest );
                }
            }
            catch ( Exception ex )
            {
                if ( request.Session.IsActive )
                {
                    Log.Exception(
                        request.Session.Id
                        + " An error was received when sending message to the client.",
                        ex );
                    EndSession( request.Session, true );
                }
            }
        }

        private void HandleDataFromServer( IAsyncResult ar )
        {
            ProxyRequest request = ar.AsyncState as ProxyRequest;
            Trace.Assert( request != null );

            try
            {
                int bytesReceived = request.Session.ServerSocket.EndReceive( ar );

                if ( bytesReceived > 0 )
                {
                    Log.Info( "{0} Data received from remote host. Length {1}.",
                              request.Session.Id,
                              bytesReceived );

                    IHttpMessage message = request.Session.ServerMessageParser.AppendData(
                        request.Buffer.Take( bytesReceived ) );

                    if ( message != null )
                    {
                        Log.Verbose(
                            () =>
                            string.Format( "{0} Data from server:\r\n{1}",
                                           request.Session.Id,
                                           Encoding.UTF8.GetString(
                                               request.Buffer.Take( bytesReceived ).ToArray
                                                   () ) ) );

                        // Send to client
                        ProxyRequest newRequest = new ProxyRequest( request.Session );
                        newRequest.Buffer = message.CreateHttpMessage();
                        newRequest.Session.ClientSocket.BeginSend(
                            newRequest.Buffer,
                            0,
                            newRequest.Buffer.Length,
                            SocketFlags.None,
                            HandleDataSentToClient,
                            newRequest );
                    }
                    else
                    {
                        ProxyRequest newRequest = new ProxyRequest( request.Session );
                        newRequest.Session.ServerSocket.BeginReceive(
                            newRequest.Buffer,
                            0,
                            newRequest.Buffer.Length,
                            SocketFlags.None,
                            HandleDataFromServer,
                            newRequest );
                    }
                }
                else
                {
                    // Server shutdown.
                    Log.Info( "{0} Remote host is shutting down.", request.Session.Id );
                    EndSession( request.Session, false );
                }
            }
            catch ( Exception ex )
            {
                if ( request.Session.IsActive )
                {
                    Log.Exception(
                        request.Session.Id
                        + " An error was encountered when receiving data from the remote host.",
                        ex );
                    EndSession( request.Session, true );
                }
            }
        }

        private void HandleDataSentToServer( IAsyncResult ar )
        {
            ProxyRequest request = ar.AsyncState as ProxyRequest;
            Trace.Assert( request != null );

            try
            {
                SocketError socketError;
                int dataSent = request.Session.ServerSocket.EndSend( ar, out socketError );

                if ( socketError != SocketError.Success )
                {
                    Log.Error( request.Session.Id
                               + " An error occurred while sending data to the remote host. "
                               + socketError );
                    EndSession( request.Session, true );
                }
                else if ( dataSent == 0 )
                {
                    Log.Warning( "{0} 0 bytes sent to server. Closing session.", request.Session.Id );
                    EndSession( request.Session, false );
                }
                else
                {
                    Log.Info( "{0} Data sent to remote host", request.Session.Id );

                    // Prepare to read response from server
                    ProxyRequest newRequest = new ProxyRequest( request.Session );
                    request.Session.ServerSocket.BeginReceive(
                        newRequest.Buffer,
                        0,
                        newRequest.Buffer.Length,
                        SocketFlags.None,
                        HandleDataFromServer,
                        newRequest );

                    // Now that the server has received the data, accept more data from the client
                    //ProxyRequest newClientRequest = new ProxyRequest( request.Session );
                    //newClientRequest.Buffer = new byte[BufferSize];
                    //newClientRequest.Session.ClientSocket.BeginReceive(
                    //    newClientRequest.Buffer,
                    //    0,
                    //    newClientRequest.Buffer.Length,
                    //    SocketFlags.None,
                    //    HandleDataFromClient,
                    //    newClientRequest );
                }
            }
            catch ( Exception ex )
            {
                if ( request.Session.IsActive )
                {
                    Log.Exception(
                        request.Session.Id
                        + " An error occurred while sending data to the remote host.",
                        ex );
                    EndSession( request.Session, true );
                }
            }
        }

        private void SendDataToServer( ProxyRequest request )
        {
            try
            {
                string host;
                int port;
                bool isSsl;
                if ( TryGetHostAddress( request.Session, out host, out port, out isSsl ) )
                {
                    if ( request.Session.ServerSocket == null || request.Session.Host != host
                         || request.Session.Port != port )
                    {
                        if ( request.Session.ServerSocket != null
                             && request.Session.ServerSocket.Connected )
                        {
                            request.Session.ServerSocket.Shutdown( SocketShutdown.Both );
                            request.Session.ServerSocket.Close();
                        }

                        // TODO: remove --  for debug only
                        if(host.Contains("127.0.0.1"))
                        {
                            Log.Verbose("{0} Found local host", request.Session.Id);

                            IHttpMessage message;
                            request.Session.ClientMessageParser.TryGetCompleteMessage( out message );
                            
                            Log.Verbose(Encoding.UTF8.GetString(message.CreateHttpMessage()));
                        }

                        request.Session.Host = host;
                        request.Session.Port = port;
                        request.Session.IsSsl = isSsl;

                        request.Session.ServerSocket =
                            new Socket( AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp );

                        Log.Verbose( "{0} Connecting to remote host: {1}:{2}",
                                     request.Session.Id,
                                     host,
                                     port );

                        request.Session.ServerSocket.BeginConnect(
                            host,
                            port,
                            HandleConnectToServer,
                            request );
                    }
                    else
                    {
                        ProxyRequest newRequest = new ProxyRequest( request.Session );

                        IHttpMessage message;
                        if ( newRequest.Session.ClientMessageParser.TryGetCompleteMessage(
                                out message ) )
                        {
                            newRequest.Buffer = message.CreateHttpMessage();

                            newRequest.Session.ServerSocket.BeginSend(
                                newRequest.Buffer,
                                0,
                                newRequest.Buffer.Length,
                                SocketFlags.None,
                                HandleDataSentToServer,
                                newRequest );
                        }
                    }
                }
                else
                {
                    Log.Error( "Unable to determine host address" );
                    EndSession( request.Session, true );
                }
            }
            catch ( Exception ex )
            {
                Log.Exception( request.Session.Id + " Error sending data to server", ex );
            }
        }

        public void HandleDataFromClient( IAsyncResult ar )
        {
            ProxyRequest request = ar.AsyncState as ProxyRequest;
            Trace.Assert( request != null );

            try
            {
                int bytesReceived = request.Session.ClientSocket.EndReceive( ar );

                if ( bytesReceived > 0 )
                {
                    Log.Info( "{0} Data received from client -- {1} bytes",
                              request.Session.Id,
                              bytesReceived );

                    IHttpMessage message = request.Session.ClientMessageParser.AppendData(
                        request.Buffer.Take( bytesReceived ) );

                    if ( message != null )
                    {
                        // TODO: check message type

                        // Forward the data just received to server
                        SendDataToServer( request );
                    }
                }
                else
                {
                    Log.Info(
                        "{0} No data received from client. Client closed connection. Terminating proxy session.",
                        request.Session.Id );
                    EndSession( request.Session, false );
                }
            }
            catch ( Exception ex )
            {
                if ( request.Session.IsActive )
                {
                    Log.Exception(
                        request.Session.Id
                        + " An error was encountered when receiving data from the client.",
                        ex );
                    EndSession( request.Session, true );
                }
            }
        }

        private void EndSession( ProxySession session, bool inError )
        {
            session.EndSession( inError );
        }

        private void HandleNewClientConnect( IAsyncResult ar )
        {
            try
            {
                // Server may be in the process of shutting down. Ignore pending connect notifications.
                if ( _serverSocket != null )
                {
                    ProxySession session = new ProxySession();
                    session.ClientSocket = _serverSocket.EndAccept( ar );

                    // Immediately listen for the next clientSession
                    _serverSocket.BeginAccept( HandleNewClientConnect, null );

                    ProxyRequest request = new ProxyRequest( session );

                    Log.Info( "{0} New client connect", session.Id );

                    session.ClientSocket.BeginReceive( request.Buffer,
                                                       0,
                                                       request.Buffer.Length,
                                                       SocketFlags.None,
                                                       HandleDataFromClient,
                                                       request );
                }
            }
            catch ( Exception ex )
            {
                Log.Exception( "Error establishing client connect", ex );
            }
        }

        #region Nested type: ProxyRequest

        private class ProxyRequest
        {
            public ProxyRequest( ProxySession session )
            {
                Session = session;
                Buffer = new byte[BufferSize];
            }

            public ProxySession Session { get; private set; }
            public byte[] Buffer { get; set; }
        }

        #endregion

        #region Nested type: ProxySession

        private class ProxySession
        {
            public ProxySession()
            {
                Id = Guid.NewGuid();
                ClientMessageParser = new HttpMessageParser();
                ServerMessageParser = new HttpMessageParser();
                AccessMutex = new Mutex( false );
                IsActive = true;
            }

            public Guid Id { get; private set; }
            public Socket ClientSocket { get; set; }
            public Socket ServerSocket { get; set; }
            public bool IsActive { get; private set; }
            public IHttpMessageParser ClientMessageParser { get; set; }
            public IHttpMessageParser ServerMessageParser { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }
            public bool IsSsl { get; set; }
            private Mutex AccessMutex { get; set; }

            public void EndSession( bool inError )
            {
                AccessMutex.WaitOne();

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
                                {
                                    ClientSocket.Send(
                                        Encoding.UTF8.GetBytes(
                                            "HTTP/1.0 500 Internal Server Error\r\nContent-Length: 11\r\n\r\nProxy error" ) );
                                }

                                ClientSocket.Shutdown( SocketShutdown.Both );
                            }

                            ClientSocket.Close();
                        }

                        if ( ServerSocket != null )
                        {
                            if ( ServerSocket.Connected )
                            {
                                ServerSocket.Shutdown( SocketShutdown.Both );
                            }

                            ServerSocket.Close();
                        }
                    }
                }
                finally
                {
                    AccessMutex.ReleaseMutex();
                }
            }
        }

        #endregion
    }
}