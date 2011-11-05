using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Web;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Web
{
    [TestFixture]
    public class HttpRequestHeaderEventArgsTests
    {
        [Test]
        public void RemoveProxyConnectionHeaderInHttp10()
        {
            List<KeyValuePair<string,string> > headerList = new List<KeyValuePair<string, string>>();
            headerList.Add(new KeyValuePair<string, string>("Proxy-Connection", "keep-alive"));
            HttpHeaders headers = new HttpHeaders(headerList);

            var objectUnderTest = new HttpRequestHeaderEventArgs( "1.0", headers, "GET", "http://www.yahoo.com/foo.html" );

            Assert.That(objectUnderTest.Headers.Count, Is.EqualTo(0));

            var buffer = objectUnderTest.GetBuffer();

            Assert.That(Encoding.UTF8.GetString(buffer), Is.EqualTo("GET /foo.html HTTP/1.0\r\n\r\n") );
        }

        [Test]
        public void ConvertProxyConnectionHeaderInHttp11()
        {
            List<KeyValuePair<string, string>> headerList = new List<KeyValuePair<string, string>>();
            headerList.Add(new KeyValuePair<string, string>("Host", "www.yahoo.com"));
            headerList.Add(new KeyValuePair<string, string>("Proxy-Connection", "keep-alive"));
            headerList.Add(new KeyValuePair<string, string>("Foo", "bar"));
            HttpHeaders headers = new HttpHeaders(headerList);

            var objectUnderTest = new HttpRequestHeaderEventArgs("1.1", headers, "GET", "http://www.yahoo.com/foo.html");

            Assert.That(objectUnderTest.Headers.Count, Is.EqualTo(3));
            Assert.That(objectUnderTest.Headers["Connection"], Is.EqualTo("keep-alive"));

            var buffer = objectUnderTest.GetBuffer();

            // This test uses three headers to verify that the Connection header is modified in-place
            // and not moved in the list. This is important because a proxy server cannot change
            // the header order according to HTTP spec.
            Assert.That(Encoding.UTF8.GetString(buffer), Is.EqualTo("GET /foo.html HTTP/1.1\r\nHost: www.yahoo.com\r\nConnection: keep-alive\r\nFoo: bar\r\n\r\n"));
        }
    }
}
