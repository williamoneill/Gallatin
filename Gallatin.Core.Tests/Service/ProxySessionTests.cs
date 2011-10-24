﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ProxySessionTests
    {
        [Test]
        public void SimpleSendReceiveTest()
        {
            byte[] request = Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: www.yahoo.com\r\n\r\n" );
            byte[] response = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 2\r\n\r\nhi" );

            Mock<INetworkFacade> client = new Mock<INetworkFacade>();
            client.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, request, client.Object));

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();
            server.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback((Action<bool, byte[], INetworkFacade> callback) => callback(true, response, server.Object));

            Mock<INetworkFacadeFactory> mockFactory = new Mock<INetworkFacadeFactory>();
            mockFactory.Setup( m => m.BeginConnect( "www.yahoo.com", 80, It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<string, int, Action<bool, INetworkFacade>>( ( h, p, c ) => c( true, server.Object ) );

            ProxySession session = new ProxySession( client.Object, mockFactory.Object );
            session.Start();

            client.Verify(m=>m.BeginClose(It.IsAny<Action<bool,INetworkFacade>>()));
            server.Verify(m => m.BeginClose(It.IsAny<Action<bool, INetworkFacade>>()));
        }
    }
}
