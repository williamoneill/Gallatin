using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Gallatin.Core.Service.SessionState;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service.SessionState
{
    [TestFixture]
    public class ConnectedStateTests
    {
        private Mock<ISessionContext> _mockContext;
        private Mock<IHttpRequest> _mockRequest;
        private Mock<IProxyFilter> _mockFilter;
        private Mock<INetworkFacadeFactory> _mockFactory;
        private Mock<INetworkFacade> _mockClient;
        private Mock<INetworkFacade> _mockServer;
        private Mock<IHttpHeaders> _mockHeaders;

        private const string ConnectionId = "127.0.0.1:40";

        [SetUp]
        public void Setup()
        {
            _mockClient = new Mock<INetworkFacade>();
            _mockContext = new Mock<ISessionContext>();
            _mockRequest = new Mock<IHttpRequest>();
            _mockFilter = new Mock<IProxyFilter>();
            _mockFactory = new Mock<INetworkFacadeFactory>();
            _mockHeaders = new Mock<IHttpHeaders>();
            _mockServer = new Mock<INetworkFacade>();

            _mockContext.SetupAllProperties();

            _mockHeaders.SetupGet(m => m["Host"]).Returns("www.yahoo.com");

            _mockRequest.SetupGet(m => m.Headers).Returns(_mockHeaders.Object);

            _mockClient.SetupGet(m => m.ConnectionId).Returns(ConnectionId);

            _mockContext.SetupGet(m => m.ClientConnection).Returns(_mockClient.Object);
            _mockContext.SetupGet( m => m.RecentRequestHeader ).Returns( _mockRequest.Object );
        }

        [Test]
        public void TransitionTest()
        {
            ConnectedState state = new ConnectedState(_mockFilter.Object);

            byte[] data = new byte[]{1,2,3};

            _mockRequest.Setup(m => m.GetBuffer()).Returns(data);

            state.TransitionToState(_mockContext.Object);

            _mockContext.Verify(m=>m.SendServerData(data), Times.Once());
        }

    }
}
