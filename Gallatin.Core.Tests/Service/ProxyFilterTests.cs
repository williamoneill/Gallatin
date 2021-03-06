﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Service;
using Moq;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Service
{
    [TestFixture]
    public class ProxyFilterTests
    {
        private static byte[] Compress( byte[] data )
        {
            MemoryStream newBody = new MemoryStream();
            using ( GZipStream gZipStream = new GZipStream( newBody, CompressionMode.Compress ) )
            {
                MemoryStream uncompressed = new MemoryStream( data );
                uncompressed.CopyTo( gZipStream );
            }

            return newBody.ToArray();
        }

        [Test]
        public void VerifyResponseFilterWithCallbackAndNoBody([Values(true, false)] bool filterEnabled)
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet(m => m.FilteringEnabled).Returns(filterEnabled);

            ProxyFilter filter = new ProxyFilter(settings.Object);
            List<IResponseFilter> filters = new List<IResponseFilter>();

            Mock<IHttpResponse> response = new Mock<IHttpResponse>();
            response.SetupAllProperties();
            response.SetupGet( s => s.HasBody ).Returns( false );

            Mock<IResponseFilter> mockFilter = new Mock<IResponseFilter>();
            mockFilter.SetupGet( s => s.FilterSpeedType ).Returns( FilterSpeedType.Remote );
            Func<IHttpResponse, string, byte[], byte[]> outParm =
                ( r, c, i ) =>
                {
                    Assert.Fail();
                    return null;
                };
            mockFilter.Setup( s => s.EvaluateFilter( response.Object, "connectionid", out outParm ) ).Returns( null as string );

            filters.Add( mockFilter.Object );
            filter.ResponseFilters = filters;

            bool isBodyNeeded;
            byte[] filterResponse;
            filterResponse = filter.EvaluateResponseFilters( response.Object, "connectionid", out isBodyNeeded );

            Assert.That( isBodyNeeded,
                         Is.False,
                         "Since the HTTP response did not have a body, a body should not be requested even though the body required delegate was set" );
            Assert.That( filterResponse, Is.Null );
        }

        [Test]
        public void VerifyResponseFilterWithCallbackWithBody([Values(true, false)] bool filterEnabled)
        {
            bool delegateHasBeenCalled = false;

            byte[] body = Encoding.UTF8.GetBytes( "this is the body" );
            byte[] compressedBody = Compress( body );
            byte[] outBody = Encoding.UTF8.GetBytes( "this is the modified body" );

            Mock<IHttpHeaders> mockHeaders = new Mock<IHttpHeaders>();
            mockHeaders.Setup( s => s["transfer-encoding"] ).Returns( "chunked" );
            mockHeaders.Setup( s => s["content-encoding"] ).Returns( "gzip" );

            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet(m => m.FilteringEnabled).Returns(filterEnabled);

            ProxyFilter filter = new ProxyFilter(settings.Object);
            List<IResponseFilter> filters = new List<IResponseFilter>();

            Mock<IHttpResponse> response = new Mock<IHttpResponse>();
            response.SetupAllProperties();
            response.SetupGet( s => s.HasBody ).Returns( true );
            response.SetupGet( s => s.Headers ).Returns( mockHeaders.Object );

            Mock<IResponseFilter> mockFilter = new Mock<IResponseFilter>();
            mockFilter.SetupGet( s => s.FilterSpeedType ).Returns( FilterSpeedType.Remote );
            Func<IHttpResponse, string, byte[], byte[]> outParm =
                ( r, c, i ) =>
                {
                    delegateHasBeenCalled = true;
                    Assert.That( r, Is.SameAs( response.Object ) );
                    Assert.That( c, Is.EqualTo( "connectionid" ) );
                    Assert.That( i, Is.EqualTo( body ), "This should be the uncompressed body" );
                    return outBody;
                };
            mockFilter.Setup( s => s.EvaluateFilter( response.Object, "connectionid", out outParm ) ).Returns( null as string );

            filters.Add( mockFilter.Object );
            filter.ResponseFilters = filters;

            bool isBodyNeeded;
            byte[] filterResponse = filter.EvaluateResponseFilters( response.Object, "connectionid", out isBodyNeeded );

            if (filterEnabled)
            {
                Assert.That(isBodyNeeded, Is.True);
                Assert.That(filterResponse, Is.Null);

                byte[] responseMessage = filter.EvaluateResponseFiltersWithBody(response.Object, "connectionid", compressedBody);

                Assert.That(responseMessage, Is.EqualTo(outBody));

                mockHeaders.Verify(s => s.RemoveKeyValue("transfer-encoding", "chunked"), Times.Once());
                mockHeaders.Verify(s => s.UpsertKeyValue("Content-Length", "25"), Times.Once());
                mockHeaders.Verify(s => s.RemoveKeyValue("content-encoding", "gzip"), Times.Once());
                mockHeaders.Verify(s => s.UpsertKeyValue("Content-Length", "16"),
                                    Times.Once(),
                                    "The header should have been updated with the uncompressed body size");

                Assert.That(delegateHasBeenCalled, Is.True);
            }
            else
            {
                Assert.That(isBodyNeeded, Is.False);
                Assert.That(filterResponse, Is.Null);
                
            }
        }

        [Test]
        public void VerifyResponseFiltersNoCallback(
            [Values(true, false)] bool filterEnabled, 
            [Values(true, false)] bool isWhitelisted)
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet( m => m.FilteringEnabled ).Returns( filterEnabled );

            ProxyFilter filter = new ProxyFilter(settings.Object);
            List<IResponseFilter> filters = new List<IResponseFilter>();

            Mock<IHttpResponse> response = new Mock<IHttpResponse>();
            response.SetupAllProperties();

            Mock<IHttpRequest> requestArgs = new Mock<IHttpRequest>();
            requestArgs.SetupAllProperties();

            // Whitelister
            Mock<IWhitelistEvaluator> whiteLister = new Mock<IWhitelistEvaluator>();
            whiteLister.Setup(m => m.IsWhitlisted(requestArgs.Object, "connectionid")).Returns(isWhitelisted);
            List<IWhitelistEvaluator> whitelistEvaluators = new List<IWhitelistEvaluator>();
            whitelistEvaluators.Add(whiteLister.Object);

            // Filter under test
            Mock<IResponseFilter> mockFilter = new Mock<IResponseFilter>();
            mockFilter.SetupGet( s => s.FilterSpeedType ).Returns( FilterSpeedType.Remote );
            Func<IHttpResponse, string, byte[], byte[]> outParm = null;
            mockFilter.Setup( s => s.EvaluateFilter( response.Object, "connectionid", out outParm ) ).Returns( "foo" );

            filters.Add( mockFilter.Object );
            filter.ResponseFilters = filters;
            filter.WhitelistEvaluators = whitelistEvaluators;

            // Needed to set the internal whitelist to short-curcuit evaluation on response
            filter.EvaluateConnectionFilters( requestArgs.Object, "connectionid" );

            bool isBodyNeeded;
            byte[] filterResponse = filter.EvaluateResponseFilters( response.Object, "connectionid", out isBodyNeeded );

            Assert.That( isBodyNeeded, Is.False );

            if (filterEnabled && !isWhitelisted )
            {
                Assert.That(filterResponse,
                             Is.EqualTo( Encoding.UTF8.GetBytes(
                                 "HTTP/ 200 OK\r\nConnection: close\r\nContent length: 91\r\nContent-Type: text/html\r\n\r\n<html><head><title>Gallatin Proxy - Response Filtered</title></head><body>foo</body></html>")));
            }
            else
            {
                Assert.That(filterResponse, Is.Null);
            }
        }

        [Test]
        public void VerifyConnectionFilterSort([Values(true, false)] bool filterEnabled, [Values(true, false)] bool isWhitelisted)
        {
            Mock<ICoreSettings> settings = new Mock<ICoreSettings>();
            settings.SetupGet(m => m.FilteringEnabled).Returns(filterEnabled);

            ProxyFilter filter = new ProxyFilter(settings.Object);
            List<IConnectionFilter> filters = new List<IConnectionFilter>();

            Mock<IHttpRequest> requestArgs = new Mock<IHttpRequest>();
            requestArgs.SetupAllProperties();

            Mock<IWhitelistEvaluator> whiteLister = new Mock<IWhitelistEvaluator>();
            whiteLister.Setup(m => m.IsWhitlisted(requestArgs.Object, "whatever")).Returns(isWhitelisted);
            List<IWhitelistEvaluator> whitelistEvaluators = new List<IWhitelistEvaluator>();
            whitelistEvaluators.Add(whiteLister.Object);

            Mock<IConnectionFilter> mockFilter = new Mock<IConnectionFilter>();
            mockFilter.SetupGet( s => s.FilterSpeedType ).Returns( FilterSpeedType.Remote );

            Mock<IConnectionFilter> mockFilter2 = new Mock<IConnectionFilter>();
            mockFilter2.SetupGet( s => s.FilterSpeedType ).Returns( FilterSpeedType.LocalAndFast );
            mockFilter2.Setup( s => s.EvaluateFilter( requestArgs.Object, It.IsAny<string>() ) ).Returns( "Foo" );

            filters.Add( mockFilter.Object );
            filters.Add( mockFilter2.Object );
            filter.ConnectionFilters = filters;
            filter.WhitelistEvaluators = whitelistEvaluators;

            byte[] output = filter.EvaluateConnectionFilters( requestArgs.Object, "whatever" );

            if (filterEnabled && !isWhitelisted)
            {
                Assert.That(output,
                             Is.EqualTo(
                                 Encoding.UTF8.GetBytes( "HTTP/ 200 OK\r\nConnection: close\r\nContent length: 93\r\nContent-Type: text/html\r\n\r\n<html><head><title>Gallatin Proxy - Connection Rejected</title></head><body>Foo</body></html>")));
            }
            else
            {
                Assert.That( output, Is.Null );
            }
        }

    }
}