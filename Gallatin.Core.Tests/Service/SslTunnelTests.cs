using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class SslTunnelTests
    {
        [Test]
        public void TransferTest()
        {
            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            byte[] firstDataFromClient = new byte[] { 0x01, 0x02 };

            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback( (Action<bool, byte[], INetworkFacade> callback) => callback(true, firstDataFromClient, client.Object));

            SslTunnel tunnel = new SslTunnel(client.Object, server.Object, "1.1");
            tunnel.EstablishTunnel();

            var data = Encoding.UTF8.GetBytes(
                "HTTP/1.1 200 Connection established\r\n" +
                "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n" );

            client.Verify( m => m.BeginSend( data, It.IsAny<Action<bool,INetworkFacade>>() ) );

            server.Verify(m => m.BeginSend(firstDataFromClient, It.IsAny<Action<bool, INetworkFacade>>()));
        }

        [Test]
        public void MultipleClientTransferTest()
        {
            int clientBufferCtr = 0;

            List<byte[]> clientMessages = new List<byte[]>();
            clientMessages.Add( new byte[] { 0x01, 0x01} );
            clientMessages.Add(new byte[] { 0x02, 0x02 });
            clientMessages.Add(new byte[] { 0x03, 0x03 });

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, clientMessages[clientBufferCtr++], client.Object));

            server.Setup( m => m.BeginSend( clientMessages[0], It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback( ( byte[] d1, Action<bool, INetworkFacade> callback ) => callback( true, server.Object ) );
            
            server.Setup(m => m.BeginSend(clientMessages[1], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, server.Object));
            
            // Send "false" back to the client to indicate that the server closed the connection
            server.Setup(m => m.BeginSend(clientMessages[2], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(false, server.Object));

            SslTunnel tunnel = new SslTunnel(client.Object, server.Object, "1.1");
            tunnel.EstablishTunnel();

            var data = Encoding.UTF8.GetBytes(
                "HTTP/1.1 200 Connection established\r\n" +
                "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n");

            client.Verify(m => m.BeginSend(data, It.IsAny<Action<bool, INetworkFacade>>()));

            server.Verify(m => m.BeginSend(clientMessages[0], It.IsAny<Action<bool, INetworkFacade>>()));
            server.Verify(m => m.BeginSend(clientMessages[1], It.IsAny<Action<bool, INetworkFacade>>()));
            server.Verify(m => m.BeginSend(clientMessages[2], It.IsAny<Action<bool, INetworkFacade>>()));
        }

        [Test]
        public void MultipleTransferTest()
        {
            int clientBufferCtr = 0;
            int serverBufferCtr = 0;

            var data = Encoding.UTF8.GetBytes(
                "HTTP/1.1 200 Connection established\r\n" +
                "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n");

            List<byte[]> clientMessages = new List<byte[]>();
            clientMessages.Add(new byte[] { 0x00, 0x00 });
            clientMessages.Add(new byte[] { 0x01, 0x01 });
            clientMessages.Add(new byte[] { 0x02, 0x02 });
            clientMessages.Add(new byte[] { 0x03, 0x03 });

            List<byte[]> serverMessages = new List<byte[]>();
            serverMessages.Add(new byte[] { 0x04, 0x04 });
            serverMessages.Add(new byte[] { 0x05, 0x05 });
            serverMessages.Add(new byte[] { 0x06, 0x06 });
            serverMessages.Add(new byte[] { 0x07, 0x07 });

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            // Mock data received from the client and server. This will get called multiple times and increment through the buffers.
            // Since the begin receive methods have no state (BeginEnd have received buffer) we cannot tie an invocation
            // in a series in the sequence.
            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, clientMessages[clientBufferCtr++], client.Object));
            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, serverMessages[serverBufferCtr++], server.Object));

            // Mock the data sends as data is routed between the two endpoints
            client.Setup(m => m.BeginSend(data, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, client.Object));     // Note: ack proxy header send

            client.Setup(m => m.BeginSend(serverMessages[0], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, client.Object));

            client.Setup(m => m.BeginSend(serverMessages[1], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, client.Object));

            client.Setup(m => m.BeginSend(serverMessages[2], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(false, client.Object));    //Note: false - termination condition

            server.Setup(m => m.BeginSend(clientMessages[0], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, server.Object));

            server.Setup(m => m.BeginSend(clientMessages[1], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, server.Object));

            server.Setup(m => m.BeginSend(clientMessages[2], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, server.Object));

            // Run the class under test
            SslTunnel tunnel = new SslTunnel(client.Object, server.Object, "1.1");
            tunnel.EstablishTunnel();

            server.Verify(m => m.BeginSend(clientMessages[0], It.IsAny<Action<bool, INetworkFacade>>()));
            server.Verify(m => m.BeginSend(clientMessages[1], It.IsAny<Action<bool, INetworkFacade>>()));
            server.Verify(m => m.BeginSend(clientMessages[2], It.IsAny<Action<bool, INetworkFacade>>()));

            client.Verify(m => m.BeginSend(data, It.IsAny<Action<bool, INetworkFacade>>()));
            client.Verify(m => m.BeginSend(serverMessages[0], It.IsAny<Action<bool, INetworkFacade>>()));
            client.Verify(m => m.BeginSend(serverMessages[1], It.IsAny<Action<bool, INetworkFacade>>()));
            client.Verify(m => m.BeginSend(serverMessages[2], It.IsAny<Action<bool, INetworkFacade>>()));
        }

    }

}
