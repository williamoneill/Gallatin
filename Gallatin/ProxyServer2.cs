using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Gallatin.Core
{
    public class ProxyServer
    {
        private Socket _serverSocket;

        public ProxyServer( int port, IHttpClient remoteServer )
        {
            if ( remoteServer == null )
            {
                throw new ArgumentNullException( "remoteServer" );
            }

            _remoteServerProxy = remoteServer;

            Port = port;
        }

        public int Port { get; private set; }

        public void Start()
        {
            try
            {
                if ( _serverSocket == null )
                {
                    _serverSocket = new Socket( AddressFamily.InterNetwork,
                                                SocketType.Stream,
                                                ProtocolType.Tcp );

                    IPAddress hostAddress =
                        ( Dns.Resolve( IPAddress.Any.ToString() ) ).AddressList[0];
                    IPEndPoint endPoint = new IPEndPoint( hostAddress, Port );

                    _serverSocket.Bind( endPoint );

                    _serverSocket.Listen( 10 );

                    _serverSocket.BeginAccept( HandleNewConnect, null );
                }
            }
            catch ( Exception ex )
            {
                Trace.TraceError( "Unable to start server. {0}", ex.Message );
                throw;
            }
        }

        public void Stop()
        {
            if ( _serverSocket != null )
            {
                _serverSocket.Close();
                _serverSocket = null;
            }
        }


        private void HandleNewConnect( IAsyncResult ar )
        {
            Trace.TraceInformation( "New clientSession connection" );

            try
            {
                Socket clientSocket = _serverSocket.EndAccept( ar );

                // Immediately listen for the next clientSession
                _serverSocket.BeginAccept( HandleNewConnect, null );

                ProxyClient client = new ProxyClient( clientSocket );

                client.ClientSocket.BeginReceive( client.Buffer,
                                                  0,
                                                  client.Buffer.Length,
                                                  SocketFlags.None,
                                                  HandleReceive,
                                                  client );
            }
            catch ( Exception ex )
            {
                Trace.TraceError( "Unable to service new clientSession connection. {0}", ex.Message );
            }
        }

        private void HandleSend( IAsyncResult ar )
        {
            ProxyClient client = ar.AsyncState as ProxyClient;

            Trace.Assert( client != null );

            try
            {
                SocketError error;

                client.ClientSocket.EndSend( ar, out error );

                if ( error != SocketError.Success )
                {
                    Trace.TraceError( "Failed to send clientSession data. {0}", (int) error );
                }

                client.ClientSocket.Close();
            }
            catch ( Exception ex )
            {
                Trace.TraceError( "Unable to send data to clientSession. {0}", ex.Message );
            }
        }

        private void HandleReturnFromServer( HttpResponse response, ProxyClient client )
        {
            try
            {
                // See http://west-wind.com/presentations/dotnetwebrequest/dotnetwebrequest.htm
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendFormat( "HTTP/{0} {1} {2}\r\n",
                                            response.Version,
                                            response.ResponseCode,
                                            response.Status );

                foreach ( KeyValuePair<string, string> headerPair in response.HeaderPairs )
                {
                    stringBuilder.AppendFormat( "{0}: {1}\r\n", headerPair.Key, headerPair.Value );
                }

                // End header
                stringBuilder.Append( "\r\n" );

                List<byte> bufferList = new List<byte>();

                bufferList.AddRange( Encoding.GetEncoding( 1252 ).GetBytes( stringBuilder.ToString() ) );

                if ( response.Body != null )
                {
                    bufferList.AddRange( response.Body );
                }

                //Trace.TraceInformation( Encoding.GetEncoding( 1252 ).GetString( bufferList.ToArray() ) );

                Trace.TraceInformation( "Sending response to original clientSession" );

                client.ClientSocket.BeginSend( bufferList.ToArray(),
                                               0,
                                               bufferList.Count,
                                               SocketFlags.None,
                                               HandleSend,
                                               client );
            }
            catch ( Exception ex )
            {
                Trace.TraceError(
                    "An error occurred while processing the response from the remote server. {0}",
                    ex.Message );

                try
                {
                    client.ClientSocket.Close();
                }
                catch ( Exception closeError )
                {
                    Trace.TraceError( "Unable to close the clientSession socket. {0}", ex.Message );
                }
            }
        }

        private void HandleReceive( IAsyncResult ar )
        {
            try
            {
                ProxyClient client = ar.AsyncState as ProxyClient;

                Trace.Assert( client != null );

                int bytesReceived = client.ClientSocket.EndReceive( ar );

                if ( bytesReceived > 0 )
                {
                    client.ContentStream.Write( client.Buffer, 0, bytesReceived );

                    HttpMessageOld message;

                    if ( HttpContentParser.TryParse( client.ContentStream, out message ) )
                    {
                        HttpRequest request = message as HttpRequest;

                        Trace.Assert( request != null );

                        _remoteServerProxy.BeginWebRequest( request,
                                                            HandleReturnFromServer,
                                                            client );
                    }
                    else
                    {
                        client.ClientSocket.BeginReceive( client.Buffer,
                                                          0,
                                                          client.Buffer.Length,
                                                          SocketFlags.None,
                                                          HandleReceive,
                                                          client );
                    }
                }
                else
                {
                    Trace.Write( "foo" );
                }
            }
            catch ( Exception ex )
            {
                Trace.TraceError( "Unable to receive data from clientSession. {0}", ex.Message );
            }
        }
    }
}