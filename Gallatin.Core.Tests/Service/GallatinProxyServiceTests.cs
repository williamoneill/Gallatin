using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Service;
using Moq;
using Moq.Language.Flow;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class GallatinProxyServiceTests
    {

        [Test]
        public void SimpleSendAndReceive()
        {
            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.SetupAllProperties();

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.SetupAllProperties();

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup( m => m.Listen( 0, 8080, It.IsAny<Action<INetworkFacade>>() ) )
                .Callback<int,int,Action<INetworkFacade>>( (s,p,d) => d( client.Object ) );
            mockFactory.Setup(
                m =>
                m.BeginConnect( "www.cnn.com",
                                80,
                                It.IsAny<Action<bool, INetworkFacade, ConnectionContext>>(),
                                It.IsAny<ConnectionContext>() ) )
                .Callback<string, int, Action<bool, INetworkFacade, ConnectionContext>, ConnectionContext>(
                    ( h, p, d, c ) => d( true, server.Object, c ) );

            Mock<ICoreSettings> mockSettings = new Mock<ICoreSettings>();
            mockSettings.SetupGet( m => m.MaxNumberClients ).Returns( 5 );

            byte[] dataFromClient = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.cnn.com\r\n\r\n" );
            byte[] responseFromServer = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");

            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, dataFromClient, client.Object));

            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, responseFromServer, server.Object));

            server.Setup( m => m.BeginSend( dataFromClient, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<byte[], Action<bool, INetworkFacade>>( ( b, d ) => d( true, server.Object ) );

            client.Setup(m => m.BeginSend(responseFromServer, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<byte[], Action<bool, INetworkFacade>>((b, d) => d(true, client.Object));

            GallatinProxyService serviceUnderTest = new GallatinProxyService(mockFactory.Object, mockSettings.Object);
            serviceUnderTest.Start();

            client.Verify(m=>m.BeginClose( It.IsAny<Action<bool,INetworkFacade>>() ));
            server.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()));
        }

        [Test]
        public void PersistentConnectionVerification()
        {
            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.SetupAllProperties();

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.SetupAllProperties();

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup(m => m.Listen(0, 8080, It.IsAny<Action<INetworkFacade>>()))
                .Callback<int, int, Action<INetworkFacade>>((s, p, d) => d(client.Object));
            mockFactory.Setup(
                m =>
                m.BeginConnect("www.cnn.com",
                                80,
                                It.IsAny<Action<bool, INetworkFacade, ConnectionContext>>(),
                                It.IsAny<ConnectionContext>()))
                .Callback<string, int, Action<bool, INetworkFacade, ConnectionContext>, ConnectionContext>(
                    (h, p, d, c) => d(true, server.Object, c));

            Mock<ICoreSettings> mockSettings = new Mock<ICoreSettings>();
            mockSettings.SetupGet(m => m.MaxNumberClients).Returns(5);

            int clientMessageCtr = 0;
            List<byte[]> clientMessages = new List<byte[]>();
            clientMessages.Add(Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\nHost: www.cnn.com\r\n\r\n"));
            clientMessages.Add(Encoding.UTF8.GetBytes("GET /foo.jpg HTTP/1.1\r\nHost: www.cnn.com\r\n\r\n"));

            int serverMessageCtr = 0;
            List<byte[]> serverMessages = new List<byte[]>();
            serverMessages.Add(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4\r\nConnection: keep-alive\r\n\r\ntest"));
            serverMessages.Add(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello"));

            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, clientMessages[clientMessageCtr++], client.Object));

            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, serverMessages[serverMessageCtr++], server.Object));

            server.Setup(m => m.BeginSend(clientMessages[0], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<byte[], Action<bool, INetworkFacade>>((b, d) => d(true, server.Object));

            server.Setup(m => m.BeginSend(clientMessages[1], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<byte[], Action<bool, INetworkFacade>>((b, d) => d(true, server.Object));

            client.Setup(m => m.BeginSend(serverMessages[0], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<byte[], Action<bool, INetworkFacade>>((b, d) => d(true, client.Object));

            client.Setup(m => m.BeginSend(serverMessages[1], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback<byte[], Action<bool, INetworkFacade>>((b, d) => d(true, client.Object));

            GallatinProxyService serviceUnderTest = new GallatinProxyService(mockFactory.Object, mockSettings.Object);
            serviceUnderTest.Start();

            client.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            server.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()), Times.Once());
            
        }
    }
}
