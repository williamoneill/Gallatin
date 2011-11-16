using System;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ProxySessionTests
    {
        [Test]
        public void UseSslTest()
        {
            bool callbackInvoked = false;

            byte[] request = Encoding.UTF8.GetBytes("CONNECT www.yahoo.com:443 HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n");

            // Client sends SSL request
            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, request, client.Object));

            // The server should be contacted by the proxy server
            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            // That the proxy server should attempt to connect on port 443
            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 443, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((h, p, c) => c(true, server.Object));

            Mock<IProxyFilter> outboundFilter = new Mock<IProxyFilter>();
            outboundFilter.Setup(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>())).Returns(null as string);

            // Setup the mock SSL tunnel. Yes, this is an integration test and not a pure unit test.
            Mock<ISslTunnel> mockSslTunnel = new Mock<ISslTunnel>();
            mockSslTunnel.Setup( m => m.EstablishTunnel( client.Object, server.Object, "1.1" ) )
                .Callback<INetworkFacade, INetworkFacade, string>(
                    ( c, s, v ) =>
                    {
                        callbackInvoked = true;
                        mockSslTunnel.Raise( m => m.TunnelClosed += null, new EventArgs() );
                    } );

            CoreFactory.Register( () => mockSslTunnel.Object );

            ProxySession session = new ProxySession(mockFactory.Object, outboundFilter.Object);
            session.Start(client.Object);

            client.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            server.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());

            Assert.IsTrue(callbackInvoked);
        }

        [Test]
        public void ConnectionFilterTest()
        {
            byte[] badRequest = Encoding.UTF8.GetBytes( "Bad request" );
            byte[] request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback( ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, request, client.Object ) );

            // If I place Encoding.UTF8.GetBytes("Bad request") in the Setup method, this never gets called, yet
            // the Assert verfies that is the value. Not sure what I'm doing wrong with MOQ.
            client.Setup( m => m.BeginSend( It.IsAny<byte[]>(), It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<byte[], Action<bool, INetworkFacade>>( ( b, d ) =>
                                                                 {
                                                                     Assert.That( b, Is.EqualTo( badRequest ) );
                                                                     d( true, client.Object );
                                                                 } );

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();

            Mock<IProxyFilter> outboundFilter = new Mock<IProxyFilter>();
            outboundFilter.Setup( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ) ).Returns( "Bad request" );

            ProxySession session = new ProxySession( mockFactory.Object, outboundFilter.Object );
            session.Start( client.Object );

            client.Verify( m => m.BeginSend( badRequest, It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
            client.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
            mockFactory.Verify( m => m.BeginConnect( It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool, INetworkFacade>>() ),
                                Times.Never() );
            outboundFilter.Verify( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ), Times.Once() );
        }

        [Test]
        public void PersistentConnectionTest()
        {
            int i = 0;

            byte[] request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            byte[] response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nhi" );

            byte[] request2 = Encoding.UTF8.GetBytes( "GET /foo.jpg HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            byte[] response2 = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nhi" );

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback(
                    ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, i++ == 0 ? request : request2, client.Object ) );

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback(
                    ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, i++ == 1 ? response : response2, server.Object ) );

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup( m => m.BeginConnect( "www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<string, int, Action<bool, INetworkFacade>>( ( h, p, c ) => c( true, server.Object ) );

            Mock<IProxyFilter> outboundFilter = new Mock<IProxyFilter>();
            outboundFilter.Setup( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ) ).Returns( null as string );

            ProxySession session = new ProxySession( mockFactory.Object, outboundFilter.Object );
            session.Start( client.Object );

            client.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
            server.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
            mockFactory.Verify( m => m.BeginConnect( It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool, INetworkFacade>>() ),
                                Times.Once() );

            outboundFilter.Verify( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ), Times.Exactly( 1 ) );
        }

        [Test]
        public void ResponseFilterTestWithBody()
        {
            // If this test ever hangs, change the client.BeginSend mock to use It.IsAny and debug from there
            int clientSendCounter = 0;
            byte[] request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            byte[] response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nhi" );
            byte[] body = Encoding.UTF8.GetBytes( "hi" );
            string filterResponse = null;

            // Request from client
            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback( ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, request, client.Object ) );
            client.Setup( m => m.BeginSend( It.IsAny<byte[]>(), It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<byte[], Action<bool, INetworkFacade>>(
                    ( b, c ) =>
                    {
                        string data = Encoding.UTF8.GetString( b );
                        if ( clientSendCounter++ == 0 )
                        {
                            Assert.That( data, Is.EqualTo( "HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\n" ) );
                        }
                        else
                        {
                            Assert.That( data, Is.EqualTo( "by" ) );
                        }

                        c( true, client.Object );
                    } );

            // Response from server
            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback( ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, response, server.Object ) );

            // Mock the connection to the remote host
            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup( m => m.BeginConnect( "www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<string, int, Action<bool, INetworkFacade>>( ( h, p, c ) => c( true, server.Object ) );

            Mock<IProxyFilter> filter = new Mock<IProxyFilter>();
            filter.Setup( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ) ).Returns( null as string );
            filter.Setup( m => m.TryEvaluateResponseFilters( It.IsAny<IHttpResponse>(), It.IsAny<string>(), out filterResponse ) )
                .Returns( false );
            filter.Setup( m => m.EvaluateResponseFiltersWithBody( It.IsAny<IHttpResponse>(), It.IsAny<string>(), body ) )
                .Returns( Encoding.UTF8.GetBytes( "by" ) );

            ProxySession session = new ProxySession( mockFactory.Object, filter.Object );
            session.Start( client.Object );

            client.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
            mockFactory.Verify( m => m.BeginConnect( It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool, INetworkFacade>>() ),
                                Times.Once() );
            filter.Verify( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ), Times.Once() );
            filter.Verify( m => m.TryEvaluateResponseFilters( It.IsAny<IHttpResponse>(), It.IsAny<string>(), out filterResponse ),
                           Times.Once() );
            filter.Verify(
                m => m.EvaluateResponseFiltersWithBody( It.IsAny<IHttpResponse>(), It.IsAny<string>(), body ), Times.Once() );
        }

        [Test]
        public void ResponseFilterTestWithoutBody()
        {
            byte[] request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            byte[] response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nhi" );
            string filterResponse = "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 20\r\n\r\nbad things live here";
            byte[] encodedFilterResponse = Encoding.UTF8.GetBytes( filterResponse );

            // Request from client
            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback( ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, request, client.Object ) );
            client.Setup( m => m.BeginSend( encodedFilterResponse, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<byte[], Action<bool, INetworkFacade>>( ( b, c ) => c( true, client.Object ) );

            // Response from server
            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback( ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, response, server.Object ) );

            // Mock the connection to the remote host
            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup( m => m.BeginConnect( "www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<string, int, Action<bool, INetworkFacade>>( ( h, p, c ) => c( true, server.Object ) );

            Mock<IProxyFilter> filter = new Mock<IProxyFilter>();
            filter.Setup( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ) ).Returns( null as string );
            filter.Setup( m => m.TryEvaluateResponseFilters( It.IsAny<IHttpResponse>(), It.IsAny<string>(), out filterResponse ) ).Returns(
                true );

            ProxySession session = new ProxySession( mockFactory.Object, filter.Object );
            session.Start( client.Object );

            client.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
            mockFactory.Verify( m => m.BeginConnect( It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool, INetworkFacade>>() ),
                                Times.Once() );
            filter.Verify( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ), Times.Once() );
            filter.Verify( m => m.TryEvaluateResponseFilters( It.IsAny<IHttpResponse>(), It.IsAny<string>(), out filterResponse ),
                           Times.Once() );
            filter.Verify(
                m => m.EvaluateResponseFiltersWithBody( It.IsAny<IHttpResponse>(), It.IsAny<string>(), It.IsAny<byte[]>() ), Times.Never() );
        }

        [Test]
        public void SimpleSendReceiveTest()
        {
            byte[] request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            byte[] response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nhi" );

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback( ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, request, client.Object ) );

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback( ( Action<bool, byte[], INetworkFacade> callback ) => callback( true, response, server.Object ) );

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup( m => m.BeginConnect( "www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<string, int, Action<bool, INetworkFacade>>( ( h, p, c ) => c( true, server.Object ) );

            Mock<IProxyFilter> outboundFilter = new Mock<IProxyFilter>();
            outboundFilter.Setup( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ) ).Returns( null as string );

            ProxySession session = new ProxySession( mockFactory.Object, outboundFilter.Object );
            session.Start( client.Object );

            client.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
            server.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );

            outboundFilter.Verify( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ), Times.Once() );
        }
    }
}