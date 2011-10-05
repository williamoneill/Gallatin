using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Core.Client;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;
using Gallatin.Core.Service;
using System.Net;
using System.Net.Sockets;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ProxyServiceTests
    {
        static byte[] requestData = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");
        static byte[] responseData = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");

        public class RemoteHost
        {
            private Thread _thread;
            private bool _isRunning = true;
            private bool _success = false;
            private TcpListener tcpListener;
            
            private void Worker()
            {
                byte[] data = new byte[200];
                TcpClient clientSocket = tcpListener.AcceptTcpClient();
                var stream = clientSocket.GetStream();

                int bytesReceived = stream.Read( data, 0, data.Length );

                if (bytesReceived == requestData.Length)
                {
                    for (int i = 0; i < bytesReceived; i++)
                    {
                        if( data[i] != requestData[i] )
                        {
                            return;
                        }
                    }

                    _success = true;
                }

                stream.Write( responseData, 0, responseData.Length );
            }

            public void Start()
            {
                tcpListener = new TcpListener( Dns.GetHostEntry("127.0.0.1").AddressList[0], 80);
                tcpListener.Start();

                _thread = new Thread(Worker);
                _thread.Start();
            }

            public bool Stop()
            {
                _thread.Join();
                return _success;
            }
        }

        public class WebClient
        {
            private TcpClient tcpClient;
            private NetworkStream networkStream;
            private bool _success = false;
            private Thread _thread;

            private void Worker()
            {
                byte[] data = new byte[200];
                int bytesRead = networkStream.Read(data, 0, data.Length);

                if (bytesRead == responseData.Length)
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        if (data[i] != responseData[i])
                        {
                            return;
                        }
                    }

                    _success = true;
                }
            }

            public void Start()
            {
                tcpClient = new TcpClient("127.0.0.1", 8080);
                networkStream = tcpClient.GetStream();
                networkStream.Write(requestData, 0, requestData.Length);

                _thread = new Thread(Worker);
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
            private INetworkService _networkService;
            private bool _isFirstReceive = true;

            public void SendComplete()
            {
                _isFirstReceive = false;
                _networkService.GetDataFromRemoteHost(this);
            }

            public void NewDataAvailable(IEnumerable<byte> data)
            {
                if(_isFirstReceive)
                {
                    IHttpMessage message;
                    IHttpMessageParser parser = new HttpMessageParser();
                    message = parser.AppendData(requestData);
                    IHttpRequestMessage requestMessage = message as IHttpRequestMessage;
                    this._networkService.SendMessage(this, requestMessage);
                }
                else
                {
                    IHttpMessage message;
                    IHttpMessageParser parser = new HttpMessageParser();
                    message = parser.AppendData(responseData);
                    IHttpResponseMessage responseMessage = message as IHttpResponseMessage;
                    this._networkService.SendMessage(this, responseMessage);
                }
            }

            public void StartSession(INetworkService networkService)
            {
                _networkService = networkService;
                _networkService.GetDataFromClient( this );
            }

            public void EndSession()
            {
                
            }
        }

        [Test]
        public void VerifySuccessfulTransaction()
        {
            var mockFactory = new Mock<IProxyClientFactory>();

            // Start proxy server under test
            ProxyService service = new ProxyService(mockFactory.Object);
            service.Start(8080);

            // Start the remote host
            RemoteHost remoteHost = new RemoteHost();
            remoteHost.Start();

            mockFactory.Setup( s => s.CreateClient() ).Returns( new MockProxyClient() );

            // Send request to proxy server from the web client
            WebClient client = new WebClient();
            client.Start();

            Assert.That(client.Stop(), Is.True);
            Assert.That(remoteHost.Stop(), Is.True);
        }
    }
}
