using System;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Web
{
    [TestFixture]
    public class ReadHeaderStateTests
    {
        [Test]
        public void MissingHttpStatusText()
        {
            bool isInvoked = false;

            string badHeader = "HTTP/1.1 200\r\nContent-Length: 35\r\nConnection: close\r\nContent-Type: image/gif\r\n\r\n";
            byte[] badHeaderData = Encoding.UTF8.GetBytes( badHeader );

            Mock<IHttpStreamParserContext> mockContext = new Mock<IHttpStreamParserContext>();
            mockContext.Setup(m => m.OnReadResponseHeaderComplete("1.1", It.IsAny<IHttpHeaders>(), 200, ""))
                .Callback<string, IHttpHeaders, int, string>(
                    (s, h, si, st) =>
                    {
                        isInvoked = true;
                        Assert.That(h.Count, Is.EqualTo(3));
                        Assert.That(h["connection"], Is.EqualTo("close"));
                    });

            ReadHeaderState headerState = new ReadHeaderState( mockContext.Object );

            headerState.AcceptData( badHeaderData );

            mockContext.VerifySet(m => m.State = It.IsAny<ReadNormalBodyState>());

            Assert.IsTrue(isInvoked);
        }

        [Test]
        public void ValidResponseText()
        {
            bool isInvoked = false;

            string header = "HTTP/1.1 200 OK to go\r\nContent-Length: 35\r\nConnection: close\r\nContent-Type: image/gif\r\n\r\n";
            byte[] headerData = Encoding.UTF8.GetBytes( header );

            Mock<IHttpStreamParserContext> mockContext = new Mock<IHttpStreamParserContext>();
            mockContext.Setup( m => m.OnReadResponseHeaderComplete( "1.1", It.IsAny<IHttpHeaders>(), 200, "OK to go" ) )
                .Callback<string, IHttpHeaders, int, string>(
                    ( s, h, si, st ) =>
                    {
                        isInvoked = true;
                        Assert.That(h.Count, Is.EqualTo(3));
                        Assert.That(h["connection"], Is.EqualTo("close"));
                    } );

            ReadHeaderState headerState = new ReadHeaderState( mockContext.Object );

            headerState.AcceptData( headerData );

            mockContext.VerifySet( m => m.State = It.IsAny<ReadNormalBodyState>() );

            Assert.IsTrue( isInvoked );
        }

        [Test]
        public void ValidRequestText()
        {
            bool isInvoked = false;

            string header = "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n";
            byte[] headerData = Encoding.UTF8.GetBytes(header);

            Mock<IHttpStreamParserContext> mockContext = new Mock<IHttpStreamParserContext>();
            mockContext.Setup(m => m.OnReadRequestHeaderComplete("1.1", It.IsAny<IHttpHeaders>(), "GET", "/"))
                .Callback<string, IHttpHeaders, string, string>(
                    (s, h, si, st) =>
                    {
                        isInvoked = true;
                        Assert.That(h.Count, Is.EqualTo(1));
                        Assert.That(h["host"], Is.EqualTo("www.yahoo.com"));
                    });

            ReadHeaderState headerState = new ReadHeaderState(mockContext.Object);

            headerState.AcceptData(headerData);

            mockContext.VerifySet(m => m.State = It.IsAny<ReadHeaderState>());

            Assert.IsTrue(isInvoked);
        }

        /// <summary>
        /// This was news to me, but even HTTP 1.1 treats the content-length as optional if 
        /// the connection header value is close
        /// </summary>
        [Test]
        public void VerifyContentLengthIsOptionalWhenConnectionHasValueClose()
        {
            string header = "HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nblah";
            byte[] headerData = Encoding.UTF8.GetBytes(header);
            byte[] body = Encoding.UTF8.GetBytes( "blah" );

            Mock<IHttpStreamParserState> mockState = new Mock<IHttpStreamParserState>();

            Mock<IHttpStreamParserContext> mockContext = new Mock<IHttpStreamParserContext>();
            mockContext.SetupAllProperties();

            // Always return our mock state so we can verify interactions
            mockContext.SetupGet( m => m.State ).Returns( mockState.Object );

            ReadHeaderState headerState = new ReadHeaderState(mockContext.Object);

            headerState.AcceptData(headerData);

            mockContext.VerifySet(m => m.State = It.IsAny<ReadHttp10BodyState>(), Times.Once());
            mockState.Verify(m => m.AcceptData(body), Times.Once());
        }

        /// <summary>
        /// If HTTP 1.1 then the connection type is assumed keep-alive. If content length is not
        /// specified and connection type is not specified then assume no body. With HTTP 1.0
        /// connection is optional as it always assumes non-persistent connections.
        /// </summary>
        [Test]
        public void VerifyNoDataIsAssumedIfConnectionTypeNotSpecified([Values("1.0", "1.1")] string httpVersion)
        {
            string header = string.Format( "HTTP/{0} 200 OK\r\n\r\nblah", httpVersion );
            byte[] headerData = Encoding.UTF8.GetBytes(header);
            byte[] body = Encoding.UTF8.GetBytes("blah");

            Mock<IHttpStreamParserState> mockState = new Mock<IHttpStreamParserState>();

            Mock<IHttpStreamParserContext> mockContext = new Mock<IHttpStreamParserContext>();
            mockContext.SetupAllProperties();
            mockContext.SetupGet(m => m.State).Returns(mockState.Object);

            ReadHeaderState headerState = new ReadHeaderState(mockContext.Object);

            headerState.AcceptData(headerData);

            if (httpVersion == "1.0")
            {
                mockContext.VerifySet(m => m.State = It.IsAny<ReadHttp10BodyState>(), Times.Once());
                mockState.Verify(m => m.AcceptData(body), Times.Once());
            }
            else
            {
                mockContext.VerifySet(m => m.State = It.IsAny<ReadHeaderState>(), Times.Once());
                mockContext.Verify(m=>m.OnMessageReadComplete(), Times.Once());
                mockContext.Verify(m => m.OnAdditionalDataRequested(), Times.Once());
                
            }
        }
    }
}