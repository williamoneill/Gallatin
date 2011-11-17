﻿using System;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ProxyServiceTests
    {
 
        [Test]
        public void BasicSendTest()
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet( m => m.MaxNumberClients ).Returns( 10 );
            settings.SetupGet( m => m.ServerPort ).Returns( 8080 );
            settings.SetupGet(m => m.ListenAddress).Returns("127.0.0.1");

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            Mock<INetworkFacadeFactory> factory = new Mock<INetworkFacadeFactory>();
            factory.Setup( m => m.Listen( "127.0.0.1", 8080, It.IsAny<Action<INetworkFacade>>() ) )
                .Callback<string, int, Action<INetworkFacade>>( ( i, j, k ) => k( server.Object ) );

            ProxyService service = new ProxyService( factory.Object, settings.Object );
            service.Start();
            service.Stop();

            factory.Verify( m => m.Listen( "127.0.0.1", 8080, It.IsAny<Action<INetworkFacade>>() ), Times.Once() );
            factory.Verify( m => m.EndListen(), Times.Once() );
        }

        [Test]
        public void DoubleStartTest()
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet( m => m.MaxNumberClients ).Returns( 10 );

            Mock<INetworkFacadeFactory> factory = new Mock<INetworkFacadeFactory>();

            ProxyService service = new ProxyService( factory.Object, settings.Object );
            service.Start();

            Assert.Throws<InvalidOperationException>( service.Start );
        }

        [Test]
        public void StopBeforeStartTest()
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupAllProperties();

            Mock<INetworkFacadeFactory> factory = new Mock<INetworkFacadeFactory>();

            ProxyService service = new ProxyService( factory.Object, settings.Object );

            Assert.Throws<InvalidOperationException>( service.Stop );
        }
    }
}