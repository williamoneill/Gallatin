using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Gallatin.Core.Service;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class NetworkFacadeFactoryTests
    {
        [Test]
        public void ConnectTest()
        {
            ManualResetEvent trigger = new ManualResetEvent( false );
            bool isConnected = false;

            Socket socket = new Socket( AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp );

            IPHostEntry dnsEntry = Dns.GetHostEntry( "localhost" );

            // If this fails, consider changing the index. This could fail depending on the
            // physical configuration of the host system.
            IPEndPoint endPoint =
                new IPEndPoint( dnsEntry.AddressList[1], 8089 );

            socket.Bind( endPoint );

            socket.Listen( 30 );

            socket.BeginAccept( s =>
                                {
                                    socket.EndAccept( s );
                                    isConnected = true;
                                    trigger.Set();
                                },
                                null );

            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.BeginConnect( "localhost",
                                  8089,
                                  ( b, s ) =>
                                  {
                                      Assert.That( b, Is.True );
                                      Assert.That( s, Is.Not.Null );
                                  } );

            Assert.That( trigger.WaitOne( 2000 ), Is.True );
            Assert.That( isConnected, Is.True );
        }

        [Test]
        public void DuplicateListenFails()
        {
            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.Listen( "127.0.0.1", 20201, s => s.ToString() );
            Assert.Throws<InvalidOperationException>( () => factory.Listen( "127.0.0.1", 20202, s => s.ToString() ) );
        }

        [Test]
        public void EndListenTest()
        {
            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.Listen("127.0.0.1", 4587, s => Assert.Pass());

            Assert.Throws<InvalidOperationException>(() => factory.Listen("127.0.0.1", 4588, s => Assert.Fail()));

            factory.EndListen();

            factory.Listen("127.0.0.1", 4588, s => Assert.Pass());
            factory.EndListen();
        }

        [Test]
        public void FailedServerConnect()
        {
            // Connect to server that does not exist. Verify error.
            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.BeginConnect( "localhost",
                                  5150,
                                  ( b, s ) =>
                                  {
                                      Assert.That( b, Is.False );
                                  } );
        }

        [Test]
        public void ListenTest()
        {
            ManualResetEvent trigger = new ManualResetEvent( false );
            bool isConnected = false;

            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.Listen("127.0.0.1",
                            8081,
                            s =>
                            {
                                Assert.That( s, Is.Not.Null );
                                isConnected = true;
                                trigger.Set();
                            } );

            TcpClient client = new TcpClient( "localhost", 8081 );

            Assert.That( trigger.WaitOne( 10000 ), Is.True );
            Assert.That( isConnected, Is.True, "The callback delegate was not invoked when a client connected" );
        }

        [Test]
        [ExpectedException]
        public void SanityVerifyInterFaceContract()
        {
            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.Listen(null, -1, null);
        }

        [Test]
        public void InvalidAddressTest()
        {
            NetworkFacadeFactory factory = new NetworkFacadeFactory();

            Assert.Throws<ArgumentException>(() => factory.Listen("localhost", 45983, s => Assert.Fail()));
        }
    }
}