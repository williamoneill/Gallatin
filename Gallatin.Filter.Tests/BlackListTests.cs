using System.Xml.Linq;
using Gallatin.Contracts;
using Gallatin.Filter.Util;
using Moq;
using NUnit.Framework;

namespace Gallatin.Filter.Tests
{
    [TestFixture]
    public class BlackListTests
    {
        [Test]
        public void LoadTest([Values("192.168.0.3:213", "10.1.0.3:344")] string address,
                             [Values("www.cnn.com", "www.badsite.xxx")] string host)
        {
            string xml =
                "<?xml version='1.0' encoding='utf-8' ?>" +
                "<Blacklist>" +
                "<Hosts>" +
                "<IP address='10.0.0.0/8'/>" +
                "</Hosts>" +
                "<Urls>" +
                "<Url name='.xxx'/>" +
                "</Urls>" +
                "</Blacklist>";

            XDocument document = XDocument.Parse(xml);

            Mock<ISettingsFileLoader> mockLoader = new Mock<ISettingsFileLoader>();
            mockLoader.Setup(m => m.LoadFile(SettingsFileType.Blacklist)).Returns(document);

            Mock<IHttpHeaders> headers = new Mock<IHttpHeaders>();
            headers.SetupGet(m => m["host"]).Returns(host);

            Mock<IHttpRequest> request = new Mock<IHttpRequest>();
            request.SetupGet(m => m.Headers).Returns(headers.Object);

            BlacklistFilter blacklistFilter = new BlacklistFilter(mockLoader.Object);

            if (address == "10.1.0.3:344" || host == "www.badsite.xxx")
            {
                Assert.That( blacklistFilter.EvaluateFilter( request.Object, address ), Is.Not.Null );
            }
            else
            {
                Assert.That(blacklistFilter.EvaluateFilter(request.Object, address), Is.Null);
            }
        }
    }
}