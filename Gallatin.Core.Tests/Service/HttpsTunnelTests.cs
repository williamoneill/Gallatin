using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Net;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Net
{
    [TestFixture]
    public class HttpsTunnelTests
    {
        private Mock<INetworkConnectionFactory> _mockFactory;
        private Mock<INetworkConnection> _mockClient;
        private Mock<INetworkConnection> _mockServer;

        [SetUp]
        public void Setup()
        {
            _mockFactory = new Mock<INetworkConnectionFactory>();
            _mockClient = new Mock<INetworkConnection>();
            _mockServer = new Mock<INetworkConnection>();
        }

        [Test]
        public void EstablishTunnelTest([Values(true, false)]bool serverClosed, [Values(true, false)]bool serverShutdown, [Values(true, false)]bool clientClosed, [Values(true, false)]bool clientShutdown)
        {
            int calllbackInvokeCount = 0;

            HttpsTunnel tunnel = new HttpsTunnel(_mockFactory.Object);

            _mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 443, It.IsAny<Action<bool, INetworkConnection>>()))
                .Callback<string, int, Action<bool, INetworkConnection>>((a, b, c) => c(true, _mockServer.Object));

            var clientBytes = new byte[] {1, 2, 3};
            var serverBytes = new byte[] {4, 5, 6};

            var bytes =
                Encoding.UTF8.GetBytes("HTTP/1.1 200 Connection established\r\nProxy-agent: Gallatin-Proxy/1.1\r\n\r\n");

            tunnel.TunnelClosed += (sender, args) => calllbackInvokeCount++;

            tunnel.EstablishTunnel("www.yahoo.com", 443, "1.1", _mockClient.Object);

            _mockClient.Raise( m => m.DataAvailable += null, new DataAvailableEventArgs(clientBytes) );
            _mockServer.Raise(m => m.DataAvailable += null, new DataAvailableEventArgs(serverBytes));

            _mockClient.Verify(m=>m.SendData(bytes), Times.Once());
            _mockClient.Verify(m=>m.SendData(serverBytes), Times.Once());

            _mockServer.Verify(m=>m.SendData(clientBytes), Times.Once());

            if(serverClosed)
                _mockServer.Raise(m=>m.ConnectionClosed += null, new EventArgs());

            if(serverShutdown)
                _mockServer.Raise(m=>m.Shutdown += null, new EventArgs());

            if (clientClosed)
                _mockClient.Raise(m => m.ConnectionClosed += null, new EventArgs());

            if (clientShutdown)
                _mockClient.Raise(m => m.Shutdown += null, new EventArgs());

            if(serverClosed || serverShutdown || clientShutdown || clientClosed )
                Assert.That(calllbackInvokeCount, Is.EqualTo(1));
            else
            {
                Assert.That(calllbackInvokeCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void ServerConnectErrorTest()
        {
            HttpsTunnel tunnel = new HttpsTunnel(_mockFactory.Object);

            _mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 443, It.IsAny<Action<bool, INetworkConnection>>()))
                .Callback<string, int, Action<bool, INetworkConnection>>((a, b, c) => c(false, null));

            int calllbackInvokeCount = 0;
            tunnel.TunnelClosed += (sender, args) => calllbackInvokeCount++;

            tunnel.EstablishTunnel("www.yahoo.com", 443, "1.1", _mockClient.Object);

            Assert.That(calllbackInvokeCount, Is.EqualTo(1), "The tunnel should have reported it closed");
        }
    }
}
