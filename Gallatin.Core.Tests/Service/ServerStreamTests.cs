using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ServerStreamTests
    {
        [Test]
        public void StreamTest()
        {
            int bufferCtr = 0;

            List<byte[]> messages = new List<byte[]>();
            messages.Add(new byte[] { 0x01, 0x01 });
            messages.Add(new byte[] { 0x02, 0x02 });
            messages.Add(new byte[] { 0x03, 0x03 });

            byte[] initialData = new byte[] { 0x06, 0x06};

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, messages[bufferCtr++], server.Object));

            client.Setup(m => m.BeginSend(initialData, It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, client.Object));

            client.Setup(m => m.BeginSend(messages[0], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, client.Object));

            client.Setup(m => m.BeginSend(messages[1], It.IsAny<Action<bool, INetworkFacade>>()))
                .Callback((byte[] d1, Action<bool, INetworkFacade> callback) => callback(true, client.Object));

            ServerStream stream = new ServerStream(client.Object,server.Object);   
            stream.StartStreaming( initialData );

            client.Verify(m => m.BeginSend(initialData, It.IsAny<Action<bool, INetworkFacade>>()));

            client.Verify( m => m.BeginSend( messages[0], It.IsAny<Action<bool, INetworkFacade>>() ) );
            client.Verify(m => m.BeginSend(messages[1], It.IsAny<Action<bool, INetworkFacade>>()));
            client.Verify(m => m.BeginSend(messages[2], It.IsAny<Action<bool, INetworkFacade>>()));
        }
    }
}
