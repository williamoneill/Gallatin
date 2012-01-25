using System;
using System.Text;
using System.Threading;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ServerSessionTests
    {
        [Test]
        public void ServerSocketStopsSendingData()
        {
            int callbackCount = 0;

            ManualResetEvent callbackInvokedEvent = new ManualResetEvent( false );

            Mock<INetworkFacade> mockFacade = new Mock<INetworkFacade>();

            mockFacade.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback<Action<bool, byte[], INetworkFacade>>( callback =>
                                                                 {
                                                                     if ( callbackCount++ == 0 )
                                                                     {
                                                                         callbackInvokedEvent.Set();
                                                                         callback( true, null, mockFacade.Object );
                                                                     }
                                                                 } );

            ServerSession session = new ServerSession();

            session.Start( mockFacade.Object );

            Assert.That( callbackInvokedEvent.WaitOne( 1000 ) );

            Assert.That( session.HasStoppedSendingData );
            Assert.That( !session.HasClosed );
            Assert.That( session.LastResponseHeader, Is.Null );
        }

        [Test]
        public void ServerSocketFailsToSend()
        {
            int callbackCount = 0;

            ManualResetEvent callbackInvokedEvent = new ManualResetEvent(false);

            Mock<INetworkFacade> mockFacade = new Mock<INetworkFacade>();

            mockFacade.Setup(m => m.BeginReceive(It.IsAny<Action<bool, byte[], INetworkFacade>>()))
                .Callback<Action<bool, byte[], INetworkFacade>>(callback =>
                {
                    if (callbackCount++ == 0)
                    {
                        callbackInvokedEvent.Set();
                        callback(false, null, mockFacade.Object);
                    }
                });

            ServerSession session = new ServerSession();

            session.Start(mockFacade.Object);

            Assert.That(callbackInvokedEvent.WaitOne(1000));

            Assert.That(session.LastResponseHeader, Is.Null);

            mockFacade.Verify(m=>m.BeginClose(It.IsAny<Action<bool,INetworkFacade>>()), Times.Once());
        }

        [Test]
        public void VerifyExplicitClose()
        {
            Mock<INetworkFacade> mockFacade = new Mock<INetworkFacade>();

            mockFacade.Setup( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ) )
                .Callback<Action<bool, INetworkFacade>>( callback => callback( true, mockFacade.Object ) );

            ServerSession session = new ServerSession();

            session.Start( mockFacade.Object );

            session.Close();

            Assert.That( session.HasClosed );

            mockFacade.Verify( m => m.BeginClose( It.IsAny<Action<bool, INetworkFacade>>() ), Times.Once() );
        }

        [Test]
        public void VerifyReceiveBehavior()
        {
            int callbackCount = 0;

            Mock<INetworkFacade> mockFacade = new Mock<INetworkFacade>();

            byte[] data = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nContent-length: 2\r\n\r\nhi" );

            mockFacade.Setup( m => m.BeginReceive( It.IsAny<Action<bool, byte[], INetworkFacade>>() ) )
                .Callback<Action<bool, byte[], INetworkFacade>>( callback =>
                                                                 {
                                                                     if ( callbackCount++ == 0 )
                                                                     {
                                                                         callback( true, data, mockFacade.Object );
                                                                     }
                                                                 } );

            ServerSession session = new ServerSession();

            int fullResponseCount = 0;
            int responseHeaderCount = 0;
            int partialDataCount = 0;

            ManualResetEvent doneParsing = new ManualResetEvent( false );

            session.FullResponseReadComplete += ( sender, args ) =>
                                                {
                                                    fullResponseCount++;
                                                    doneParsing.Set();
                                                };

            session.HttpResponseHeaderAvailable += ( sender, args ) =>
                                                   {
                                                       if ( args.StatusCode == 200 )
                                                       {
                                                           responseHeaderCount++;
                                                       }
                                                   };

            session.PartialDataAvailableForClient += ( sender, args ) =>
                                                     {
                                                         if ( args.Data[0] == 'h'
                                                              && args.Data[1] == 'i' )
                                                         {
                                                             partialDataCount++;
                                                         }
                                                     };

            session.Start( mockFacade.Object );

            Assert.That( doneParsing.WaitOne( 2000 ) );

            Assert.That( fullResponseCount, Is.EqualTo( 1 ) );
            Assert.That( responseHeaderCount, Is.EqualTo( 1 ) );
            Assert.That( partialDataCount, Is.EqualTo( 1 ) );

            Assert.That( session.LastResponseHeader, Is.Not.Null );
            Assert.That( session.LastResponseHeader.Status, Is.EqualTo( 200 ) );
        }

        [Test]
        public void VerifySocketClose()
        {
            Mock<INetworkFacade> mockFacade = new Mock<INetworkFacade>();

            ServerSession session = new ServerSession();

            session.Start( mockFacade.Object );

            mockFacade.Raise( m => m.ConnectionClosed += null, new EventArgs() );

            Assert.That( session.HasClosed );
        }

        [Test]
        public void VerifyStartBehavior()
        {
            Mock<INetworkFacade> mockFacade = new Mock<INetworkFacade>();

            ServerSession session = new ServerSession();

            session.Start( mockFacade.Object );

            Assert.That( !session.HasClosed );
            Assert.That( !session.HasStoppedSendingData );
            Assert.That( session.Connection, Is.SameAs( mockFacade.Object ) );
            Assert.That( session.LastResponseHeader, Is.Null );
        }
    }
}