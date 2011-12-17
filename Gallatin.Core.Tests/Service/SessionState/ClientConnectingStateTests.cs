using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Gallatin.Core.Service.SessionState;
using Gallatin.Core.Web;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service.SessionState
{
    [TestFixture]
    public class ClientConnectingStateTests
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

            _mockHeaders.SetupGet( m => m["Host"] ).Returns( "www.yahoo.com" );

            _mockRequest.SetupGet( m => m.Headers ).Returns( _mockHeaders.Object );

            _mockClient.SetupGet( m => m.ConnectionId ).Returns( ConnectionId );

            _mockContext.SetupGet( m => m.ClientConnection ).Returns( _mockClient.Object );
        }

        [Test]
        public void SendClientDataTest()
        {
            ClientConnectingState state = new ClientConnectingState(_mockFilter.Object, _mockFactory.Object);

            Assert.Throws<InvalidOperationException>( () => state.ShouldSendPartialClientData( new byte[1], _mockContext.Object ) );
        }

        [Test]
        public void SendServerDataTest()
        {
            ClientConnectingState state = new ClientConnectingState(_mockFilter.Object, _mockFactory.Object);

            Assert.Throws<InvalidOperationException>( () => state.ShouldSendPartialServerData( new byte[1], _mockContext.Object ) );
        }

        [Test]
        public void ConnectToServerTest()
        {
            ClientConnectingState state = new ClientConnectingState(_mockFilter.Object, _mockFactory.Object);

            _mockRequest.SetupGet( m => m.IsSsl ).Returns( false );

            _mockFactory.Setup( m => m.BeginConnect( "www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<string, int, Action<bool, INetworkFacade>>( ( s, i, c ) => c( true, _mockServer.Object ) );

            _mockFilter.Setup( m => m.EvaluateConnectionFilters( _mockRequest.Object, ConnectionId ) ).Returns( null as string );

            state.RequestHeaderAvailable( _mockRequest.Object, _mockContext.Object );

            _mockContext.Verify(m=>m.SetupServerConnection(_mockServer.Object), Times.Once());
            _mockContext.Verify(m=>m.ChangeState(SessionStateType.Connected), Times.Once());
        }

        [Test]
        public void ConnectFilterTest()
        {
            ClientConnectingState state = new ClientConnectingState(_mockFilter.Object, _mockFactory.Object);

            _mockFilter.Setup(m => m.EvaluateConnectionFilters(_mockRequest.Object, ConnectionId)).Returns("filter activated");

            state.RequestHeaderAvailable(_mockRequest.Object, _mockContext.Object);

            _mockContext.Verify(m => m.ChangeState(SessionStateType.Unconnected), Times.Once());

            _mockContext.Verify(m=>m.SendClientData(It.IsAny<byte[]>()), Times.Once());
            
            _mockFactory.Verify(m=>m.BeginConnect(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Action<bool,INetworkFacade>>()), Times.Never());
        }

        [Test]
        public void ServerConnectionFailureTest()
        {
            ClientConnectingState state = new ClientConnectingState(_mockFilter.Object, _mockFactory.Object);

            _mockRequest.SetupGet(m => m.IsSsl).Returns(false);

            _mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((s, i, c) => c(false, _mockServer.Object));

            _mockFilter.Setup(m => m.EvaluateConnectionFilters(_mockRequest.Object, ConnectionId)).Returns(null as string);

            state.RequestHeaderAvailable(_mockRequest.Object, _mockContext.Object);

            _mockContext.Verify(m => m.SetupServerConnection(_mockServer.Object), Times.Never());
            _mockContext.Verify(m => m.ChangeState(SessionStateType.Unconnected), Times.Once());
        }

        [Test]
        public void SslTest()
        {
            ClientConnectingState state = new ClientConnectingState(_mockFilter.Object, _mockFactory.Object);

            _mockRequest.SetupGet( m => m.Path ).Returns( "www.gmail.com:443" );

            _mockRequest.SetupGet(m => m.IsSsl).Returns(true);

            ManualResetEvent resetEvent = new ManualResetEvent(false);

            _mockFactory.Setup(m => m.BeginConnect("www.gmail.com", 443, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((s, i, c) =>
                                                                     {
                                                                         c( true, _mockServer.Object );
                                                                         resetEvent.Set();
                                                                     });

            _mockFilter.Setup(m => m.EvaluateConnectionFilters(_mockRequest.Object, ConnectionId)).Returns(null as string);

            state.RequestHeaderAvailable(_mockRequest.Object, _mockContext.Object);

            _mockContext.VerifySet(m=>m.Port = 443);
            _mockContext.VerifySet(m => m.Host = "www.gmail.com");

            Assert.That(resetEvent.WaitOne(2000), "Timed out waiting for state change");

            _mockContext.Verify(m => m.ChangeState(SessionStateType.Https), Times.Once());
        }

        [Test]
        public void InvalidSslTest()
        {
            ClientConnectingState state = new ClientConnectingState(_mockFilter.Object, _mockFactory.Object);

            _mockRequest.SetupGet(m => m.Path).Returns("www.gmail.com");

            _mockRequest.SetupGet(m => m.IsSsl).Returns(true);

            _mockFilter.Setup(m => m.EvaluateConnectionFilters(_mockRequest.Object, ConnectionId)).Returns(null as string);

            Assert.Throws<InvalidDataException>( ()=> state.RequestHeaderAvailable(_mockRequest.Object, _mockContext.Object));
        }


    }
}
