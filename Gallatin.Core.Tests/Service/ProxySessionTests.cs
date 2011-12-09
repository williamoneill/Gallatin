using System;
using System.Text;
using System.Threading;
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

            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();

            ProxySession session = new ProxySession(mockFactory.Object, outboundFilter.Object, settings.Object);
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

            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet( m => m.FilteringEnabled ).Returns( true );

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

            ProxySession session = new ProxySession( mockFactory.Object, outboundFilter.Object, settings.Object );
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

            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();

            ProxySession session = new ProxySession( mockFactory.Object, outboundFilter.Object, settings.Object );
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
            ManualResetEvent waitForClientSends = new ManualResetEvent(false);

            // If this test ever hangs, change the client.BeginSend mock to use It.IsAny and debug from there
            byte[] request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            byte[] response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\nhi" );
            byte[] responseHeader = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nConnection: keep-alive\r\nContent-Length: 2\r\n\r\n");
            byte[] originalBody = Encoding.UTF8.GetBytes("hi");
            byte[] body = Encoding.UTF8.GetBytes("by");
            string filterResponse = null;

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            // Request from client
            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            int numClientReceives = 0;
            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) =>
                {
                    // Only respond with data on the first request. Ignore the others.
                    if (++numClientReceives == 1)
                    {
                        callback(true, request, client.Object);
                    }
                });

            int numClientSends = 0;
            client.Setup(m => m.BeginSend(It.IsAny<byte[]>(), It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<byte[], Action<bool, INetworkFacade>>(
                    (b, c) =>
                    {
                        c(true, client.Object);

                        if (++numClientSends == 2)
                        {
                            // After receiving all data (header and body) release the main thread to verify
                            waitForClientSends.Set();
                        }
                    });

            // Response from server
            int numServerReceives = 0;
            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) =>
                {
                    // Only respond with data on the first request. The second time, mock that the connection
                    // closed; thereby forcing the modified body to be sent to the client.
                    if (++numServerReceives == 1)
                    {
                        callback(true, response, server.Object);
                    }

                });

            // Mock the connection to the remote host. Return the mock network facade.
            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((h, p, c) => c(true, server.Object));

            // Set up the filters. We want a response filter that changes the body content from "hi" to "by"
            Mock<IProxyFilter> filter = new Mock<IProxyFilter>();
            filter.Setup(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>())).Returns(null as string);
            filter.Setup(m => m.TryEvaluateResponseFilters(It.IsAny<IHttpResponse>(), It.IsAny<string>(), out filterResponse))
                .Returns(false);

            // Notice that we are going to take the original body and map it to the new body
            filter.Setup(m => m.EvaluateResponseFiltersWithBody(It.IsAny<IHttpResponse>(), It.IsAny<string>(), originalBody))
                .Returns(body);

            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();

            //
            // Mocks are set up. Start the test.
            //

            ProxySession session = new ProxySession(mockFactory.Object, filter.Object, settings.Object);
            session.Start(client.Object);

            // Wait until all data has been sent to the client
            Assert.That(waitForClientSends.WaitOne(500000));

            client.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            mockFactory.Verify(m => m.BeginConnect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool, INetworkFacade>>()),
                                Times.Once());
            filter.Verify(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>()), Times.Once());
            filter.Verify(m => m.TryEvaluateResponseFilters(It.IsAny<IHttpResponse>(), It.IsAny<string>(), out filterResponse),
                           Times.Once());
            filter.Verify(
                m => m.EvaluateResponseFiltersWithBody(It.IsAny<IHttpResponse>(), It.IsAny<string>(), originalBody), Times.Once());

            client.Verify(m => m.BeginSend(responseHeader, It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            client.Verify(m => m.BeginSend(body, It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
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

            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();

            ProxySession session = new ProxySession( mockFactory.Object, filter.Object, settings.Object );
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

            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();

            ProxySession session = new ProxySession(mockFactory.Object, outboundFilter.Object, settings.Object);
            session.Start( client.Object );

            client.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
            server.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );

            outboundFilter.Verify( m => m.EvaluateConnectionFilters( It.IsAny<HttpRequest>(), It.IsAny<string>() ), Times.Once() );
        }

        /// <summary>
        /// This test uses an HTTP 1.0 request that does not have a content-length in the HTTP header to
        /// verify that the HTTP body filter is applied when the connection is closed.
        /// </summary>
        [Test]
        public void VerifyResponseBodyFilterInvokedOnHttp10Request()
        {
            ManualResetEvent waitForClientSends = new ManualResetEvent(false);

            // If this test ever hangs, change the client.BeginSend mock to use It.IsAny and debug from there
            byte[] request = Encoding.UTF8.GetBytes("GET / HTTP/1.0\r\nHost: www.yahoo.com\r\n\r\n");
            byte[] response = Encoding.UTF8.GetBytes("HTTP/1.0 200 OK\r\n\r\nhi");
            byte[] responseHeader = Encoding.UTF8.GetBytes("HTTP/1.0 200 OK\r\n\r\n");
            byte[] body = Encoding.UTF8.GetBytes("by");
            byte[] originalBody = Encoding.UTF8.GetBytes("hi");
            string filterResponse = null;

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            // Request from client
            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            int numClientReceives = 0;
            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) =>
                          {
                              // Only respond with data on the first request. Ignore the others.
                              if (++numClientReceives == 1)
                              {
                                  callback(true, request, client.Object);
                              }
                          });

            int numClientSends = 0;
            client.Setup(m => m.BeginSend(It.IsAny<byte[]>(), It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<byte[], Action<bool, INetworkFacade>>(
                    (b, c) =>
                    {
                        c(true, client.Object);

                        if (++numClientSends == 2)
                        {
                            // After receiving all data (header and body) release the main thread to verify
                            waitForClientSends.Set();
                        }
                    });

            // Response from server
            int numServerReceives = 0;
            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) =>
                          {
                              // Only respond with data on the first request. The second time, mock that the connection
                              // closed; thereby forcing the modified body to be sent to the client.
                              if (++numServerReceives == 1)
                              {
                                  callback(true, response, server.Object);
                              }
                              else
                              {
                                  server.Raise(m => m.ConnectionClosed += null, new EventArgs());
                              }

                          });

            // Mock the connection to the remote host. Return the mock network facade.
            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((h, p, c) => c(true, server.Object));

            // Set up the filters. We want a response filter that changes the body content from "hi" to "by"
            Mock<IProxyFilter> filter = new Mock<IProxyFilter>();
            filter.Setup(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>())).Returns(null as string);
            filter.Setup(m => m.TryEvaluateResponseFilters(It.IsAny<IHttpResponse>(), It.IsAny<string>(), out filterResponse))
                .Returns(false);

            // Notice that we are going to take the original body and map it to the new body
            filter.Setup(m => m.EvaluateResponseFiltersWithBody(It.IsAny<IHttpResponse>(), It.IsAny<string>(), originalBody))
                .Returns(body);

            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();

            //
            // Mocks are set up. Start the test.
            //

            ProxySession session = new ProxySession(mockFactory.Object, filter.Object, settings.Object);
            session.Start(client.Object);

            // Wait until all data has been sent to the client
            Assert.That(waitForClientSends.WaitOne(3000));

            client.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            mockFactory.Verify(m => m.BeginConnect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool, INetworkFacade>>()),
                                Times.Once());
            filter.Verify(m => m.EvaluateConnectionFilters(It.IsAny<HttpRequest>(), It.IsAny<string>()), Times.Once());
            filter.Verify(m => m.TryEvaluateResponseFilters(It.IsAny<IHttpResponse>(), It.IsAny<string>(), out filterResponse),
                           Times.Once());
            filter.Verify(
                m => m.EvaluateResponseFiltersWithBody(It.IsAny<IHttpResponse>(), It.IsAny<string>(), originalBody), Times.Once());
            
            client.Verify(m => m.BeginSend(responseHeader, It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            client.Verify(m => m.BeginSend(body, It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
        }
    }
}