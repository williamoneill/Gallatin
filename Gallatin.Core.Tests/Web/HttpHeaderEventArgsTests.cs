using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Web
{
    [TestFixture]
    public class HttpHeaderEventArgsTests
    {
        [Test]
        public void Http10HasBody()
        {
            Mock<IHttpHeaders> mockHeaders = new Mock<IHttpHeaders>();

            HttpHeaderEventArgs args = new HttpRequestHeaderEventArgs( "1.0", mockHeaders.Object, "get", "/" );

            Assert.That(args.HasBody);
        }

        [Test]
        public void Http11HasBody()
        {
            Mock<IHttpHeaders> mockHeaders = new Mock<IHttpHeaders>();

            HttpHeaderEventArgs args = new HttpRequestHeaderEventArgs("1.1", mockHeaders.Object, "get", "/");

            Assert.That(args.HasBody, Is.False);
        }
    }
}
