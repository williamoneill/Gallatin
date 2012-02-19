using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Filters;
using Gallatin.Core.Service;
using Moq;
using Moq.Language.Flow;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Filters
{
    [TestFixture]
    public class HttpResponseFilterTests
    {
        private Mock<IHttpResponse> _mockResponse;
        private Mock<IFilterCollections> _mockFilterCollections;
        private Mock<IAccessLog> _mockAccessLog;
        private Mock<IHttpRequest> _mockRequest;
        private HttpResponseFilter _filterUnderTest;
        private List<IResponseFilter> _responseFilters;
        private Mock<IHttpHeaders> _mockHeaders;
            
        [SetUp]
        public void Setup()
        {
            _mockResponse = new Mock<IHttpResponse>();
            _mockFilterCollections = new Mock<IFilterCollections>();
            _mockAccessLog = new Mock<IAccessLog>();
            _mockRequest = new Mock<IHttpRequest>();
            _mockHeaders = new Mock<IHttpHeaders>();

            _responseFilters = new List<IResponseFilter>();

            _mockFilterCollections.SetupGet(m => m.ResponseFilters).Returns(_responseFilters);

            _mockResponse.SetupGet(m => m.Headers).Returns(_mockHeaders.Object);

            _filterUnderTest = new HttpResponseFilter(_mockRequest.Object, "connectionId", _mockAccessLog.Object, _mockFilterCollections.Object);
        }

        /// <summary>
        /// Verify that nothing is returned if no filters are applied
        /// </summary>
        [Test]
        public void NoFiltersTest()
        {
            IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> callbacks;
            var response = _filterUnderTest.ApplyResponseHeaderFilters(_mockResponse.Object, out callbacks);

            Assert.That(response, Is.Null);
            Assert.That(callbacks, Is.Null);

            _mockAccessLog.Verify(m=>m.Write("connectionId", _mockRequest.Object, AccessLogType.AccessGranted), Times.Once());
        }

        [Test]
        public void VerifyResponseFilterThatDoesNotNeedBody()
        {
            Mock<IResponseFilter> responseFilter = new Mock<IResponseFilter>();

            Func<IHttpResponse, string, byte[], byte[]> responseDelegate = null;
            responseFilter.Setup(m => m.EvaluateFilter(_mockResponse.Object, "connectionId", out responseDelegate)).Returns("fubar");
            _responseFilters.Add(responseFilter.Object);

            IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> callbacks;
            var response = _filterUnderTest.ApplyResponseHeaderFilters(_mockResponse.Object, out callbacks);

            Assert.That(callbacks, Is.Null);
            Assert.That(response, Is.Not.Null);

            Assert.That(Encoding.UTF8.GetString(response), Is.StringContaining("fubar"));

            _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, AccessLogType.AccessBlocked), Times.Once());
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void VerifyResponseFilterThatNeedsBody(bool hasBody)
        {
            Mock<IResponseFilter> responseFilter = new Mock<IResponseFilter>();

            Func<IHttpResponse, string, byte[], byte[]> responseDelegate = (httpResponse, s, arg3) => Encoding.UTF8.GetBytes( "kung fu" );
            responseFilter.Setup(m => m.EvaluateFilter(_mockResponse.Object, "connectionId", out responseDelegate)).Returns(null as string);
            _responseFilters.Add(responseFilter.Object);

            _mockResponse.SetupGet(m => m.HasBody).Returns(hasBody);

            IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> callbacks;
            var response = _filterUnderTest.ApplyResponseHeaderFilters(_mockResponse.Object, out callbacks);

            if(hasBody)
            {
                Assert.That(callbacks, Is.Not.Null);
                Assert.That(callbacks, Has.Count.EqualTo(1));
                Assert.That(callbacks.First(), Is.EqualTo(responseDelegate));
                _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, It.IsAny<AccessLogType>()), Times.Never());
            }
            else
            {
                Assert.That(callbacks, Is.Null);
                _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, AccessLogType.AccessGranted), Times.Once());
            }

            Assert.That(response, Is.Null);
        }

        [Test]
        [TestCase("kung fu")]
        [TestCase(null)]
        public void ApplyResponseBodyFilterTest(string filterBody)
        {
            byte[] body = Encoding.UTF8.GetBytes( "foo" );

            _mockResponse.Setup(m => m.GetBuffer()).Returns(Encoding.UTF8.GetBytes("hdr"));

            Func<IHttpResponse, string, byte[], byte[]> responseDelegate = (httpResponse, s, arg3) =>
                                                                               {
                                                                                   if(filterBody==null)
                                                                                       return null;
                                                                                   return
                                                                                       Encoding.UTF8.GetBytes(filterBody);
                                                                               };

            List<Func<IHttpResponse, string, byte[], byte[]>> callbacks = new List<Func<IHttpResponse, string, byte[], byte[]>>();
            callbacks.Add(responseDelegate);

            var results  = _filterUnderTest.ApplyResponseBodyFilter(_mockResponse.Object, body, callbacks);

            Assert.That(results, Is.Not.Null);

            _mockHeaders.Verify(m => m.RemoveKeyValue("transfer-encoding", "chunked"), Times.Once());

            if (filterBody != null)
            {
                Assert.That(results, Is.EqualTo(Encoding.UTF8.GetBytes("hdr"+filterBody)), "The header and body were not joined correctly");
                _mockHeaders.Verify(m => m.UpsertKeyValue("Content-Length", filterBody.Length.ToString()), Times.Once(), "The content length should have been updated to the length of the new body");
                _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, AccessLogType.AccessBlocked), Times.Once());
            }
            else
            {
                _mockHeaders.Verify(m => m.UpsertKeyValue("Content-Length", It.IsAny<string>()), Times.Never());
                _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, AccessLogType.AccessGranted), Times.Once());
                
            }
        }
    }
}
