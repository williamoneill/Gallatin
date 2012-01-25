using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ServerDispatcherTests
    {
        Mock<INetworkFacadeFactory> _mockFactory = new Mock<INetworkFacadeFactory>();
        Mock<IHttpRequest> _mockRequest = new Mock<IHttpRequest>();
        Mock<IHttpHeaders> _mockHeaders = new Mock<IHttpHeaders>();
        Mock<INetworkFacade> _mockServer = new Mock<INetworkFacade>();

        Mock<IHttpRequest> _mockCnnRequest = new Mock<IHttpRequest>();
        Mock<IHttpHeaders> _mockCnnHeaders = new Mock<IHttpHeaders>();
        Mock<INetworkFacade> _mockCnnServer = new Mock<INetworkFacade>();


        [SetUp]
        public void TestSetup()
        {
            _mockRequest.SetupGet(m => m.Headers).Returns(_mockHeaders.Object);
            _mockHeaders.SetupGet(m => m["Host"]).Returns("www.yahoo.com");
            _mockRequest.SetupGet(m => m.Path).Returns("/");

            _mockCnnRequest.SetupGet(m => m.Headers).Returns(_mockCnnHeaders.Object);
            _mockCnnHeaders.SetupGet(m => m["Host"]).Returns("www.cnn.com");
            _mockCnnRequest.SetupGet(m => m.Path).Returns("/foo.jpg");
        }

        [Test]
        public void VerifyCreateState()
        {
            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();

            ServerDispatcher dispatcher = new ServerDispatcher( mockFactory.Object );

            Assert.That(dispatcher.PipeLineDepth, Is.EqualTo(0));

        }

        [Test]
        public void ConnectToNewServer()
        {
            _mockFactory.Setup( m => m.BeginConnect( "www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<string, int, Action<bool, INetworkFacade>>( (h,p,c) =>
                                                         {
                                                             c( true, _mockServer.Object );
                                                         } );


            ServerDispatcher dispatcher = new ServerDispatcher(_mockFactory.Object);

            bool connectOk = false;
            dispatcher.BeginConnect( _mockRequest.Object, ( b, request ) => connectOk = true );

            Assert.That(connectOk);
        }

        [Test]
        public void SecondConnectSameHost()
        {
            int facadeConnectCount = 0;
            int connectCallbackCount = 0;

            _mockRequest.SetupGet(m => m.Headers).Returns(_mockHeaders.Object);
            _mockHeaders.SetupGet(m => m["Host"]).Returns("www.yahoo.com");
            _mockRequest.SetupGet(m => m.Path).Returns("/");

            _mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((h, p, c) =>
                                                                     {
                                                                         facadeConnectCount++;
                    c(true, _mockServer.Object);
                });


            ServerDispatcher dispatcher = new ServerDispatcher(_mockFactory.Object);

            // Connect to the same server twice. Only one socket connection should be established.
            dispatcher.BeginConnect(_mockRequest.Object, (b, request) => connectCallbackCount++);
            dispatcher.BeginConnect(_mockRequest.Object, (b, request) => connectCallbackCount++);

            Assert.That(connectCallbackCount, Is.EqualTo(2), "The actual server connection will only occur once, but callback should be invoked twice.");
            Assert.That(facadeConnectCount, Is.EqualTo(1));
        }

        /// <summary>
        /// Connects to Yahoo, then CNN, then Yahoo again. The actual network should only be hit twice and the
        /// Yahoo connection should be reused.
        /// </summary>
        [Test]
        public void ConnectToSecondServerDifferentHost()
        {
            int yahooConnectCount = 0;
            int cnnConnectCount = 0;

            int yahooConnectCallbackCount = 0;
            int cnnConnectCallbackCount = 0;

            _mockFactory.Setup(m => m.BeginConnect("www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((h, p, c) =>
                {
                    yahooConnectCount++;
                    c(true, _mockServer.Object);
                });

            _mockFactory.Setup(m => m.BeginConnect("www.cnn.com", 80, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<string, int, Action<bool, INetworkFacade>>((h, p, c) =>
                {
                    cnnConnectCount++;
                    c(true, _mockCnnServer.Object);
                });

            ServerDispatcher dispatcher = new ServerDispatcher(_mockFactory.Object);

            dispatcher.BeginConnect(_mockRequest.Object, (b, request) => yahooConnectCallbackCount++);
            dispatcher.BeginConnect(_mockCnnRequest.Object, (b, request) => cnnConnectCallbackCount++);
            dispatcher.BeginConnect(_mockRequest.Object, (b, request) => yahooConnectCallbackCount++);

            Assert.That(yahooConnectCount, Is.EqualTo(1));
            Assert.That(cnnConnectCount, Is.EqualTo(1));

            Assert.That(yahooConnectCallbackCount, Is.EqualTo(2));
            Assert.That(cnnConnectCallbackCount, Is.EqualTo(1));
        }
    }
}
