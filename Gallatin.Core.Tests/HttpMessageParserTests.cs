using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Web;
using NUnit.Framework;
using System.IO;

namespace Gallatin.Core.Tests
{
    [TestFixture]
    public class HttpMessageParserTests
    {
        /// <summary>
        /// Verifies that a request is parsed correctly.
        /// </summary>
        [Test]
        public void ParseRequestTest()
        {
            HttpMessageParser parser = new HttpMessageParser();

            byte[] data = File.ReadAllBytes( "testdata\\request.raw" );

            IHttpMessage message = null;

            // Make multiple requests to simulate partial TCP packets split at all possible boundaries.
            foreach (byte b in data)
            {
                message = parser.AppendData(new[]
                                                          {
                                                              b
                                                          });
            }

            Assert.That(message, Is.Not.Null);

            IHttpRequestMessage requestMessage = message as IHttpRequestMessage;

            Assert.That( requestMessage, Is.Not.Null );
            Assert.That( requestMessage.Body, Is.Null );
            Assert.That( requestMessage.Destination, Is.EqualTo(new Uri("http://www.yahoo.com")));
            Assert.That( requestMessage.Version, Is.EqualTo("1.1"));
            Assert.That( requestMessage.Method, Is.EqualTo("GET"));
            Assert.That( requestMessage.Headers.Count(), Is.EqualTo(8));

            // As per RFC2616, the proxy cannot change the order of the headers. Ordering is important.

            var headerEnumeration = requestMessage.Headers.AsEnumerable().GetEnumerator();

            headerEnumeration.MoveNext();

            Assert.That(headerEnumeration.Current,
                         Is.EqualTo( new KeyValuePair<string, string>( "Host",
                                                                       "www.yahoo.com" ) ) );
            headerEnumeration.MoveNext();

            Assert.That(headerEnumeration.Current,
                         Is.EqualTo(new KeyValuePair<string, string>("User-Agent",
                                                                       "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:6.0) Gecko/20100101 Firefox/6.0" ) ) );
            headerEnumeration.MoveNext();

            Assert.That(headerEnumeration.Current,
                         Is.EqualTo(new KeyValuePair<string, string>("Accept",
                                                                       "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8")));
            headerEnumeration.MoveNext();

            Assert.That(headerEnumeration.Current,
                         Is.EqualTo(new KeyValuePair<string, string>("Accept-Language",
                                                                       "en-us,en;q=0.5")));
            headerEnumeration.MoveNext();

            Assert.That(headerEnumeration.Current,
                         Is.EqualTo(new KeyValuePair<string, string>("Accept-Encoding",
                                                                       "gzip, deflate")));
            headerEnumeration.MoveNext();

            Assert.That(headerEnumeration.Current,
                         Is.EqualTo(new KeyValuePair<string, string>("Accept-Charset",
                                                                       "ISO-8859-1,utf-8;q=0.7,*;q=0.7")));
            headerEnumeration.MoveNext();

            Assert.That(headerEnumeration.Current,
                         Is.EqualTo(new KeyValuePair<string, string>("Connection",
                                                                       "keep-alive")));
            headerEnumeration.MoveNext();

            Assert.That(headerEnumeration.Current,
                         Is.EqualTo(new KeyValuePair<string, string>("Cookie",
                                                                       "B=7ac796t6uc9ld&b=4&d=.4idkOBpYELmRpYUsfMRhgw66mRFm7xXVwR8Hw--&s=ic&i=j7abCk.Fml_1de81KUis; fpc=d=Z4feRid6IfnklwpFgl3_2nII56kb.q3qwrhR2Woo9MSuGkxr1C62f7v7mSIp0LJb2SFNYnz_VxIGU9tvSQR1M4narnLt.De_cdhOOBjuRK1LOutjvoYCJr68aNeJ5ZbNr6JE4EilAGt0Dow0azUhpQxMRD5UYnicF8Ke_K7uErz1PN3qVTvAOjTpMdBvFQ8t.myN0ZbAYRZ8QqO0e0UzqehbFC4q1hSB2zY5.p9B7LlBDN_W8gb7GECDh3PfjvC8ckyn5Yl1wzR9_k.f1Kxb9p7fy6weieYXYXAoAKysYozaxMHYKjALPVQutA--&v=2; FPS=dl; F=a=RW.pQV0MvTWwqGn0JI7ByYznYq6L3m98efzh1x3L.5xCNMphxXwS6NrD19ASwjw7fMWW0SU-&b=JAQu; PH=fn=JmLuuRSIZ0lBOPk16g--&l=en-US; U=mt=KLSAUJ2MhYhmnqnPGkBGJ.CnI.ceKV2d0PBOIEk-&ux=A4g7NB&un=0dt8ucfvlhfqv; YSC=0; FPCK3=AgBObp0gAHKZIABiNyAAYtsQAENFIABbJiAAZlIQAHe9; CH=AgBObp0gAAsUIAAgzSAALcMgACieIAAUPSAANHAgAB1TIAAkliAAFHggAAf+; FPCK2=AgBOWSoQACSgEABilxAAc0MQAFI5; fpt=d=pCnD1KLXetogCoCEm4IAhrSx_ZAJiUq3Hi5QNepfCpdlxyYFxxHvzUVBPO6GPrGR..mkFZldoMDNdWN5dF4QQ9w0rF7EcXoaIiTxYDcZOsI_o9ckd71C5FxrE.PsmzjNGom9utnFu3xANfKaQiCKlRxp6OxZ80vXbHEJl5MEishwumO8EtLuUxugVPuiorNto1vuLyTS28dFtMnVwZG62rEYq36sULx2dmcUPCP3cX0STYRA63SILxtistZtDMA0t5NWc6NjCmrdcWkutxmj0BmNseuiqQUkOhn9rfO.hkDumhAp9KGfhJNM7ma.nSQ7uaehat0XyDSDRGs7wdT9Vz4PPTCph7fDqykixCJkHwQXhCYtQDIIm8NZ15VaY9K16aud5LkEMLRxMTMOrcdw5m4kE1jY7LE.l2m8_vsFV3dsKIsE7X7NGGYN_.jgzLCYLY7NqrtjFCe02DTC.yzdmCoJzVNjN1nJMRcF.svucL6tGUG5gMcOcyItRsWPWvISfuf5RhY-&v=1; fpc_s=d=p4OEWJawsnkcZ3CMJf7xZfcHTqsJKUUNLnOmXUgw7MIEW9kuKyrWa0YrcSnLAoicDA5jSj6MMFWMMxKngibFW6fKZj6QcgpIVDFl1LuOehgumD9e_3_zD6zGifZ0qKPcJ_tQ3XjV6q03w4JJPgR_nU.xTV5h_PwoWcGwGr03EAPBs4HPKkKHuXdtqPBp_I.A3DTp_rOCuLBu0L6SlfL4ktG31dLIdUK4blMKEGUTVweTrwc.zZ1NSzBT7dL4UmMptbMinpOwZyTJlrnTGDoKAyo8UUFBAV626sTGLuaForzL0OPhASop.6eF9XRvsBjoBODBk.XA0YS7w.sqIrGGqZ2Ju5Q-&v=2")));

        }

