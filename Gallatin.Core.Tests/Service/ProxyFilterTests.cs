using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ProxyFilterTests
    {
        [Test]
        public void VerifyResponseSort()
        {
            ProxyFilter filter = new ProxyFilter();
            var filters = new List<IConnectionFilter>();

            Mock<IHttpRequest> requestArgs = new Mock<IHttpRequest>();
            requestArgs.SetupAllProperties();

            Mock<IConnectionFilter> mockFilter = new Mock<IConnectionFilter>();
            mockFilter.SetupGet( s => s.FilterSpeedType ).Returns( FilterSpeedType.Remote );

            Mock<IConnectionFilter> mockFilter2 = new Mock<IConnectionFilter>();
            mockFilter2.SetupGet(s => s.FilterSpeedType).Returns(FilterSpeedType.LocalAndFast);
            mockFilter2.Setup( s => s.EvaluateFilter( requestArgs.Object, It.IsAny<string>() ) ).Returns( "Foo" );

            filters.Add( mockFilter.Object );
            filters.Add(mockFilter2.Object);
            filter.ConnectionFilters = filters;

            var output = filter.EvaluateConnectionFilters( requestArgs.Object, "whatever" );

            Assert.That(output, Is.EqualTo("HTTP/ 200 OK\r\nConnection: close\r\nContent length: 93\r\nContent-Type: text/html\r\n\r\n<html><head><title>Gallatin Proxy - Connection Rejected</title></head><body>Foo</body></html>"));
        }
    }
}
