using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
            TcpListener server = new TcpListener(4000);
            server.Start();

            NetworkFacade facade = new NetworkFacade(server.Server);

            byte[] buffer = new byte[]{0x04, 0x05, 0x06};
            facade.BeginSend( buffer, s => Assert.That(s, Is.True, "The 'success' flag was not set in the callback even though the data was sent") );

            byte[] bufferFromClient = new byte[100];
            TcpClient client = server.AcceptTcpClient();
            int dataRead = client.GetStream().Read( bufferFromClient, 0, bufferFromClient.Length );

            Assert.That(dataRead, Is.EqualTo(3));
            Assert.That(bufferFromClient[0], Is.EqualTo(0x04));
            Assert.That(bufferFromClient[1], Is.EqualTo(0x05));
            Assert.That(bufferFromClient[2], Is.EqualTo(0x06));

        }
    }
}
