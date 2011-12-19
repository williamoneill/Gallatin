using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Gallatin.Core.Service.SessionState;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service.SessionState
{
    [TestFixture]
    public class SessionContextTests
    {
        #region Setup/Teardown

        [SetUp]
        public void SetupMocks()
        {
            _clientRequests = new Queue<string>();
            _serverResponses = new Queue<string>();

            _mockClient = new Mock<INetworkFacade>();
            _mockServer = new Mock<INetworkFacade>();

            _mockUnconnectedState = new Mock<ISessionState>();
            _mockClientConnectingState = new Mock<ISessionState>();
            _mockConnectedState = new Mock<ISessionState>();


            _mockRegistry = new Mock<ISessionStateRegistry>();
            _mockRegistry.Setup( m => m.GetState( SessionStateType.Unconnected ) ).Returns( _mockUnconnectedState.Object );
            _mockRegistry.Setup( m => m.GetState( SessionStateType.ClientConnecting ) ).Returns( _mockClientConnectingState.Object );
            _mockRegistry.Setup(m => m.GetState(SessionStateType.Connected)).Returns(_mockConnectedState.Object);
        }

        #endregion

        private Mock<INetworkFacade> _mockClient;
        private Mock<INetworkFacade> _mockServer;
        private Mock<ISessionState> _mockUnconnectedState;
        private Mock<ISessionState> _mockClientConnectingState;
        private Mock<ISessionState> _mockConnectedState;
        private Mock<ISessionStateRegistry> _mockRegistry;

        private Queue<string> _clientRequests;
        private Queue<string> _serverResponses;

        private void SetupClientRequests( Queue<string> requests )
        {
            _clientRequests = requests;

            _mockClient.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback<Action<bool, byte[], INetworkFacade>>(
                    callback =>
                    {
                        if ( _clientRequests.Count > 0 )
                        {
                            byte[] data = Encoding.UTF8.GetBytes( _clientRequests.Dequeue() );
                            callback( true, data, _mockClient.Object );
                        }
                    } );
        }

        private void SetupServerResponses( Queue<string> responses )
        {
            _serverResponses = responses;

            _mockServer.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback<Action<bool, byte[], INetworkFacade>>(
                    callback =>
                    {
                        if ( _serverResponses.Count > 0 )
                        {
                            byte[] data = Encoding.UTF8.GetBytes( _serverResponses.Dequeue() );
                            callback( true, data, _mockServer.Object );
                        }
                    } );
        }

        [Test]
        public void ChangeStateTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            classUnderTest.ChangeState( SessionStateType.Connected );   

            _mockConnectedState.Verify(m=>m.TransitionToState(classUnderTest), Times.Once());
        }

        [Test]
        public void BasicSendReceiveTest()
        {
            SessionContext classUnderTest = new SessionContext( _mockRegistry.Object );

            _clientRequests = new Queue<string>();
            _clientRequests.Enqueue( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            SetupClientRequests( _clientRequests );

            classUnderTest.Start( _mockClient.Object );

            _mockClientConnectingState.Verify(
                m => m.RequestHeaderAvailable(
                    It.Is<IHttpRequest>( r => r.HasBody == false && r.Headers["host"] == "www.yahoo.com" ), classUnderTest ),
                Times.Once() );
        }

        /// <summary>
        /// Verifies very basic state after a client connects
        /// </summary>
        [Test]
        public void VerifyBasicStartupBehavior()
        {
            SessionContext classUnderTest = new SessionContext( _mockRegistry.Object );

            Assert.That( classUnderTest.State, Is.SameAs( _mockUnconnectedState.Object ) );
            Assert.That( classUnderTest.Id, Is.Not.Null );

            classUnderTest.Start( _mockClient.Object );

            Assert.That( classUnderTest.ClientConnection, Is.Not.Null );
            Assert.That( classUnderTest.ClientConnection, Is.SameAs( _mockClient.Object ) );
            Assert.That( classUnderTest.ClientParser, Is.Not.Null );

            Assert.That( classUnderTest.State, Is.SameAs( _mockClientConnectingState.Object ) );

            Assert.That( classUnderTest.ServerConnection, Is.Null );
            Assert.That( classUnderTest.ServerParser, Is.Null );
            Assert.That( classUnderTest.Host, Is.Null );
            Assert.That( classUnderTest.Port, Is.EqualTo( 0 ) );
            Assert.That( classUnderTest.RecentRequestHeader, Is.Null );
            Assert.That( classUnderTest.RecentResponseHeader, Is.Null );
        }

        // TODO: test multiple message from server using VerifyServerSessionSetup as base test

        // TODO: test what happens when server and client connections are set to null

        [Test]
        public void ResetTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            classUnderTest.ChangeState(SessionStateType.Connected);

            classUnderTest.Reset();

            Assert.That(classUnderTest.State, Is.SameAs(_mockUnconnectedState.Object));
        }

        [Test]
        public void SendServerDataTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            classUnderTest.SetupServerConnection(_mockServer.Object);

            byte [] data = new byte[]{1,2,3};

            classUnderTest.SendServerData( data );

            _mockServer.Verify( m =>m.BeginSend(data, It.IsAny<Action<bool,INetworkFacade>>()) );
        }

        [Test]
        public void SendClientDataTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            classUnderTest.SetupClientConnection(_mockClient.Object);

            byte[] data = new byte[] { 1, 2, 3 };

            classUnderTest.SendClientData(data);

            _mockClient.Verify(m => m.BeginSend(data, It.IsAny<Action<bool, INetworkFacade>>()));
        }

        [Test]
        public void SessionEndedTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            bool eventRaised = false;

            classUnderTest.SessionEnded += ( s, e ) => eventRaised = true;

            classUnderTest.OnSessionEnded();

            Assert.That(eventRaised);
        }

        [Test]
        public void ChangeServerConnectionTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            classUnderTest.SetupServerConnection( _mockServer.Object );

            var parser = classUnderTest.ServerParser;

            classUnderTest.SetupServerConnection( _mockClient.Object );

            Assert.That(classUnderTest.ServerParser, Is.Not.SameAs(parser));
            Assert.That( classUnderTest.ServerConnection, Is.SameAs(_mockClient.Object) );

            byte[] data = new byte[]{1,2,3};
            classUnderTest.SendServerData(data);

            _mockClient.Verify(m=>m.BeginSend(data, It.IsAny<Action<bool,INetworkFacade>>()), Times.Once());
            _mockServer.Verify(m => m.BeginSend(data, It.IsAny<Action<bool, INetworkFacade>>()), Times.Never());
        }

        [Test]
        public void ChangeClientConnectionTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            classUnderTest.SetupClientConnection(_mockServer.Object);

            var parser = classUnderTest.ClientParser;

            classUnderTest.SetupClientConnection(_mockClient.Object);

            Assert.That(classUnderTest.ClientParser, Is.Not.SameAs(parser));
            Assert.That(classUnderTest.ClientConnection, Is.SameAs(_mockClient.Object));

            byte[] data = new byte[] { 1, 2, 3 };
            classUnderTest.SendClientData(data);

            _mockClient.Verify(m => m.BeginSend(data, It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            _mockServer.Verify(m => m.BeginSend(data, It.IsAny<Action<bool, INetworkFacade>>()), Times.Never());
        }

        [Test]
        public void NullServerConnectionTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            classUnderTest.SetupServerConnection(_mockServer.Object);
            classUnderTest.ChangeState(SessionStateType.Connected);

            var serverParser = classUnderTest.ServerParser;

            classUnderTest.SetupServerConnection(null);

            // Raise events using the old parser. Simulates network traffic after discconect. No events should
            // be passed to the state since the internal event handlers should be unwired.
            byte[] data = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\ncontent-length: 2\r\n\r\nhi");
            serverParser.AppendData(data);

            Assert.That(classUnderTest.ServerParser, Is.Null);
            Assert.That(classUnderTest.ServerConnection, Is.Null);

            _mockConnectedState.Verify(m => m.ResponseHeaderAvailable(It.IsAny<IHttpResponse>(), It.IsAny<ISessionContext>()), Times.Never());
            _mockConnectedState.Verify(m => m.ShouldSendPartialDataToServer(It.IsAny<byte[]>(), It.IsAny<ISessionContext>()), Times.Never());

            Assert.Throws<InvalidOperationException>( () => classUnderTest.SendServerData( data ) );
        }

        [Test]
        public void NullClientConnectionTest()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            classUnderTest.SetupClientConnection(_mockClient.Object);
            classUnderTest.ChangeState(SessionStateType.Connected);

            var clientParser = classUnderTest.ClientParser;

            classUnderTest.SetupClientConnection(null);

            // Raise events using the old parser. Simulates network traffic after discconect. No events should
            // be passed to the state since the internal event handlers should be unwired.
            byte[] data = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            clientParser.AppendData(data);

            Assert.That(classUnderTest.ClientParser, Is.Null);
            Assert.That(classUnderTest.ClientConnection, Is.Null);

            _mockConnectedState.Verify(m=>m.RequestHeaderAvailable(It.IsAny<IHttpRequest>(), It.IsAny<ISessionContext>()), Times.Never());
            _mockConnectedState.Verify(m=>m.ShouldSendPartialDataToClient(It.IsAny<byte[]>(), It.IsAny<ISessionContext>()), Times.Never());

            Assert.Throws<InvalidOperationException>(() => classUnderTest.SendClientData(data));
        }

        [Test]
        public void ResponseBodyRequestedTest()
        {
            byte[] data = null;
            int counter = 0;
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            Queue<string> responses = new Queue<string>();
            responses.Enqueue("HTTP/1.1 200 OK\r\ncontent-length: 2\r\n\r\nhi");
            responses.Enqueue("HTTP/1.1 200 OK\r\ncontent-length: 3\r\n\r\nbye");
            SetupServerResponses(responses);

            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            _mockConnectedState.Setup( m => m.ResponseHeaderAvailable( It.IsAny<IHttpResponse>(), classUnderTest ) )
                .Callback( () =>
                           {
                               if(counter == 0)
                               {
                                   classUnderTest.HttpResponseBodyRequested(
                                       ( b, s ) =>
                                       {
                                           counter++;
                                           data = b;
                                           resetEvent.Set();
                                       } );
                               }
                           }
);

            classUnderTest.ChangeState(SessionStateType.Connected);
            classUnderTest.SetupServerConnection(_mockServer.Object);

            Assert.That(resetEvent.WaitOne(2000), "Timed out waiting for event");
            Assert.That(Encoding.UTF8.GetString(data), Is.EqualTo("hi"));
            Assert.That(counter, Is.EqualTo(1), "The counter should only increment once since the body was only requested once");
        }

        [Test]
        public void TestMultipleMessagesFromClient()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            _mockConnectedState.Setup(m => m.ShouldSendPartialDataToClient(It.IsAny<byte[]>(), It.IsAny<ISessionContext>())).Returns(true);
            _mockConnectedState.Setup(m => m.ShouldSendPartialDataToServer(It.IsAny<byte[]>(), It.IsAny<ISessionContext>())).Returns(true);
            
            Queue<string> requests = new Queue<string>();
            requests.Enqueue("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n");
            requests.Enqueue("GET /foo.jpg HTTP/1.1\r\nHost: www.cnn.com\r\n\r\n");
            requests.Enqueue("GET /cat/dog.html HTTP/1.1\r\nHost: www.foxnews.com\r\n\r\n");
            SetupClientRequests(requests);

            classUnderTest.ChangeState(SessionStateType.Connected);

            classUnderTest.SetupClientConnection(_mockClient.Object);

            _mockClient.Raise( m=>m.ConnectionClosed += null, new EventArgs());

            _mockConnectedState.Verify(
                m => m.RequestHeaderAvailable(It.IsAny<IHttpRequest>(), It.IsAny<ISessionContext>()), Times.Exactly(3));

            _mockConnectedState.Verify(
                m => m.ShouldSendPartialDataToClient(It.IsAny<byte[]>(), classUnderTest), Times.Never());

            _mockConnectedState.Verify(
                m => m.RequestHeaderAvailable(It.Is<IHttpRequest>(s => s.Path == "/"), classUnderTest), Times.Once());
            _mockConnectedState.Verify(
                m => m.RequestHeaderAvailable(It.Is<IHttpRequest>(s => s.Path == "/foo.jpg"), classUnderTest), Times.Once());
            _mockConnectedState.Verify(
                m => m.RequestHeaderAvailable(It.Is<IHttpRequest>(s => s.Path == "/cat/dog.html"), classUnderTest), Times.Once());
            _mockConnectedState.Verify(
                m => m.RequestHeaderAvailable(It.IsAny<IHttpRequest>(), classUnderTest), Times.Exactly(3));

            _mockConnectedState.Verify(
                m => m.ResponseHeaderAvailable(It.IsAny<IHttpResponse>(), classUnderTest), Times.Never());

            _mockConnectedState.Verify(
                m => m.SentFullServerResponseToClient(It.IsAny<IHttpResponse>(), classUnderTest), Times.Never());

            _mockConnectedState.Verify(
                m => m.ShouldSendPartialDataToServer(It.IsAny<byte[]>(), classUnderTest), Times.Never(),
                "No data should have been sent to the server");

            _mockConnectedState.Verify(
                m => m.ShouldSendPartialDataToClient(It.IsAny<byte[]>(), classUnderTest), Times.Never(),
                "No data should have been sent back to the client");

            _mockClient.Verify(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()), Times.Exactly(4),
                "The proxy should have repeatedly requested new data from the client");
            _mockServer.Verify(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()), Times.Never());
            
        }

        [Test]
        public void TestMultipleMessagesFromServer()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            _mockConnectedState.Setup( m => m.ShouldSendPartialDataToClient( It.IsAny<byte[]>(), It.IsAny<ISessionContext>() ) ).Returns( true );
            _mockConnectedState.Setup(m => m.ShouldSendPartialDataToServer(It.IsAny<byte[]>(), It.IsAny<ISessionContext>())).Returns(true);

            // Once the server connection is established, the class under test will request messages from the server.
            Queue<string> responses = new Queue<string>();
            responses.Enqueue("HTTP/1.1 200 OK\r\ncontent-length: 2\r\n\r\nhi");
            responses.Enqueue("HTTP/1.1 201 OK\r\ncontent-length: 0\r\n\r\n");
            responses.Enqueue("HTTP/1.1 304 Not modified\r\n\r\n");
            SetupServerResponses(responses);

            classUnderTest.ChangeState(SessionStateType.Connected);

            classUnderTest.SetupServerConnection(_mockServer.Object);

            _mockServer.Raise(m => m.ConnectionClosed += null, new EventArgs());

            //
            // Verify...
            //
            _mockConnectedState.Verify(
                m => m.RequestHeaderAvailable(It.IsAny<IHttpRequest>(), It.IsAny<ISessionContext>()), Times.Never());

            _mockConnectedState.Verify(
                m => m.ShouldSendPartialDataToClient(It.IsAny<byte[]>(), classUnderTest), Times.Once());

            _mockConnectedState.Verify(
                m => m.ResponseHeaderAvailable(It.Is<IHttpResponse>( s => s.Status == 200 ), classUnderTest), Times.Once());
            _mockConnectedState.Verify(
                m => m.ResponseHeaderAvailable(It.Is<IHttpResponse>(s => s.Status == 201), classUnderTest), Times.Once());
            _mockConnectedState.Verify(
                m => m.ResponseHeaderAvailable(It.Is<IHttpResponse>(s => s.Status == 304), classUnderTest), Times.Once());
            _mockConnectedState.Verify(
                m => m.ResponseHeaderAvailable(It.IsAny<IHttpResponse>(), classUnderTest), Times.Exactly(3));

            _mockConnectedState.Verify(
                m => m.SentFullServerResponseToClient(It.IsAny<IHttpResponse>(), classUnderTest), Times.Exactly(3));

            _mockConnectedState.Verify(
                m => m.ShouldSendPartialDataToServer(It.IsAny<byte[]>(), classUnderTest), Times.Never(),
                "No data should have been sent back to the server");

            _mockServer.Verify(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()), Times.Exactly(4), 
                "The proxy should have repeatedly requested new data from the server");
        }

        /// <summary>
        /// Verifies that the class under test wires up to the server connection events correctly
        /// </summary>
        [Test]
        public void VerifyServerSessionSetup()
        {
            SessionContext classUnderTest = new SessionContext( _mockRegistry.Object );

            // Once the server connection is established, the class under test will request a message from the server.
            // Provide a complete message here.
            Queue<string> responses = new Queue<string>();
            responses.Enqueue( "HTTP/1.1 200 OK\r\ncontent-length: 2\r\n\r\nhi" );
            SetupServerResponses( responses );

            byte[] bodyData = Encoding.UTF8.GetBytes( "hi" );

            classUnderTest.ChangeState( SessionStateType.Connected );

            classUnderTest.SetupServerConnection( _mockServer.Object );

            Assert.That( classUnderTest.ServerParser, Is.Not.Null );
            Assert.That( classUnderTest.ServerConnection, Is.SameAs( _mockServer.Object ) );

            _mockServer.Raise( m => m.ConnectionClosed += null, new EventArgs() );

            Assert.That( classUnderTest.State, Is.SameAs(_mockUnconnectedState.Object) );

            //
            // Verify...
            //

            _mockConnectedState.Verify(
                m => m.RequestHeaderAvailable( It.IsAny<IHttpRequest>(), It.IsAny<ISessionContext>() ), Times.Never() );

            _mockConnectedState.Verify(
                m => m.ShouldSendPartialDataToClient(It.IsAny<byte[]>(), classUnderTest), Times.Once());

            _mockConnectedState.Verify(
                m => m.ResponseHeaderAvailable(classUnderTest.RecentResponseHeader, classUnderTest), Times.Once());
            
            _mockConnectedState.Verify(
                m => m.SentFullServerResponseToClient( classUnderTest.RecentResponseHeader, classUnderTest ), Times.Once() );
            
            _mockConnectedState.Verify(
                m => m.ShouldSendPartialDataToServer( bodyData, classUnderTest ), Times.Never());

            _mockServer.Verify( m => m.BeginReceive( It.IsAny<Action<bool,byte[],INetworkFacade>>() ), Times.Exactly(2) );
        }

        /// <summary>
        /// Verifies that the class under test wires up to the client connection events correctly
        /// </summary>
        [Test]
        public void VerifyClientSessionSetup()
        {
            SessionContext classUnderTest = new SessionContext(_mockRegistry.Object);

            // Once the client connection is established, the class under test will request a message from the server.
            // Provide a complete message here.
            _clientRequests = new Queue<string>();
            _clientRequests.Enqueue("GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n");
            SetupClientRequests(_clientRequests);

            classUnderTest.ChangeState(SessionStateType.ClientConnecting);

            classUnderTest.SetupClientConnection(_mockClient.Object);

            Assert.That(classUnderTest.ClientParser, Is.Not.Null);
            Assert.That(classUnderTest.ClientConnection, Is.SameAs(_mockClient.Object));
            Assert.That(classUnderTest.ServerParser, Is.Null);
            Assert.That(classUnderTest.ServerConnection, Is.Null);

            _mockClient.Raise(m => m.ConnectionClosed += null, new EventArgs());

            Assert.That(classUnderTest.State, Is.SameAs(_mockUnconnectedState.Object));

            //
            // Verify...
            //

            _mockClientConnectingState.Verify(
                m => m.RequestHeaderAvailable(classUnderTest.RecentRequestHeader, classUnderTest), Times.Once());

            _mockClientConnectingState.Verify(
                m => m.ResponseHeaderAvailable( It.IsAny<IHttpResponse>(), It.IsAny<ISessionContext>() ), Times.Never());

            _mockClientConnectingState.Verify(
                m => m.SentFullServerResponseToClient(It.IsAny<IHttpResponse>(), It.IsAny<ISessionContext>()), Times.Never());

            _mockClientConnectingState.Verify(
                m => m.ShouldSendPartialDataToClient( It.IsAny<byte[]>(), It.IsAny<ISessionContext>() ), Times.Never());

            _mockClientConnectingState.Verify(
                m => m.ShouldSendPartialDataToServer(It.IsAny<byte[]>(), It.IsAny<ISessionContext>()), Times.Never());

            _mockClient.Verify(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()), Times.Exactly(2));
        }

    }
}