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

            proxyClient.NewDataAvailable(
                Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" ) );

            proxyClient.SendComplete();

            proxyClient.NewDataAvailable( Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\n\r\n" ) );

            proxyClient.SendComplete();

            proxyClient.NewDataAvailable(
                Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.cnn.com\r\n\r\n" ) );

            proxyClient.SendComplete();

            proxyClient.NewDataAvailable(
                Encoding.UTF8.GetBytes( "HTTP/1.1 304 Not Modified\r\n\r\n" ) );

            proxyClient.SendComplete();

            proxyClient.EndSession();

            mockNetworkService.Verify( s => s.GetDataFromClient( proxyClient ), Times.Exactly( 3 ) );
            mockNetworkService.Verify( s => s.GetDataFromRemoteHost( proxyClient ),
                                       Times.Exactly( 2 ) );
            mockNetworkService.Verify(
                s =>
                s.SendMessage( proxyClient,
                               It.Is<IHttpRequestMessage>(
                                   p => p.Destination.Host == "www.yahoo.com" ) ),
                Times.Once() );
            mockNetworkService.Verify(
                s =>
                s.SendMessage( proxyClient,
                               It.Is<IHttpResponseMessage>(
                                   p => p.StatusCode == 200 && p.StatusText == "OK" ) ),
                Times.Once() );
            mockNetworkService.Verify(
                s =>
                s.SendMessage( proxyClient,
                               It.Is<IHttpRequestMessage>( p => p.Destination.Host == "www.cnn.com" ) ),
                Times.Once() );
            mockNetworkService.Verify(
                s =>
                s.SendMessage( proxyClient,
                               It.Is<IHttpResponseMessage>(
                                   p => p.StatusCode == 304 && p.StatusText == "Not Modified" ) ),
                Times.Once() );
        }

        [Test]
        public void VerifyCompleteSendAndReceive()
        {
            Mock<INetworkService> mockNetworkService = new Mock<INetworkService>();

            ProxyClient proxyClient = new ProxyClient();

            // Drive the proxy client like the proxy server would in a typical client session
            proxyClient.StartSession( mockNetworkService.Object );

            proxyClient.NewDataAvailable(
                Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" ) );

            proxyClient.SendComplete();

            proxyClient.NewDataAvailable( Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\n\r\n" ) );

            proxyClient.SendComplete();

            proxyClient.EndSession();

            mockNetworkService.Verify( s => s.GetDataFromClient( proxyClient ), Times.Exactly( 2 ) );
            mockNetworkService.Verify( s => s.GetDataFromRemoteHost( proxyClient ), Times.Once() );
            mockNetworkService.Verify(
                s =>
                s.SendMessage( proxyClient,
                               It.Is<IHttpRequestMessage>(
                                   p => p.Destination.Host == "www.yahoo.com" ) ),
                Times.Once() );
            mockNetworkService.Verify(
                s =>
                s.SendMessage( proxyClient,
                               It.Is<IHttpResponseMessage>(
                                   p => p.StatusCode == 200 && p.StatusText == "OK" ) ),
                Times.Once() );
        }

        [Test]
        public void VerifyExceptionsInSpecificStates()
        {
            Mock<INetworkService> mockNetworkService = new Mock<INetworkService>();

            ProxyClient proxyClient = new ProxyClient();
            Assert.Throws<InvalidOperationException>(
                () => proxyClient.NewDataAvailable( new byte[200] ) );
            Assert.Throws<InvalidOperationException>(
                proxyClient.SendComplete );

            proxyClient.StartSession( mockNetworkService.Object );

            // Accept data from client state
            Assert.Throws<InvalidOperationException>(
                proxyClient.SendComplete);
            proxyClient.NewDataAvailable(Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n"));

            // Send request to server state
            Assert.Throws<InvalidOperationException>(
                () => proxyClient.NewDataAvailable(new byte[200]));
            proxyClient.SendComplete();

            // Get response from server state
            Assert.Throws<InvalidOperationException>(
                proxyClient.SendComplete);
            proxyClient.NewDataAvailable(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\n\r\n"));

            // Send response to client state
            Assert.Throws<InvalidOperationException>(
                () => proxyClient.NewDataAvailable(new byte[200]));
            proxyClient.SendComplete();

            proxyClient.EndSession();
        }

        [Test]
        public void VerifyErrorIfHttpResponseIsFirstMessage()
        {
            Mock<INetworkService> mockNetworkService = new Mock<INetworkService>();

            ProxyClient proxyClient = new ProxyClient();

            proxyClient.StartSession(mockNetworkService.Object);

            Assert.Throws<InvalidOperationException>(
                () => proxyClient.NewDataAvailable( Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\n\r\n" ) ) );
        }

        [Test]
        public void VerifyErrorIfHttpRequestIsSentFromServer()
        {
            Mock<INetworkService> mockNetworkService = new Mock<INetworkService>();

            ProxyClient proxyClient = new ProxyClient();

            proxyClient.StartSession(mockNetworkService.Object);

            proxyClient.NewDataAvailable(Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n"));

            proxyClient.SendComplete();

            Assert.Throws<InvalidOperationException>(
                () => proxyClient.NewDataAvailable(Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n")));
        }

    }
}