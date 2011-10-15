using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Gallatin.Core.Web;

namespace Gallatin.Core.Tests.Web
{
    [TestFixture]
    public class HttpRequestMessageTests
    {
        [Test]
        public void ParseSsl()
        {
            Uri uri = new Uri("https://somesite.com:443");

            HttpRequestMessage message = new HttpRequestMessage( null, "1.1", null, "CoNnEcT", uri );

            Assert.That(message.IsSsl, Is.True);
            Assert.That(message.Port, Is.EqualTo(443));
            Assert.That(message.Host, Is.EqualTo("somesite.com"));
        }

        [Test]
        public void ParseDefaultAddressAndPort()
        {
            Uri uri = new Uri("http://www.cnn.com/");

            HttpRequestMessage message = new HttpRequestMessage(null, "1.1", null, "GET", uri);

            Assert.That(message.IsSsl, Is.False);
            Assert.That(message.Port, Is.EqualTo(80));
            Assert.That(message.Host, Is.EqualTo("www.cnn.com"));
        }

        [Test]
        public void NonstandardPort()
        {
            Uri uri = new Uri("http://somesite.com:555/");

            HttpRequestMessage message = new HttpRequestMessage(null, "1.1", null, "GET", uri);

            Assert.That(message.IsSsl, Is.False);
            Assert.That(message.Port, Is.EqualTo(555));
            Assert.That(message.Host, Is.EqualTo("somesite.com"));
        }
    }
}
