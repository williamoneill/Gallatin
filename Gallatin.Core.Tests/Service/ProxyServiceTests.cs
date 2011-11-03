using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
            settings.SetupGet( m => m.ServerPort ).Returns( 5150 );
            settings.SetupGet(m => m.NetworkAddressBindingOrdinal).Returns(1);

            Mock<IProxySession> session = new Mock<IProxySession>();

            //TODO: CoreFactory.Register( () => session.Object );

            Mock<INetworkFacade> server = new Mock<INetworkFacade>();

            Mock<INetworkFacadeFactory> factory = new Mock<INetworkFacadeFactory>();
            factory.Setup( m => m.Listen( 1, 5150, It.IsAny<Action<INetworkFacade>>() ) )
                .Callback<int, int, Action<INetworkFacade>>( ( i, j, k ) => k( server.Object ) );

            ProxyService service = new ProxyService( settings.Object, factory.Object );
            service.Start();
            service.Stop();

            factory.Verify(m => m.Listen(1, 5150, It.IsAny<Action<INetworkFacade>>()), Times.Once());
            factory.Verify(m => m.EndListen(), Times.Once());
            session.Verify(m=>m.Start(It.IsAny<INetworkFacade>()), Times.Once());
        }

        [Test]
        public void StopBeforeStartTest()
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet(m => m.ServerPort).Returns(5150);
            settings.SetupGet(m => m.NetworkAddressBindingOrdinal).Returns(1);

            Mock<INetworkFacadeFactory> factory = new Mock<INetworkFacadeFactory>();

            ProxyService service = new ProxyService(settings.Object,factory.Object);

            Assert.Throws<InvalidOperationException>( service.Stop );
        }

        [Test]
        public void DoubleStartTest()
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet(m => m.ServerPort).Returns(5150);
            settings.SetupGet(m => m.NetworkAddressBindingOrdinal).Returns(1);

            Mock<INetworkFacadeFactory> factory = new Mock<INetworkFacadeFactory>();

            ProxyService service = new ProxyService(settings.Object, factory.Object);
            service.Start();

            Assert.Throws<InvalidOperationException>(service.Start);
            
        }
    }
}
