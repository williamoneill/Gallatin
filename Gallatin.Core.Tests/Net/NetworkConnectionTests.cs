using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Gallatin.Core.Net;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Net
{
    [TestFixture]
    public class NetworkConnectionTests
    {
        private Socket _server;
        private Socket _client;
        private Socket _serverClient;
        private byte[] _buffer;
        private int _port = 8080;


        private void EndAccept(IAsyncResult ar)
        {
            _serverClient = _server.EndAccept( ar );
            _serverClient.NoDelay = true;
        }

        [SetUp]
        public void Setup()
        {
            _buffer = new byte[]{1,2,3};

            IPHostEntry dnsEntry = Dns.GetHostEntry( "127.0.0.1" );
            IPEndPoint endPoint =
                new IPEndPoint( dnsEntry.AddressList[0], ++_port );


            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _server.Bind(endPoint);
            _server.Listen(50);
            _server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, false);
            _server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, false);
            _server.NoDelay = true;
            _server.BeginAccept(EndAccept, null);

            Thread.Sleep(100);

            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, false);
            _client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, false);
            _client.NoDelay = true;
            _client.Connect("127.0.0.1", _port);

            Thread.Sleep(100);
        }

        [TearDown]
        public void TearDown()
        {
            _server.Close();
            _client.Close();
        }

        [Test]
        public void StartupTest()
        {
            NetworkConnection connection = new NetworkConnection(_server);
        }

        [Test]
        public void SendData()
        {
            int eventCount = 0;

            ManualResetEvent resetEvent = new ManualResetEvent(false);

            NetworkConnection connection = new NetworkConnection(_client);

            connection.DataSent += ( sender, args ) =>
                                   {
                                       eventCount++;
                                       resetEvent.Set();
                                   };

            byte[] buffer = new byte[10];
            _serverClient.BeginReceive( buffer,
                                        0,
                                        10,
                                        SocketFlags.None,
                                        ar =>_serverClient.EndReceive( ar ),
                                        null );

            connection.SendData(_buffer);

            Assert.That(resetEvent.WaitOne(2000));

            Assert.That(buffer[0], Is.EqualTo(1), "Data not sent to server");
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void ReceiveData()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            NetworkConnection connection = new NetworkConnection(_client);

            byte[] buffer = null;
            connection.DataAvailable += ( sender, args ) =>
                                        {
                                            buffer = args.Data;
                                            resetEvent.Set();
                                        };
            
            // Send data from other end. Place this before the Start method to verify what might happen
            // on an actual network. Data may arrive before we start to receive it.
            _serverClient.Send( _buffer );

            connection.Start();

            Assert.That(resetEvent.WaitOne(2000));
            Assert.That(buffer[0], Is.EqualTo(1));
        }

        [Test]
        public void RapidReceiveData()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            NetworkConnection connection = new NetworkConnection(_client);

            byte[] originalBuffer = new byte[10000];
            List<byte> copiedBuffer = new List<byte>();

            for (int i = 0; i < 10000; i++)
            {
                originalBuffer[i] = (byte) ( i % 100 );
            }

            connection.DataAvailable += (sender, args) =>
                                        {
                                            var receiveBuffer = args.Data;

                                            copiedBuffer.AddRange(receiveBuffer);

                                            if (copiedBuffer.Count() == 10000)
                                            {
                                                resetEvent.Set();
                                            }
                                        };

            connection.Start();

            for (int i = 0; i < 1000; i++)
            {
                byte[] buffer = new byte[10];
                
                Array.Copy( originalBuffer, i * 10, buffer, 0, 10  );

                _serverClient.Send(buffer);
            }

            Assert.That(resetEvent.WaitOne(2000));
            Assert.That(originalBuffer, Is.EqualTo(copiedBuffer)  );
        }

        [Test]
        public void ReceiveDoesNotWorkIfNotStarted()
        {
            ManualResetEvent resetEvent = new ManualResetEvent(false);

            NetworkConnection connection = new NetworkConnection(_client);

            byte[] buffer = null;
            connection.DataAvailable += (sender, args) =>
            {
                buffer = args.Data;
                resetEvent.Set();
            };

            // Send data from other end. Object under test should not receive data since it was not started.
            _serverClient.Send(_buffer);

            Assert.That(resetEvent.WaitOne(1000), Is.False);
        }

        [Test]
        public void StopSendingTest()
        {
            int shutdownCount = 0;

            ManualResetEvent resetEvent = new ManualResetEvent(false);

            NetworkConnection connection = new NetworkConnection(_client);

            connection.Shutdown += ( sender, args ) =>
                                          {
                                              shutdownCount++;
                                              resetEvent.Set();
                                          };

            connection.Start();

            _serverClient.Shutdown(SocketShutdown.Send);

            Assert.That(resetEvent.WaitOne(2000));
            Assert.That(shutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void StopReceivingTest()
        {
            int shutdownCount = 0;

            ManualResetEvent resetEvent = new ManualResetEvent(false);

            NetworkConnection connection = new NetworkConnection(_client);

            connection.Shutdown += (sender, args) =>
            {
                shutdownCount++;
                resetEvent.Set();
            };

            connection.Start();

            // Server stops receiving data
            _serverClient.Shutdown(SocketShutdown.Receive);

            connection.SendData(_buffer);

            Assert.That(resetEvent.WaitOne(2000));
            Assert.That(shutdownCount, Is.EqualTo(1));
        }

        [Test]
        public void RemoteHostCloseTest()
        {
            int shutdownCount = 0;
            int closeCount = 0;

            ManualResetEvent resetEvent = new ManualResetEvent(false);

            NetworkConnection connection = new NetworkConnection(_client);

            connection.Shutdown += (sender, args) =>
            {
                shutdownCount++;
                resetEvent.Set();
            };

            connection.ConnectionClosed += ( sender, args ) =>
                                           {
                                               closeCount++;
                                               resetEvent.Set();
                                           };

            connection.Start();

            _serverClient.Close();

            Assert.That(resetEvent.WaitOne(2000));
            Assert.That(shutdownCount, Is.EqualTo(1));
            Assert.That(closeCount, Is.EqualTo(0));
        }

        [Test]
        public void CloseTest()
        {
            int shutdownCount = 0;
            int closeCount = 0;

            ManualResetEvent resetEvent = new ManualResetEvent(false);

            NetworkConnection connection = new NetworkConnection(_client);

            connection.Shutdown += (sender, args) =>
            {
                shutdownCount++;
                resetEvent.Set();
            };

            connection.ConnectionClosed += (sender, args) =>
            {
                closeCount++;
                resetEvent.Set();
            };

            connection.Start();
            connection.Close();

            Assert.That(resetEvent.WaitOne(2000));
            Assert.That(shutdownCount, Is.EqualTo(0));
            Assert.That(closeCount, Is.EqualTo(1));
        }

        [Test]
        public void SocketClosedBeforeSendTest()
        {
            NetworkConnection connection = new NetworkConnection(_client);

            _serverClient.Close();
            _client.Close();

            Assert.Throws<SocketException>(() => connection.SendData(_buffer));
        }
    }
}
