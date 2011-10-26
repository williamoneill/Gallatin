using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Gallatin.Core.Service;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class NetworkFacadeTests
    {
        [Test]
        public void BeginSendTest()
        {
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            NetworkFacade facadeUnderTest = null;

            // Set up the "server"
            TcpListener server = new TcpListener(4000);
            server.Start();

            // When the client connects, set up the object under test
            server.BeginAcceptTcpClient(
                ar =>
                {
                    var s = ar.AsyncState as TcpListener;
                    TcpClient serversClientSocket = s.EndAcceptTcpClient( ar );
                    facadeUnderTest = new NetworkFacade(serversClientSocket.Client);
                    resetEvent.Set();
                }
                , server);

            //
            // Setup complete...
            //

            // Connect to the above "server"
            TcpClient client = new TcpClient("localhost", 4000);

            // Wait for client connect
            Assert.That(resetEvent.WaitOne(30000), Is.True);

            // Server socket sends to client socket once the client connects
            byte[] buffer = new byte[]{0x04, 0x05, 0x06};
            facadeUnderTest.BeginSend( buffer, (s,f) =>
                                      {
                                          Assert.That(f, Is.SameAs(facadeUnderTest));
                                          Assert.That( s,
                                                       Is.True,
                                                       "The 'success' flag was not set in the callback even though the data was sent" );
                                          resetEvent.Set();
                                      } );

            // Wait for the class under test to send data
            Assert.That(resetEvent.WaitOne(5000), Is.True);
            
            byte[] bufferFromClient = new byte[100];
            int dataRead = client.GetStream().Read( bufferFromClient, 0, bufferFromClient.Length );

            Assert.That(dataRead, Is.EqualTo(3));
            Assert.That(bufferFromClient[0], Is.EqualTo(0x04));
            Assert.That(bufferFromClient[1], Is.EqualTo(0x05));
            Assert.That(bufferFromClient[2], Is.EqualTo(0x06));

        }

        [Test]
        public void BeginReceiveTest()
        {
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            NetworkFacade facadeUnderTest = null;

            // Set up the "server"
            TcpListener server = new TcpListener(4001);
            server.Start();

            // When the client connects, set up the object under test
            server.BeginAcceptTcpClient(
                ar =>
                {
                    var s = ar.AsyncState as TcpListener;
                    TcpClient serversClientSocket = s.EndAcceptTcpClient(ar);
                    facadeUnderTest = new NetworkFacade(serversClientSocket.Client);
                    resetEvent.Set();
                }
                , server);

            //
            // Setup complete...
            //

            // Connect to the above "server"
            TcpClient client = new TcpClient("localhost", 4001);

            // Wait for client connect
            Assert.That(resetEvent.WaitOne(30000), Is.True);

            // Wait to receive data from client. Once received, release the reset event
            byte[] buffer = new byte[] { 0x04, 0x05, 0x06 };
            facadeUnderTest.BeginReceive( (ok, data, facade) =>
            {
                Assert.That(facade, Is.SameAs(facadeUnderTest));
                Assert.That(ok, Is.True,
                             "The 'success' flag was not set in the callback even though the data was sent");
                Assert.That( data[0], Is.EqualTo(0x04) );
                Assert.That(data.Length, Is.EqualTo(3));
                resetEvent.Set();
            });

            client.GetStream().Write(buffer, 0, buffer.Length);


            // Wait for the class under test to receive data
            Assert.That(resetEvent.WaitOne(5000), Is.True);
        }

        [Test]
        public void BeginCloseTest()
        {
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            NetworkFacade facadeUnderTest = null;

            // Set up the "server"
            TcpListener server = new TcpListener(4002);
            server.Start();

            // When the client connects, set up the object under test
            server.BeginAcceptTcpClient(
                ar =>
                {
                    var s = ar.AsyncState as TcpListener;
                    TcpClient serversClientSocket = s.EndAcceptTcpClient(ar);
                    facadeUnderTest = new NetworkFacade(serversClientSocket.Client);
                    resetEvent.Set();
                }
                , server);

            //
            // Setup complete...
            //

            // Connect to the above "server"
            TcpClient client = new TcpClient("localhost", 4002);

            // Wait for client connect
            Assert.That(resetEvent.WaitOne(30000), Is.True);
            Assert.That(client.Connected, Is.True);

            // Close connection
            facadeUnderTest.BeginClose( (b,f) =>
                                        {
                                            Assert.That(f, Is.SameAs(facadeUnderTest));
                                            Assert.That(b, Is.True);
                                            resetEvent.Set();
                                        } );

            // Wait for close
            Assert.That(resetEvent.WaitOne(30000), Is.True);

            Thread.Sleep(1000);

            var stream = client.GetStream();
            stream.Write( new byte[]{0x04, 0x05}, 0, 2 );

            byte[] buffer = new byte[300];
            Assert.Throws<IOException>( () => stream.Read( buffer, 0, buffer.Length ) );
        }

    }
}
