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
    public class ReadChunkedBodyStateTests
    {
        [Test]
        public void BufferOversized()
        {
            Mock<IHttpStreamParserContext> mockContext = new Mock<IHttpStreamParserContext>();

            byte [] data = new byte[]{ 0x01, 0x02 };
            byte [] dataWithTerminator = new byte[]{ 0x01, 0x02, 0x0d, 0x0a};

            ReadChunkedBodyState state = new ReadChunkedBodyState( mockContext.Object, 2 );

            state.AcceptData( dataWithTerminator );

            mockContext.Verify(m => m.OnPartialDataAvailable(dataWithTerminator));
            mockContext.Verify(m => m.AppendBodyData(data));

        }
    }
}