        [Test]
        public void ParseResponseWithContentLengthSpecified()
        {
            HttpMessageParser parser = new HttpMessageParser();

            byte[] headerData = File.ReadAllBytes("testdata\\response content length.raw");
            byte[] bodyData = File.ReadAllBytes("testdata\\response content length body.raw");

            IHttpMessage message = null;

            message = parser.AppendData( headerData );

            Assert.That(message, Is.Null);

            message = parser.AppendData(bodyData);

            Assert.That(message, Is.Not.Null);

            IHttpResponseMessage response = message as IHttpResponseMessage;

            Assert.That(message, Is.Not.Null);

            Assert.That(response.Body, Is.EqualTo(bodyData));

            Assert.That( response.StatusCode, Is.EqualTo(200) );

            Assert.That(response.StatusText, Is.EqualTo("OK"));

            Assert.That(response.Version, Is.EqualTo("1.1"));

            Assert.That(response.Headers, Has.Member( new KeyValuePair<string,string> ("Content-Length", "2891") ));
        }

        [Test]
        public void ParseResponseWithChunkedData()
        {
            HttpMessageParser parser = new HttpMessageParser();

            byte[] headerData = File.ReadAllBytes("testdata\\response chunked.raw");
            byte[] bodyData = File.ReadAllBytes("testdata\\response chunked body.raw");

            IHttpMessage message = null;

            message = parser.AppendData(headerData);

            Assert.That(message, Is.Null);

            message = parser.AppendData(bodyData);

            Assert.That(message, Is.Not.Null);

            IHttpResponseMessage response = message as IHttpResponseMessage;

            Assert.That(message, Is.Not.Null);

            // Verify transfer-encoding is removed prior to forwarding by proxy (section 14.41)
            Assert.That(response.StatusCode, Is.EqualTo(200));

            Assert.That(response.StatusText, Is.EqualTo("OK"));

            Assert.That(response.Version, Is.EqualTo("1.1"));

            // Content-length should be added
            Assert.That(response.Headers, Has.Member( new KeyValuePair<string, string>("Content-Length", "52177") ));

            // The body should equal the control file
            FileStream stream = new FileStream("testdata\\response chunked body reassembled.raw", FileMode.Open);

            StreamReader reader = new StreamReader(stream);

            byte[] controlData = File.ReadAllBytes("testdata\\response chunked body reassembled.raw");

            Assert.That(response.Body, Is.EqualTo(controlData));

        }

    }
}
