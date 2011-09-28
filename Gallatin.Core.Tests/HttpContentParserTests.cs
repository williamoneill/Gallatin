using System.Diagnostics;
using System.IO;
using Gallatin;
using Gallatin.Core;
using NUnit.Framework;

namespace CodeOntogeny.Gallatin.Core.Test
{
    [TestFixture]
    public class HttpContentParserTests
    {
        [Test]
        public void VerifyRequestParse()
        {
            using ( Stream stream = new FileStream( @".\TestData\SampleHttpRequest.txt", FileMode.Open ) )
            {
                HttpMessage httpMessage;

                Assert.That( HttpContentParser.TryParse( stream, out httpMessage ), Is.True );

                HttpRequest httpRequest = httpMessage as HttpRequest;

                Assert.That(httpRequest, Is.Not.Null);

                Assert.That(httpRequest.DestinationAddress, Is.EqualTo("http://oneillart.com/"));
                Assert.That(httpRequest.RequestType, Is.EqualTo(HttpActionType.Get));
                Assert.That(httpRequest.Version, Is.EqualTo("1.1"));
                Assert.That(httpRequest.Body, Is.Null);
                Assert.That(httpRequest.HeaderPairs, Has.Count.EqualTo( 10 ) );

                Assert.That(httpRequest.OriginalStream, Is.SameAs(stream));

                var keyValue = httpRequest.HeaderPairs.Find( s => s.Key == "User-Agent" );

                Assert.That(keyValue, Is.Not.Null);
                Assert.That(keyValue.Value, Is.EqualTo("Mozilla/5.0 (Windows NT 6.1; WOW64; rv:6.0) Gecko/20100101 Firefox/6.0"));
            }
        }

        [Test]
        public void VerifyResponseParse()
        {
            using (Stream stream = new FileStream(@".\TestData\SampleHttpResponse.txt", FileMode.Open))
            {
                HttpMessage httpMessage;

                Assert.That(HttpContentParser.TryParse(stream, out httpMessage), Is.True);

                HttpResponse httpResponse = httpMessage as HttpResponse;

                Assert.That(httpResponse, Is.Not.Null);

                Assert.That( httpResponse.ResponseCode, Is.EqualTo( 304 ) );
                Assert.That(httpResponse.Status, Is.EqualTo("Not Modified"));
                Assert.That(httpResponse.Version, Is.EqualTo("1.1"));

                Assert.That(httpResponse.Body, Is.Null);
            }
        }

        [Test]
        public void DuplicateHeaderKeyTest()
        {
            using (Stream stream = new FileStream(@".\TestData\MultipleCookieResponse.txt", FileMode.Open))
            {
                HttpMessage httpMessage;

                Assert.That(HttpContentParser.TryParse(stream, out httpMessage), Is.True);

                HttpResponse httpResponse = httpMessage as HttpResponse;

                Assert.That(httpResponse, Is.Not.Null);

                Assert.That(httpResponse.ResponseCode, Is.EqualTo(200));
                Assert.That(httpResponse.Status, Is.EqualTo("OK"));
                Assert.That(httpResponse.Version, Is.EqualTo("1.1"));

                Assert.That(httpResponse.Body, Is.Not.Null);
                Assert.That(httpResponse.Body, Is.EqualTo(new byte[] { 0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }));
                Assert.That(httpResponse.HeaderPairs, Has.Count.EqualTo(20));

                Assert.That(httpResponse.HeaderPairs.FindAll(s=>s.Key == "Set-Cookie").Count, Is.EqualTo(9));
            }
        }
    }
}