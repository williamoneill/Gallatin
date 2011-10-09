using System;
using System.Text;
using Gallatin.Core.Client;
using Gallatin.Core.Service;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Client
{
    [TestFixture]
    public class ProxyClientTests
    {
        [Test]
        public void MultipleRequestsOnSameSocketConnection()
        {
            Mock<INetworkService> mockNetworkService = new Mock<INetworkService>();

            ProxyClient proxyClient = new ProxyClient();

            proxyClient.StartSession( mockNetworkService.Object );

            var requestData =
                Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            var responseData = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: keep-alive\r\n\r\n" );
            var requestData2 =
                Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.cnn.com\r\n\r\n");
            var responseData2 = Encoding.UTF8.GetBytes("HTTP/1.1 304 Not Modified\r\n\r\n");

            proxyClient.TryCompleteMessageFromClient( requestData );

            proxyClient.ServerSendComplete();

            proxyClient.TryCompleteMessageFromServer( responseData );

            proxyClient.ClientSendComplete();

            proxyClient.TryCompleteMessageFromClient(requestData2 );

            proxyClient.ServerSendComplete();

            proxyClient.TryCompleteMessageFromServer(responseData2 );

            proxyClient.ClientSendComplete();

            mockNetworkService.Verify(
                s =>
                s.SendServerMessage( proxyClient, requestData, "www.yahoo.com", 80 ),
                Times.Once() );

            mockNetworkService.Verify(
                s =>
                s.SendServerMessage( proxyClient, requestData2, "www.cnn.com", 80 ),
                Times.Once() );
            mockNetworkService.Verify(
                s =>
                s.SendClientMessage( proxyClient,
                               responseData ),
                Times.Once() );
            mockNetworkService.Verify(
                s =>
                s.SendClientMessage( proxyClient,
                               responseData2 ),
                Times.Once() );
        }


        [Test]
        public void VerifyErrorIfHttpResponseIsFirstMessage()
        {
            Mock<INetworkService> mockNetworkService = new Mock<INetworkService>();

            ProxyClient proxyClient = new ProxyClient();

            proxyClient.StartSession(mockNetworkService.Object);

            Assert.Throws<InvalidCastException>(
                () => proxyClient.TryCompleteMessageFromClient( Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\n\r\n" ) ) );
        }

        [Test]
        public void VerifyErrorIfHttpRequestIsSentFromServer()
        {
            Mock<INetworkService> mockNetworkService = new Mock<INetworkService>();

            ProxyClient proxyClient = new ProxyClient();

            proxyClient.StartSession(mockNetworkService.Object);

            proxyClient.TryCompleteMessageFromClient(Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n"));

            proxyClient.ServerSendComplete();

            Assert.Throws<InvalidCastException>(
                () => proxyClient.TryCompleteMessageFromServer(Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n")));
        }

    }
}