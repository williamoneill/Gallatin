using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Web
{
    [TestFixture]
    public class ReadHttp10BodyStateTests
    {
        [Test]
        public void NoDataAvailableTest()
        {
            Mock<IHttpStreamParserContext> mockContext = new Mock<IHttpStreamParserContext>();

            ReadHttp10BodyState stateUnderTest = new ReadHttp10BodyState( mockContext.Object );

            byte[] data = new byte[0];
            stateUnderTest.AcceptData(data);

            mockContext.Verify(m => m.OnAdditionalDataRequested(), Times.Once());
            mockContext.Verify(m => m.AppendBodyData(It.IsAny<byte[]>()), Times.Never());
            mockContext.Verify(m => m.OnPartialDataAvailable(It.IsAny<byte[]>()), Times.Never());
        }

        [Test]
        public void DataAvailableTest()
        {
            Mock<IHttpStreamParserContext> mockContext = new Mock<IHttpStreamParserContext>();

            ReadHttp10BodyState stateUnderTest = new ReadHttp10BodyState(mockContext.Object);

            byte[] data = Encoding.UTF8.GetBytes( "foo" );
            stateUnderTest.AcceptData(data);

            mockContext.Verify(m => m.OnAdditionalDataRequested(), Times.Once());
            mockContext.Verify(m => m.AppendBodyData(It.IsAny<byte[]>()), Times.Once());
            mockContext.Verify(m => m.OnPartialDataAvailable(It.IsAny<byte[]>()), Times.Once());
        }
    }
}
