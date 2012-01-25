using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Gallatin.Core.Net;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Net
{
    [TestFixture]
    public class NetworkConnectionFactoryTests
    {
        private NetworkConnectionFactory _factory;
        private Socket _server;
        private Socket _serverClient;

        private void EndAccept(IAsyncResult ar)
        {
            _serverClient = _server.EndAccept(ar);
            _serverClient.NoDelay = true;
        }
        
        [SetUp]
        public void Setup()
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();

            _factory = new NetworkConnectionFactory(settings.Object);

            IPHostEntry dnsEntry = Dns.GetHostEntry("127.0.0.1");
            IPEndPoint endPoint =
                new IPEndPoint(dnsEntry.AddressList[0], 8080);


            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _server.Bind(endPoint);
            _server.Listen(50);
            _server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, false);
            _server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, false);
            _server.NoDelay = true;
            _server.BeginAccept(EndAccept, null);
        }

        [TearDown]
        public void Teardown()
        {
            _server.Close();
        }

        [Test]
        public void BeginConnectTest()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            int connectCount = 0;

            _factory.BeginConnect("127.0.0.1", 8080, ( b, connection ) =>
                                                     {
                                                         if (b && connection!=null)
                                                         {
                                                             connectCount++;
                                                             resetEvent.Set();
                                                         }
                                                     });

            Assert.That(resetEvent.WaitOne(2000));

            Assert.That(connectCount, Is.EqualTo(1));
        }

        [Test]
        public void ConnectInvalidHostTest()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            int connectCount = 0;

            // Nothing should be listening to the port. 
            _factory.BeginConnect("127.0.0.1", 8089, (b, connection) =>
            {
                if (!b && connection == null)
                {
                    connectCount++;
                    resetEvent.Set();
                }
            });

            Assert.That(resetEvent.WaitOne(2000));

            Assert.That(connectCount, Is.EqualTo(1));
        }

        [Test]
        public void ListenTest()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);
            int connectCount = 0;

            _factory.Listen("127.0.0.1", 4545, connection =>
                                               {
                                                   if(connection != null)
                                                   {
                                                       resetEvent.Set();
                                                   }
                                               } );

            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", 4545);

            Assert.That(resetEvent.WaitOne(1000));

            client.Close();
        }

    }
}
