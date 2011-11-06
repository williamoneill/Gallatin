using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Service;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ProxySessionTests
    {
        // TODO: create test to verify filter is used

        [Test]
        public void SimpleSendReceiveTest()
        {
            byte[] request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            byte[] response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nhi" );

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, request, client.Object));

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, response, server.Object));

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup( m => m.BeginConnect( "www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<string, int, Action<bool, INetworkFacade>>( ( h, p, c ) => c( true, server.Object ) );

            Mock<IProxyFilter> outboundFilter = new Mock<IProxyFilter>();
            outboundFilter.Setup(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>())).Returns(null as string);

            ProxySession session = new ProxySession( mockFactory.Object, outboundFilter.Object );
            session.Start(client.Object);

            client.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            server.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());

            outboundFilter.Verify(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void PersistentConnectionTest()
        {
            int i = 0;

            byte[] request = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n");
            byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nhi");

            byte[] request2 = Encoding.UTF8.GetBytes("GET /foo.jpg HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n");
            byte[] response2 = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nhi");

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, i++ == 0 ? request : request2, client.Object));

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, i++ == 1 ? response : response2, server.Object));

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((h, p, c) => c(true, server.Object));

            Mock<IProxyFilter> outboundFilter = new Mock<IProxyFilter>();
            outboundFilter.Setup(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>())).Returns(null as string);

            ProxySession session = new ProxySession( mockFactory.Object, outboundFilter.Object);
            session.Start(client.Object);

            client.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            server.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            mockFactory.Verify(m=>m.BeginConnect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool,INetworkFacade>>()), Times.Once());

            outboundFilter.Verify(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>()), Times.Exactly(1));
        }

        [Test]
        public void ConnectionFilterTest()
        {
            byte[] request = Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n");

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, request, client.Object));

            // If I place Encoding.UTF8.GetBytes("Bad request") in the Setup method, this never gets called, yet
            // the Assert verfies that is the value. Not sure what I'm doing wrong with MOQ.
            client.Setup( m => m.BeginSend( It.IsAny<byte[]>(), It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<byte[], Action<bool, INetworkFacade>>( ( b, d ) =>
                                                                 {
                                                                     Assert.That(b, Is.EqualTo(Encoding.UTF8.GetBytes("Bad request")));
                                                                     d( true, client.Object );
                                                                 } );

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();

            Mock<IProxyFilter> outboundFilter = new Mock<IProxyFilter>();
            outboundFilter.Setup(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>())).Returns("Bad request");

            ProxySession session = new ProxySession(mockFactory.Object, outboundFilter.Object);
            session.Start(client.Object);

            // I can't get this to work in MOQ. Asserting in the callback above. 
            //client.Verify(m => m.BeginSend(Encoding.UTF8.GetBytes("Bad request"), It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            client.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            mockFactory.Verify( m => m.BeginConnect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool,INetworkFacade>>()), Times.Never() );
            outboundFilter.Verify(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>()), Times.Once());
        }
    }
}
