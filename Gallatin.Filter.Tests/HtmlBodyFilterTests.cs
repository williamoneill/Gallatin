using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Gallatin.Contracts;
using Gallatin.Filter.Util;
using Moq;
using NUnit.Framework;

namespace Gallatin.Filter.Tests
{
    [TestFixture]
    class HtmlBodyFilterTests
    {
        [Test]
        public void SimpleLoadTest()
        {
            string xml =
                "<?xml version='1.0' encoding='utf-8' ?>" +
                "<ContentFiltering>" +
                "<BannedWords>" +
                @"<Regex name='test filter' value='(prox\w{1,4})' weight='50'/>"+
                "</BannedWords>" +
                "</ContentFiltering>";

            XDocument document = XDocument.Parse(xml);

            Mock<ISettingsFileLoader> mockLoader = new Mock<ISettingsFileLoader>();
            mockLoader.Setup(m => m.LoadFile(SettingsFileType.HtmlBodyFilter)).Returns(document);   

            Mock<ILogger> mockLogger = new Mock<ILogger>();
         
            Mock<IHttpHeaders> headers = new Mock<IHttpHeaders>();
            headers.SetupGet(m => m["content-type"]).Returns("text/html");

            Mock<IHttpResponse> response = new Mock<IHttpResponse>();
            response.SetupGet(m => m.Headers).Returns(headers.Object);

            HtmlBodyFilter filter = new HtmlBodyFilter( mockLoader.Object, mockLogger.Object );

            Func<IHttpResponse, string, byte[], byte[]> outVal;
            var filterResponse = filter.EvaluateFilter( response.Object, "connectionid", out outVal );

            Assert.That(filterResponse, Is.Null);
            Assert.That(outVal, Is.Not.Null);

            var htmlBody = Encoding.UTF8.GetBytes( "this string contains the word proxy once and proxy twice so it should get some weight" );

            var filterResponse2 = outVal( response.Object, "connectionid", htmlBody );

            Assert.That( filterResponse2, Is.Not.Null );
        }
    }
}
