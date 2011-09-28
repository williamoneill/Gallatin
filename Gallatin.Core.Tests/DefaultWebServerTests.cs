using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;

namespace Gallatin.Core.Tests
{
    [TestFixture]
    public class DefaultWebServerTests
    {
        /// <summary>
        /// Simple class used to mock a web server
        /// </summary>
        private class _SimpleWebServer
        {
            private Socket _serverSocket;
            private byte[] _expectedData;
            private byte[] _dataToReturn;
            private Thread _worker;

            public _SimpleWebServer( int port, byte [] expectedData, byte [] dataToReturn )
            {
                _expectedData = expectedData;
                _dataToReturn = dataToReturn;

                _serverSocket = new Socket(AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp);

                IPHostEntry hostEntry = Dns.GetHostEntry( "127.0.0.1" );
                IPAddress hostAddress = hostEntry.AddressList[0];
                IPEndPoint endPoint = new IPEndPoint(hostAddress, port);

                _serverSocket.Bind(endPoint);

                _serverSocket.Listen(10);

                _worker = new Thread(Accept);
                _worker.Start();
            }


            private void Accept()
            {
                Socket client = _serverSocket.Accept();

                byte[] buffer = new byte[10000];

                int bytesReceived = client.Receive(buffer, 0, 10000, SocketFlags.None);

                Assert.That( bytesReceived, Is.EqualTo(_expectedData.Length) );

                for (int i = 0; i < bytesReceived; i++ )
                    Assert.That( buffer[i], Is.EqualTo( _expectedData[i] ) );

                client.Send(_dataToReturn, 0, _dataToReturn.Length, SocketFlags.None);

                client.Close();
                _serverSocket.Close();
                
            }

            public void Stop()
            {
                _worker.Join();
            }
        }

        /// <summary>
        /// Verifies that the correct data is sent to the server is correct, and the data
        /// returned from the server is parsed correctly.
        /// </summary>
        [Test]
        public void VerifyDataSentToServerTest()
        {
            byte[] dataToSend = File.ReadAllBytes( ".\\testdata\\LocalHostRequest.txt" );
            byte[] dataExpected = File.ReadAllBytes( ".\\testdata\\samplehttpresponse.txt" );

            // Hold the test thread until the response is received
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            // Callback to verify the data was received from the server
            Action<HttpResponse, ProxyClient> callback = delegate(HttpResponse r, ProxyClient c)
            {
                byte[] dataFromServer = new byte[r.OriginalStream.Length];
                r.OriginalStream.Position = 0;
                r.OriginalStream.Read( dataFromServer, 0, dataFromServer.Length );

                Assert.That(dataFromServer, Is.EqualTo(dataExpected));
                resetEvent.Set();
            };

            
            HttpMessage message;

            MemoryStream stream = new MemoryStream();
            stream.Write(dataToSend,0,dataToSend.Length);

            Assert.That( HttpContentParser.TryParse( stream, out message), Is.True);

            HttpRequest request = message as HttpRequest;

            _SimpleWebServer simpleWeb = new _SimpleWebServer(80, dataToSend, dataExpected);

            HttpClient server = new HttpClient();

            server.BeginWebRequest(request, callback, new ProxyClient());

            Assert.That(resetEvent.WaitOne(1000), Is.True);

            simpleWeb.Stop();

        }

    }
}