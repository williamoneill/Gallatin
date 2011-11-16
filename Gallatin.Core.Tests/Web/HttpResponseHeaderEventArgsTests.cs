using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Gallatin.Core.Web;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Web
{
    [TestFixture]
    public class HttpResponseHeaderEventArgsTests
    {
        [Test]
        public void PersistentConnectionTestHttp10()
        {
            List<KeyValuePair<string, string>> headerList = new List<KeyValuePair<string, string>>();
            headerList.Add(new KeyValuePair<string, string>("Content-Length", "10"));
            HttpHeaders headers = new HttpHeaders(headerList);

            HttpResponseHeaderEventArgs args = new HttpResponseHeaderEventArgs( "1.0", headers, 200, "OK"  );

            Assert.That(args.IsPersistent, Is.False, "Default is false with HTTP 1.0");
            Assert.That(args.HasBody, Is.True);
        }

        [Test]
        public void PersistentConnectionTestHttp11()
        {
            List<KeyValuePair<string, string>> headerList = new List<KeyValuePair<string, string>>();
            headerList.Add(new KeyValuePair<string, string>("Age", "0"));
            HttpHeaders headers = new HttpHeaders(headerList);

            HttpResponseHeaderEventArgs args = new HttpResponseHeaderEventArgs("1.1", headers, 200, "OK");

            Assert.That(args.IsPersistent, Is.True, "Default is true with HTTP 1.1");
            Assert.That(args.HasBody, Is.False);
        }

        [Test]
        public void GetBufferTest()
        {
            List<KeyValuePair<string, string>> headerList = new List<KeyValuePair<string, string>>();
            headerList.Add(new KeyValuePair<string, string>("Content-Length", "10"));
            headerList.Add(new KeyValuePair<string, string>("Age", "0"));
            HttpHeaders headers = new HttpHeaders(headerList);

            var objectUnderTest = new HttpResponseHeaderEventArgs("1.1", headers, 200, "OK");

            byte[] headerBytes = objectUnderTest.GetBuffer();

            string expectedHeader = "HTTP/1.1 200 OK\r\nContent-Length: 10\r\nAge: 0\r\n\r\n";
            var expectedBytes = Encoding.UTF8.GetBytes(expectedHeader);

            Assert.That(headerBytes, Is.EqualTo(expectedBytes));
            
        }
    }
}
