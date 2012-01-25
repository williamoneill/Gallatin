﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Core.Net;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Net
{
    [TestFixture]
    public class ServerDispatcherTests
    {
        Mock<INetworkConnectionFactory> _factory;
        Mock<INetworkConnection> _connection;

        [SetUp]
        public void Setup()
        {
            _factory = new Mock<INetworkConnectionFactory>();
            _connection = new Mock<INetworkConnection>();
            
        }

        /// <summary>
        /// Verify the server connection plumbing works as expected
        /// </summary>
        [Test]
        public void ConnectToServer()
        {
            WaitForIt();
        }

        /// <summary>
        /// Verifies that a second connection request blocks until the first connection request succeeds
        /// </summary>
        [Test]
        public void ConcurrentConnectsBlock()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            ServerDispatcher dispatcher = new ServerDispatcher(_factory.Object);
            bool secondConnectionOccurred = false;

            _factory.Setup(m => m.BeginConnect("www.cnn.com", 80, It.IsAny<Action<bool, INetworkConnection>>()))
                .Callback<string, int, Action<bool, INetworkConnection>>((a, b, c) => c(true, _connection.Object));

            _factory.Setup(m => m.BeginConnect("www.yahoo.com", 80, It.IsAny<Action<bool, INetworkConnection>>()))
                .Callback<string, int, Action<bool, INetworkConnection>>((a, b, c) =>
                                                                             {
                                                                                 secondConnectionOccurred = true;
                                                                                 c(true, _connection.Object);
                                                                             });

            dispatcher.ConnectToServer("www.cnn.com", 80, b =>
            {
                // Connected to remote host. Other connection should still be blocking.
                if (b && !secondConnectionOccurred)
                {
                    resetEvent.Set();
                }
            });

            dispatcher.ConnectToServer("www.yahoo.com", 80, b =>
                                                                {
                                                                    secondConnectionOccurred = true;
                                                                });

            Assert.That(resetEvent.WaitOne(2000), "Never attempted to connect to remote host");

        }

        private ServerDispatcher _dispatcher;

        public void WaitForIt()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            _dispatcher = new ServerDispatcher(_factory.Object);

            _factory.Setup(m => m.BeginConnect("www.cnn.com", 80, It.IsAny<Action<bool, INetworkConnection>>()))
                .Callback<string, int, Action<bool, INetworkConnection>>((a, b, c) => c(true, _connection.Object));

            _dispatcher.ConnectToServer("www.cnn.com", 80, b =>
            {
                if (b)
                {
                    resetEvent.Set();
                }
            });

            Assert.That(resetEvent.WaitOne(2000), "Never attempted to connect to remote host");
            
        }

        /// <summary>
        /// Verify data is sent to the active server
        /// </summary>
        [Test]
        public void SendDataToActiveServer()
        {
            var buffer = new byte[] {1, 2, 3};

            WaitForIt();

            Assert.That( _dispatcher.TrySendDataToActiveServer(buffer) );

            _connection.Verify(m=>m.SendData(buffer), Times.Once());
        }

        /// <summary>
        /// Verify data is only sent to the second server
        /// </summary>
        [Test]
        public void SendDataToActiveServerWithMultipleServers()
        {
            var buffer = new byte[] { 1, 2, 3 };

            ManualResetEvent resetEvent = new ManualResetEvent(false);

            WaitForIt();

            Mock<INetworkConnection> yahoo = new Mock<INetworkConnection>();

            _factory.Setup(m => m.BeginConnect("www.yahoo.com", 80, It.IsAny<Action<bool, INetworkConnection>>()))
                .Callback<string, int, Action<bool, INetworkConnection>>((a, b, c) => c(true, yahoo.Object));

            _dispatcher.ConnectToServer("www.yahoo.com", 80, b =>
            {
                if (b)
                {
                    resetEvent.Set();
                }
            });

            Assert.That(resetEvent.WaitOne(2000));

            Assert.That( _dispatcher.TrySendDataToActiveServer(buffer) );

            _connection.Verify(m => m.SendData(It.IsAny<byte[]>()), Times.Never());
            yahoo.Verify(m=>m.SendData(buffer), Times.Once());
        }

        /// <summary>
        /// Connect to server A, then server B, and then send a request to host A. A new connection should be established.
        /// </summary>
        [Test]
        public void SendingDataToOriginalServerWithMultipleConnections()
        {
            var buffer = new byte[] { 1, 2, 3 };

            ManualResetEvent resetEvent = new ManualResetEvent(false);

            int callbackCount = 0;
            Mock<INetworkConnection> fooServer = new Mock<INetworkConnection>();
            Mock<INetworkConnection> barServer = new Mock<INetworkConnection>();
            Mock<INetworkConnection> fooServer2 = new Mock<INetworkConnection>();

            ServerDispatcher dispatcher = new ServerDispatcher(_factory.Object);

            _factory.Setup(m => m.BeginConnect("www.foo.com", 80, It.IsAny<Action<bool, INetworkConnection>>()))
                .Callback<string, int, Action<bool, INetworkConnection>>((a, b, c) =>
                                                                             {
                                                                                 if (callbackCount == 0)
                                                                                     c(true, fooServer.Object);
                                                                                 else
                                                                                     c(true, fooServer2.Object);
                                                                             });
            _factory.Setup(m => m.BeginConnect("www.bar.com", 80, It.IsAny<Action<bool, INetworkConnection>>()))
                .Callback<string, int, Action<bool, INetworkConnection>>((a, b, c) => c(true, barServer.Object));

            dispatcher.ConnectToServer("www.foo.com", 80, b =>
            {
                if (b)
                {
                    callbackCount++;
                }
            });

            dispatcher.ConnectToServer("www.bar.com", 80, b =>
            {
                if (b)
                {
                    callbackCount++;
                }
            });

            dispatcher.ConnectToServer("www.foo.com", 80, b =>
            {
                if (b)
                {
                    callbackCount++;
                    resetEvent.Set();
                }
            });

            Assert.That(resetEvent.WaitOne(2000));
                
            Assert.That( dispatcher.TrySendDataToActiveServer(buffer) );

            fooServer.Verify(m => m.SendData(It.IsAny<byte[]>()), Times.Never());
            fooServer.Verify(m => m.Start(), Times.Once());

            barServer.Verify(m => m.SendData(It.IsAny<byte[]>()), Times.Never());
            barServer.Verify(m => m.Start(), Times.Once());
            
            fooServer2.Verify(m => m.SendData(buffer), Times.Once());
            fooServer2.Verify(m => m.Start(), Times.Once());
        }

        [Test]
        public void ConnectToActiveServer()
        {
            WaitForIt();

            _dispatcher.ConnectToServer("www.cnn.com", 80, Assert.That);

            _connection.Verify(m => m.Start(), Times.Once(), "The existing, active connection should not have been re-established.");
        }

        /// <summary>
        /// Verify data sent from the server is passed to the client.
        /// </summary>
        [Test]
        public void DataSentFromServerTest()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            var buffer = new byte[] { 1, 2, 3 };

            WaitForIt();

            _dispatcher.ServerDataAvailable += (sender, args) =>
                                                   {
                                                       Assert.That(args.Data, Is.EqualTo(buffer));
                                                       resetEvent.Set();
                                                   };

            _connection.Raise(m => m.DataAvailable += null, new DataAvailableEventArgs(buffer));
            Assert.That(resetEvent.WaitOne(2000));
        }

        /// <summary>
        /// Verify that the class under test unsubscribes from events
        /// </summary>
        [Test]
        public void ResetTest()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            var buffer = new byte[] { 1, 2, 3 };

            WaitForIt();

            _connection.Raise(m => m.ConnectionClosed += null, new EventArgs());

            _dispatcher.ServerDataAvailable += (sender, args) =>
            {
                Assert.That(args.Data, Is.EqualTo(buffer));
                resetEvent.Set();
            };

            _connection.Raise(m => m.DataAvailable += null, new DataAvailableEventArgs(buffer));
            Assert.That(resetEvent.WaitOne(2000), Is.False, "Since the socket closed before sending data, the data should be ignored.");
        }



        [Test]
        public void SendDataNoActiveServerTest()
        {
            var buffer = new byte[] { 1, 2, 3 };

            WaitForIt();

            _connection.Raise(m => m.ConnectionClosed += null, new EventArgs());

            Assert.That(_dispatcher.TrySendDataToActiveServer(buffer), Is.False);
        }
    }
}