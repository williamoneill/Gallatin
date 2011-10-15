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
                new IPEndPoint( dnsEntry.AddressList[1], 8082 );

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
                                  8082,
                                  ( b, s, state ) =>
                                  {
                                      Assert.That( b, Is.True );
                                      Assert.That( s, Is.Not.Null );
                                      Assert.That( state, Is.EqualTo("foo") );
                                  }, 
                                  "foo" );

            Assert.That( trigger.WaitOne( 2000 ), Is.True );
            Assert.That( isConnected, Is.True );
        }

        [Test]
        [ExpectedException]
        public void SanityVerifyInterFaceContract()
        {
            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.Listen(-1,-1, null);
        }

        [Test]
        public void DuplicateListenFails()
        {
            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.Listen( 1, 20201, s => s.ToString());
            Assert.Throws<InvalidOperationException>( () => factory.Listen( 1, 20202, s => s.ToString() ) );
        }

        [Test]
        public void ListenTest()
        {
            ManualResetEvent trigger = new ManualResetEvent( false );
            bool isConnected = false;

            NetworkFacadeFactory factory = new NetworkFacadeFactory();
            factory.Listen( 1,
                            8081,
                            s =>
                            {
                                Assert.That( s, Is.Not.Null );
                                trigger.Set();
                                isConnected = true;
                            } );

            TcpClient client = new TcpClient( "localhost", 8081 );

            Assert.That( trigger.WaitOne( 2000 ), Is.True );
            Assert.That( isConnected, Is.True, "The callback delegate was not invoked when a client connected" );
        }
    }
}