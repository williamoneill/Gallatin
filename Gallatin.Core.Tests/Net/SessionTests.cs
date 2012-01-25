using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Net;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;
using IServerDispatcher = Gallatin.Core.Net.IServerDispatcher;

namespace Gallatin.Core.Tests.Net
{

    [TestFixture]
    public class SessionTests
    {
        private Mock<IAccessLog> _mockLog;
        private Mock<IServerDispatcher> _mockDispatcher;
        private Mock<INetworkConnection> _mockClient;

        [SetUp]
        public void Setup()
        {
            _mockClient = new Mock<INetworkConnection>();
            _mockLog = new Mock<IAccessLog>();
            _mockDispatcher = new Mock<IServerDispatcher>();
        }

        [Test]
        public void InitializeTest()
        {
            Session session = new Session(_mockLog.Object, _mockDispatcher.Object);

            _mockDispatcher.VerifySet(m=>m.Logger = It.IsAny<ISessionLogger>(), Times.Once());
        }

        [Test]
        public void SimpleStart()
        {
            Session session = new Session(_mockLog.Object, _mockDispatcher.Object);

            session.Start(_mockClient.Object);

            _mockClient.Verify(m=>m.Start(), Times.Once());
            _mockClient.VerifySet(m=>m.Logger = It.IsAny<ISessionLogger>(), Times.Once());
        }

        [Test]
        public void ResetWithoutConnectTest()
        {
            Session session = new Session(_mockLog.Object, _mockDispatcher.Object);

            session.Reset();
        }

        [Test]
        public void ClientSuddenlyDisconnectsAfterConnect([Values(true, false)]bool shouldReset, [Values(true, false)]bool connectionClose, [Values(true, false)]bool connectionReset)
        {
            Session session = new Session(_mockLog.Object, _mockDispatcher.Object);

            session.Start(_mockClient.Object);

            if (connectionReset)
                _mockClient.Raise(m => m.Shutdown += null, new EventArgs());

            if (connectionClose)
                _mockClient.Raise(m => m.ConnectionClosed += null, new EventArgs());

            // This should not cause an exception regardless
            if(shouldReset)
                session.Reset();

            if (connectionClose || shouldReset || connectionReset)
            {
                _mockDispatcher.Verify(m => m.Reset(), Times.Once());
                _mockClient.Verify(m => m.Close(), Times.Once());
            }
            else
            {
                _mockDispatcher.Verify(m => m.Reset(), Times.Never());
                _mockClient.Verify(m => m.Close(), Times.Never());
            }
        }

        [Test]
        public void SimpleConnectTest()
        {
            string header = "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n";
            var rawHeader = Encoding.UTF8.GetBytes( header );

            byte[] dataFromServer = new byte[]{1,2,3};

            _mockDispatcher.Setup( m => m.ConnectToServer( "www.yahoo.com", 80, It.IsAny<Action<bool>>() ) )
                .Callback<string, int, Action<Boolean>>( ( a, b, c ) => c( true ) );

            _mockDispatcher.Setup( m => m.TrySendDataToActiveServer( rawHeader ) ).Returns( true );

            Session session = new Session(_mockLog.Object, _mockDispatcher.Object);

            session.Start(_mockClient.Object);

            _mockClient.Raise( m => m.DataAvailable += null, new DataAvailableEventArgs(rawHeader) );

            _mockDispatcher.Raise(m=>m.ServerDataAvailable += null, new DataAvailableEventArgs(dataFromServer));

            _mockDispatcher.Verify(m => m.TrySendDataToActiveServer(rawHeader), Times.Once());
            _mockClient.Verify(m=>m.Close(), Times.Never());
            _mockClient.Verify(m=>m.SendData(dataFromServer), Times.Once());
        }
    }
}
