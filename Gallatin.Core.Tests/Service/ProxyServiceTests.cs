using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Gallatin.Core.Client;
using Gallatin.Core.Service;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ProxyServiceTests
    {
        public class RequestResponsePair
        {
            public bool IsPersistent;
            public bool CloseServerSocketAfterSend;
            public byte[] Request;
            public byte[] Response;
        }

        public class RemoteHost
        {
            private List<RequestResponsePair> _responseMessages;
            private bool _success;
            private Thread _thread;
            private TcpListener tcpListener;

            private void Worker()
            {
                TcpClient clientSocket = null;

                foreach ( RequestResponsePair pair in _responseMessages )
                {
                    byte[] data = new byte[10000];

                    if ( clientSocket == null )
                    {
                        clientSocket = tcpListener.AcceptTcpClient();
                    }

                    NetworkStream stream = clientSocket.GetStream();

                    int bytesReceived = stream.Read( data, 0, data.Length );

                    if ( bytesReceived == pair.Request.Length )
                    {
                        for ( int i = 0; i < bytesReceived; i++ )
                        {
                            if ( data[i]
                                 != pair.Request[i] )
                            {
                                return;
                            }
                        }
                    }

                    stream.Write( pair.Response, 0, pair.Response.Length );

                    if ( !pair.IsPersistent )
                    {
                        clientSocket.Close();
                        clientSocket = null;
                    }
                }

                _success = true;
            }

            public void Start( List<RequestResponsePair> responseMessages )
            {
                _responseMessages = responseMessages;

                tcpListener = new TcpListener( Dns.GetHostEntry( "127.0.0.1" ).AddressList[0], 80 );
                tcpListener.Start();

                _thread = new Thread( Worker );
                _thread.Start();
            }

            public bool Stop()
            {
                tcpListener.Stop();
                _thread.Join();
                return _success;
            }
        }

        public class WebClient
        {
            private List<RequestResponsePair> _pairs;
            private bool _success;
            private Thread _thread;

            private void Worker()
            {
                TcpClient tcpClient = null;

                foreach ( RequestResponsePair requestResponsePair in _pairs )
                {
                    if ( tcpClient == null )
                    {
                        tcpClient = new TcpClient( "127.0.0.1", 8080 );
                    }

                    NetworkStream networkStream = tcpClient.GetStream();
                    networkStream.Write( requestResponsePair.Request,
                                         0,
                                         requestResponsePair.Request.Length );

                    byte[] data = new byte[10000];
                    int bytesRead = networkStream.Read( data, 0, data.Length );

                    if ( bytesRead == requestResponsePair.Response.Length )
                    {
                        for ( int i = 0; i < bytesRead; i++ )
                        {
                            if ( data[i]
                                 != requestResponsePair.Response[i] )
                            {
                                return;
                            }
                        }
                    }

                    if ( !requestResponsePair.IsPersistent )
                    {
                        tcpClient.Close();
                        tcpClient = null;
                    }
                }

                _success = true;
            }

            public void Start( List<RequestResponsePair> responsePairs )
            {
                _pairs = responsePairs;
                _thread = new Thread( Worker );
                _thread.Start();
            }

            public bool Stop()
            {
                _thread.Join();
                return _success;
            }
        }

        public class MockProxyClient : IProxyClient
        {
            private readonly List<RequestResponsePair> _pairs;
            private int _counter;
            private INetworkService _networkService;

            public MockProxyClient( List<RequestResponsePair> pairs )
            {
                _pairs = pairs;
            }

            #region IProxyClient Members

            public void ServerSendComplete()
            {
                _networkService.GetDataFromRemoteHost( this );
            }

            public void ClientSendComplete()
            {
                _networkService.GetDataFromRemoteHost( this );

                if( _pairs[_counter].IsPersistent )
                    _networkService.GetDataFromClient(this);

                _counter++;
            }

            public void NewDataAvailableFromServer( byte[] data )
            {
                IHttpMessageParser parser = new HttpMessageParser();
                IHttpMessage message = parser.AppendData( _pairs[_counter].Response );
                IHttpResponseMessage responseMessage = message as IHttpResponseMessage;
                _networkService.SendClientMessage( this, responseMessage.CreateHttpMessage() );
            }

            public void NewDataAvailableFromClient( byte[] data )
            {
                IHttpMessageParser parser = new HttpMessageParser();
                IHttpMessage message = parser.AppendData( _pairs[_counter].Request );
                IHttpRequestMessage requestMessage = message as IHttpRequestMessage;
                _networkService.SendServerMessage( this,
                                                   requestMessage.CreateHttpMessage(),
                                                   requestMessage.Destination.Host,
                                                   requestMessage.Destination.Port );
            }

            public void StartSession( INetworkService networkService )
            {
                _networkService = networkService;
                _networkService.GetDataFromClient( this );
            }

            #endregion

            public void EndSession()
            {
            }
        }

        [Test]
        public void VerifyServerSocketClose()
        {
            List<RequestResponsePair> list = new List<RequestResponsePair>();

            RequestResponsePair pair = new RequestResponsePair();
            pair.Request = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
            pair.Response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
            pair.IsPersistent = true;
            pair.CloseServerSocketAfterSend = true;
            list.Add(pair);

            Mock<IProxyClientFactory> mockFactory = new Mock<IProxyClientFactory>();
            mockFactory.Setup(s => s.CreateClient()).Returns(new MockProxyClient(list));

            // Start proxy server under test
            ProxyService service = new ProxyService(mockFactory.Object);
            service.Start(8080);

            // Start the remote host
            RemoteHost remoteHost = new RemoteHost();
            remoteHost.Start(list);

            // Send request to proxy server from the web client
            WebClient client = new WebClient();
            client.Start(list);

            // Verify
            Assert.That(client.Stop(), Is.True);
            Assert.That(remoteHost.Stop(), Is.True);
            
            service.Stop();
        }

        [Test]
        public void VerifyPersistentConnection()
        {
            List<RequestResponsePair> list = new List<RequestResponsePair>();

            RequestResponsePair pair = new RequestResponsePair();
            pair.Request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n" );
            pair.Response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n" );
            pair.IsPersistent = true;
            list.Add( pair );

            RequestResponsePair pair2 = new RequestResponsePair();
            pair2.Request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n" );
            pair2.Response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n" );
            pair2.IsPersistent = false;
            list.Add( pair2 );

            Mock<IProxyClientFactory> mockFactory = new Mock<IProxyClientFactory>();
            mockFactory.Setup( s => s.CreateClient() ).Returns( new MockProxyClient( list ) );

            // Start proxy server under test
            ProxyService service = new ProxyService( mockFactory.Object );
            service.Start( 8080 );

            // Start the remote host
            RemoteHost remoteHost = new RemoteHost();
            remoteHost.Start( list );

            // Send request to proxy server from the web client
            WebClient client = new WebClient();
            client.Start( list );

            // Verify
            Assert.That( client.Stop(), Is.True );
            Assert.That( remoteHost.Stop(), Is.True );

            service.Stop();
        }

        [Test]
        public void VerifySuccessfulTransaction()
        {
            List<RequestResponsePair> list = new List<RequestResponsePair>();

            RequestResponsePair pair = new RequestResponsePair();
            pair.Request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n" );
            pair.Response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n" );
            list.Add( pair );

            Mock<IProxyClientFactory> mockFactory = new Mock<IProxyClientFactory>();
            mockFactory.Setup( s => s.CreateClient() ).Returns( new MockProxyClient( list ) );

            // Start proxy server under test
            ProxyService service = new ProxyService( mockFactory.Object );
            service.Start( 8080 );

            // Start the remote host
            RemoteHost remoteHost = new RemoteHost();
            remoteHost.Start( list );

            // Send request to proxy server from the web client
            WebClient client = new WebClient();
            client.Start( list );

            // Verify
            Assert.That( client.Stop(), Is.True );
            Assert.That( remoteHost.Stop(), Is.True );

            service.Stop();
        }
    }
}