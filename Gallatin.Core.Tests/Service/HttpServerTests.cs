﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Gallatin.Contracts;
using Gallatin.Core.Filters;
using Gallatin.Core.Net;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class HttpServerTests
    {
        #region Setup/Teardown

        [SetUp]
        public void Setup()
        {
            _connection = new Mock<INetworkConnection>();
            _filter = new Mock<IHttpResponseFilter>();
        }

        #endregion

        private Mock<INetworkConnection> _connection;
        private Mock<IHttpResponseFilter> _filter;

        [Test]
        public void BodyFilterTest()
        {
            HttpServer server = new HttpServer( _connection.Object, _filter.Object );

            byte[] serverResponse = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 20\r\n\r\n" );
            byte[] serverResponseBody = Encoding.UTF8.GetBytes( "01234567890123456789" );
            byte[] filterResponse = new byte[]
                                    {
                                        1, 2, 3
                                    };

            // When callbacks are returned when evaluating the header then the body should be capturned
            // and filtered.
            IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> callbacks = new List<Func<IHttpResponse, string, byte[], byte[]>>();
            
            _filter.Setup(m => m.ApplyResponseHeaderFilters(It.IsAny<IHttpResponse>(), out callbacks)).
                Returns( null as byte[] );

            // This returns a new body. The header and body should be returned to the client.
            _filter.Setup(
                m =>
                m.ApplyResponseBodyFilter( It.IsAny<IHttpResponse>(), serverResponseBody, callbacks ) ).
                Returns( filterResponse );

            int invocationCount = 0;

            ManualResetEvent dataReceived = new ManualResetEvent( false );

            server.DataAvailable += ( sender, args ) =>
                                        {
                                        Assert.That(args.Data, Is.EqualTo(filterResponse ));
                                        invocationCount++;
                                        dataReceived.Set();
                                    };

            _connection.Raise( m => m.DataAvailable += null, new DataAvailableEventArgs( serverResponse ) );
            _connection.Raise( m => m.DataAvailable += null, new DataAvailableEventArgs( serverResponseBody ) );

            Assert.That( dataReceived.WaitOne( 3000 ) );
            Assert.That( invocationCount, Is.EqualTo( 1 ) );

            _connection.Verify( m => m.Close(), Times.Once() );
        }

        [Test]
        public void CloseTest()
        {
            HttpServer server = new HttpServer( _connection.Object, _filter.Object );

            server.Close();

            _connection.Verify( m => m.Close(), Times.Once() );
        }

        [Test]
        public void MultipleSendsFromServerTest()
        {
            HttpServer server = new HttpServer( _connection.Object, _filter.Object );

            byte[] serverResponse = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 20\r\n\r\n" );
            byte[] serverResponseBody = Encoding.UTF8.GetBytes( "01234567890123456789" );

            IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> callbacks;
            _filter.Setup(m => m.ApplyResponseHeaderFilters(It.IsAny<IHttpResponse>(), out callbacks)).
                Returns( null as byte[] );

            int invocationCount = 0;

            server.DataAvailable += ( sender, args ) =>
                                    {
                                        if ( invocationCount == 0 )
                                        {
                                            Assert.That( args.Data, Is.EqualTo( serverResponse ) );
                                        }
                                        else
                                        {
                                            Assert.That( args.Data, Is.EqualTo( serverResponseBody ) );
                                        }

                                        invocationCount++;
                                    };

            _connection.Raise( m => m.DataAvailable += null, new DataAvailableEventArgs( serverResponse ) );
            _connection.Raise( m => m.DataAvailable += null, new DataAvailableEventArgs( serverResponseBody ) );

            Assert.That( invocationCount, Is.EqualTo( 2 ) );

            _connection.Verify( m => m.Close(), Times.Never() );
        }

        [Test]
        public void ResponseFilterTest()
        {
            int callbackInvocationCount = 0;

            HttpServer server = new HttpServer( _connection.Object, _filter.Object );

            byte[] filterResponse = new byte[]
                                    {
                                        1, 2, 3
                                    };
            byte[] serverResponse = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 0\r\n\r\n" );

            IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> callbacks = null;
            _filter.Setup(m => m.ApplyResponseHeaderFilters(It.IsAny<IHttpResponse>(), out callbacks)).
                Returns( filterResponse );

            server.DataAvailable += ( sender, args ) =>
                                    {
                                        Assert.That( args.Data, Is.EqualTo( filterResponse ) );
                                        callbackInvocationCount++;
                                    };

            _connection.Raise( m => m.DataAvailable += null, new DataAvailableEventArgs( serverResponse ) );

            Assert.That( callbackInvocationCount, Is.EqualTo( 1 ) );

            _connection.Verify( m => m.Close(), Times.Once() );
        }

        [Test]
        public void SendTest()
        {
            HttpServer server = new HttpServer( _connection.Object, _filter.Object );

            byte[] data = new byte[]
                          {
                              1, 2, 3
                          };

            server.Send( data );

            _connection.Verify( m => m.SendData( data ), Times.Once() );
        }

        [Test]
        public void SocketClosed()
        {
            int timesClosed = 0;

            HttpServer server = new HttpServer( _connection.Object, _filter.Object );

            server.SessionClosed += ( sender, args ) => timesClosed++;

            _connection.Raise( m => m.ConnectionClosed += null, new EventArgs() );

            Assert.That( timesClosed, Is.EqualTo( 1 ) );
        }
    }
}