using System.Collections.Generic;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Filters;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Filters
{
    [TestFixture]
    public class HttpFilterTests
    {
        #region Setup/Teardown

        // TODO: verify writes to access log

        [SetUp]
        public void Setup()
        {
            _mockFilterCollections = new Mock<IFilterCollections>();
            _mockAccessLog = new Mock<IAccessLog>();
            _mockSettings = new Mock<ICoreSettings>();
            _mockRequest = new Mock<IHttpRequest>();
            _connectionFilters = new List<IConnectionFilter>();
            _whitelistEvaluators = new List<IWhitelistEvaluator>();

            _mockFilterCollections.Setup( m => m.ConnectionFilters ).Returns( _connectionFilters );
            _mockFilterCollections.Setup( m => m.WhitelistEvaluators ).Returns( _whitelistEvaluators );

            _filterUnderTest = new HttpFilter( _mockFilterCollections.Object, _mockAccessLog.Object, _mockSettings.Object );
        }

        #endregion

        private Mock<IFilterCollections> _mockFilterCollections;
        private List<IConnectionFilter> _connectionFilters;
        private List<IWhitelistEvaluator> _whitelistEvaluators;
        private Mock<IAccessLog> _mockAccessLog;
        private Mock<ICoreSettings> _mockSettings;
        private Mock<IHttpRequest> _mockRequest;
        private HttpFilter _filterUnderTest;

        [Test]
        [TestCase( true )]
        [TestCase( false )]
        public void RespectSettingsTest( bool isFilteringEnabled )
        {
            Mock<IConnectionFilter> connectionFilter = new Mock<IConnectionFilter>();
            connectionFilter.Setup(m => m.EvaluateFilter(_mockRequest.Object, "connectionId")).Returns("foo");
            _connectionFilters.Add(connectionFilter.Object);

            _mockSettings.SetupGet( m => m.FilteringEnabled ).Returns( isFilteringEnabled );

            byte[] results = _filterUnderTest.ApplyConnectionFilters( _mockRequest.Object, "connectionId" );

            if ( isFilteringEnabled )
            {
                Assert.That( results, Is.Not.Null );
                _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, AccessLogType.AccessBlocked), Times.Once());
            }
            else
            {
                Assert.That(results, Is.Null);
                _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, It.IsAny<AccessLogType>()), Times.Never());
            }

            IHttpResponseFilter responseFilters = _filterUnderTest.CreateResponseFilters( _mockRequest.Object, "connectionId" );

            if (isFilteringEnabled)
            {
                Assert.That(responseFilters, Is.Not.Null);
            }
            else
            {
                Assert.That(responseFilters, Is.Null);
            }
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void WhiteListTest(bool isWhitelisted)
        {
            _mockSettings.SetupGet(m => m.FilteringEnabled).Returns(true);

            Mock<IWhitelistEvaluator> whiteListEvaluator = new Mock<IWhitelistEvaluator>();
            whiteListEvaluator.SetupGet(m => m.FilterSpeedType).Returns(FilterSpeedType.LocalAndFast);
            whiteListEvaluator.Setup( m => m.IsWhitlisted( _mockRequest.Object, "connectionId" ) ).Returns( isWhitelisted);
            _whitelistEvaluators.Add(whiteListEvaluator.Object);

            Mock<IConnectionFilter> connectionFilter = new Mock<IConnectionFilter>();
            connectionFilter.Setup(m => m.EvaluateFilter(_mockRequest.Object, "connectionId")).Returns("foo");
            _connectionFilters.Add(connectionFilter.Object);

            byte[] connectionResults = _filterUnderTest.ApplyConnectionFilters(_mockRequest.Object, "connectionId");

            if(isWhitelisted)
            {
                Assert.That(connectionResults, Is.Null);
                _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, It.IsAny<AccessLogType>()), Times.Never());
            }
            else
            {
                Assert.That(connectionResults, Is.Not.Null);
                _mockAccessLog.Verify(m => m.Write("connectionId", _mockRequest.Object, AccessLogType.AccessBlocked), Times.Once());
                
            }

            IHttpResponseFilter results = _filterUnderTest.CreateResponseFilters(_mockRequest.Object, "connectionId");

            if(isWhitelisted)
            {
                Assert.That(results, Is.Null);
            }
            else
            {
                Assert.That(results, Is.Not.Null);
            }

        }

        [TestCase("foo", "bar")]
        [TestCase(null, "bar")]
        [TestCase("foo", null)]
        [TestCase(null, null)]
        [Test]
        public void ApplyConnectionFilterTest(string filter1, string filter2)
        {
            _mockSettings.SetupGet(m => m.FilteringEnabled).Returns(true);

            // Vary the filter speed type to verify sort

            Mock<IConnectionFilter> connectionFilter = new Mock<IConnectionFilter>();
            connectionFilter.Setup(m => m.EvaluateFilter(_mockRequest.Object, "connectionId")).Returns(filter1);
            connectionFilter.SetupGet(m => m.FilterSpeedType).Returns(FilterSpeedType.Remote);
            _connectionFilters.Add(connectionFilter.Object);

            Mock<IConnectionFilter> connectionFilter2 = new Mock<IConnectionFilter>();
            connectionFilter2.Setup(m => m.EvaluateFilter(_mockRequest.Object, "connectionId")).Returns(filter2);
            connectionFilter2.Setup(m => m.FilterSpeedType).Returns(FilterSpeedType.LocalAndFast);
            _connectionFilters.Add(connectionFilter2.Object);

            var results = _filterUnderTest.ApplyConnectionFilters(_mockRequest.Object, "connectionId");

            if(filter1 == null && filter2 == null)
            {
                Assert.That(results, Is.Null);
            }
            else
            {
                Assert.That(results, Is.Not.Null);

                if(filter1!=null && filter2 == null)
                {
                    Assert.That(Encoding.UTF8.GetString(results), Is.StringContaining(filter1));
                }
                if (filter2 != null)
                {
                    var rawString = Encoding.UTF8.GetString(results);

                    Assert.That(rawString, Is.StringContaining(filter2));

                    if(filter1!=null)
                    {
                        Assert.That(rawString, Is.Not.StringContaining(filter1), "The filter speed type was not respected");
                    }
                    
                }
            }
        }
    }
}