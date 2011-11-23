using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Gallatin.Contracts;
using Gallatin.Filter.Util;
using Moq;
using NUnit.Framework;

namespace Gallatin.Filter.Tests
{
    [TestFixture]
    public class WhiteListTests
    {
        [Test]
        public void LoadTest( [Values("192.168.0.3:213", "10.1.0.3:344")] string address,
            [Values("www.cnn.com", "www.whitehouse.gov")] string host)
        {
            string xml =
                "<?xml version='1.0' encoding='utf-8' ?>" +
                "<Whitelist>" +
                "<Hosts>" +
                "<IP address='10.0.0.0/8'/>" +
                "</Hosts>" +
                "<Urls>" +
                "<Url name='.gov'/>" +
                "</Urls>" +
                "</Whitelist>";

            XDocument document = XDocument.Parse( xml );

            Mock<ISettingsFileLoader> mockLoader = new Mock<ISettingsFileLoader>();
            mockLoader.Setup( m => m.LoadFile( SettingsFileType.Whitelist ) ).Returns( document );

            Mock<IHttpHeaders> headers = new Mock<IHttpHeaders>();
            headers.SetupGet( m => m["host"] ).Returns( host );

            Mock<IHttpRequest> request = new Mock<IHttpRequest>();
            request.SetupGet( m => m.Headers ).Returns( headers.Object );

            WhiteListEvaluator whiteListEvaluator = new WhiteListEvaluator( mockLoader.Object );

            if (address == "10.1.0.3:344" || host == "www.whitehouse.gov")
            {
                Assert.That(whiteListEvaluator.IsWhitlisted( request.Object, address ), Is.True);
            }
            else
            {
                Assert.That(whiteListEvaluator.IsWhitlisted(request.Object, address), Is.False);
            }
        }
    }
}
