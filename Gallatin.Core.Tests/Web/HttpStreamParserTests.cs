using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Core.Web;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Web
{
    [TestFixture]
    public class HttpStreamParserTests
    {
        [Test]
        public void SlowChoppyNetworkTest()
        {
            int bodyAvailableCalled = 0;
            int bodyReadCompleteCalled = 0;

            // 60 bytes long
            byte[] msg = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nContent-Length: 21\r\n\r\n123456789012345678901" );

            HttpStreamParser parser = new HttpStreamParser();

            parser.BodyAvailable += (s, a) =>
            {
                bodyAvailableCalled++;
                Assert.That(a.Data, Is.EqualTo(Encoding.UTF8.GetBytes("123456789012345678901")));
            };

            int i = 0;
            parser.AdditionalDataRequested += ( s, a ) =>
                                              {
                                                  i++;
                                                    parser.AppendData(msg.Skip(i * 5).Take(5).ToArray());
                                              };

            parser.MessageReadComplete += (s, a) => bodyReadCompleteCalled++;

            List<byte> partialReceiveBuffer = new List<byte>();
            parser.PartialDataAvailable += (s, a) =>
            {
                partialReceiveBuffer.AddRange(a.Data);
            };

            // Kick it off...
            parser.AppendData(msg.Take(5).ToArray());

            Assert.That(partialReceiveBuffer, Is.EqualTo(Encoding.UTF8.GetBytes("123456789012345678901")));
            Assert.That(bodyAvailableCalled, Is.EqualTo(1));
            Assert.That(bodyReadCompleteCalled, Is.EqualTo(1));
            Assert.That(i, Is.EqualTo(11));
        }

        [Test]
        public void ChunkedDataTest()
        {
            int bodyAvailableCalled = 0;
            int bodyReadCompleteCalled = 0;
            int partialDataAvailableCalled = 0;
            int readResponseCompleteCalled = 0;
            int additionalDataRequestedCalled = 0;

            byte[] header = File.ReadAllBytes("testdata\\response chunked.raw");
            byte[] body = File.ReadAllBytes("testdata\\response chunked body.raw");
            byte[] reassembledBody = File.ReadAllBytes( "testdata\\response chunked body reassembled.raw" );

            HttpStreamParser parser = new HttpStreamParser();

            parser.BodyAvailable += (s, a) =>
            {
                bodyAvailableCalled++;
                Assert.That(a.Data, Is.EqualTo(reassembledBody));
            };

            parser.AdditionalDataRequested += (s, a) => additionalDataRequestedCalled++;

            parser.MessageReadComplete += (s, a) => bodyReadCompleteCalled++;

            parser.ReadResponseHeaderComplete += (s, a) =>
            {
                readResponseCompleteCalled++;
                Assert.That(a.Headers["transfer-encoding"], Is.EqualTo("chunked"));
            };

            List<byte> assembledData = new List<byte>();
            parser.PartialDataAvailable += (s, a) =>
            {
                assembledData.AddRange(  a.Data );
                partialDataAvailableCalled++;
            };

            parser.ReadRequestHeaderComplete += (s, a) => Assert.Fail("Should not be invoked with HTTP response");

            parser.AppendData(header);
            parser.AppendData(body);

            Assert.That(assembledData.ToArray(), Is.EqualTo(body));
            Assert.That(bodyAvailableCalled, Is.EqualTo(1));
            Assert.That(bodyReadCompleteCalled, Is.EqualTo(1));
            Assert.That(partialDataAvailableCalled, Is.EqualTo(10));
            Assert.That(readResponseCompleteCalled, Is.EqualTo(1));
            Assert.That(additionalDataRequestedCalled, Is.EqualTo(1));
            
        }

        [Test]
        public void ResponseWithBody()
        {
            int bodyAvailableCalled = 0;
            int bodyReadCompleteCalled = 0;
            int partialDataAvailableCalled = 0;
            int readResponseCompleteCalled = 0;
            int additionalDataRequestedCalled = 0;

            byte[] header = File.ReadAllBytes( "testdata\\response content length.raw" );
            byte[] body = File.ReadAllBytes( "testdata\\response content length body.raw" );

            HttpStreamParser parser = new HttpStreamParser();

            parser.BodyAvailable += ( s, a ) =>
                                    {
                                        bodyAvailableCalled++;
                                        Assert.That( a.Data, Is.EqualTo( body ) );
                                    };

            parser.AdditionalDataRequested += ( s, a ) => additionalDataRequestedCalled++;

            parser.MessageReadComplete += ( s, a ) => bodyReadCompleteCalled++;

            parser.ReadResponseHeaderComplete += ( s, a ) =>
                                                 {
                                                     readResponseCompleteCalled++;
                                                 };

            parser.PartialDataAvailable += ( s, a ) =>
                                           {
                                               Assert.That( a.Data, Is.EqualTo( body ) );
                                               partialDataAvailableCalled++;
                                           };

            parser.ReadRequestHeaderComplete += ( s, a ) => Assert.Fail( "Should not be invoked with HTTP response" );

            parser.AppendData( header );
            parser.AppendData( body );

            Assert.That( bodyAvailableCalled, Is.EqualTo(1) );
            Assert.That(bodyReadCompleteCalled, Is.EqualTo(1));
            Assert.That(partialDataAvailableCalled, Is.EqualTo(1));
            Assert.That(readResponseCompleteCalled, Is.EqualTo(1));
            Assert.That(additionalDataRequestedCalled, Is.EqualTo(1));
        }

        [Test]
        public void VerifySimpleHttpRequest()
        {
            bool bodyReadCompleteCalled = false;
            bool readCompleteCalled = false;
            int additionalDataRequestedCount = 0;

            byte[] data = File.ReadAllBytes( "testdata\\request.raw" );

            HttpStreamParser parser = new HttpStreamParser();

            parser.BodyAvailable += ( s, e ) => Assert.Fail( "Method should not have been called" );

            parser.AdditionalDataRequested += ( s, a ) => additionalDataRequestedCount++;

            parser.MessageReadComplete += ( s, a ) => bodyReadCompleteCalled = true;

            parser.ReadResponseHeaderComplete += ( s, a ) => Assert.Fail( "Should not be invoked with a request" );

            parser.PartialDataAvailable += ( s, a ) => Assert.Fail( "No data in body. Event should not have been invoked" );

            parser.ReadRequestHeaderComplete += ( s, a ) =>
                                                {
                                                    readCompleteCalled = true;
                                                    Assert.That( a.Method, Is.EqualTo( "GET" ) );
                                                    Assert.That( a.Path, Is.EqualTo( "/" ) );
                                                    Assert.That( a.Headers.Count, Is.EqualTo( 8 ) );
                                                    Assert.That( a.Headers["connection"], Is.EqualTo( "keep-alive" ) );
                                                };

            parser.AppendData( data );

            Assert.IsTrue( bodyReadCompleteCalled );
            Assert.IsTrue( readCompleteCalled );
            Assert.That(additionalDataRequestedCount, Is.EqualTo(0));
        }
    }
}