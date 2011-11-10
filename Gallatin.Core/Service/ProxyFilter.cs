using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Text;
using Gallatin.Contracts;
using System.Linq;


namespace Gallatin.Core.Service
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(IProxyFilter))]
    internal class ProxyFilter : IProxyFilter
    {
        [ImportMany]
        public IEnumerable<IConnectionFilter> ConnectionFilters { get; set; }

        [ImportMany]
        public IEnumerable<IResponseFilter> ResponseFilters { get; set; }

        private List<Func<IHttpResponse, string, byte[], byte[]>> _callbackList = new List<Func<IHttpResponse, string, byte[], byte[]>>();

        public byte[] EvaluateResponseFiltersWithBody( IHttpResponse args, string connectionId, byte[] body )
        {
            Contract.Requires(args!=null);
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires( ResponseFilters != null );

            byte[] filteredBody = null;

            if(body!= null && body.Length> 0)
            {
                // The body has been re-assembled. Remove the "chunked" header.
                args.Headers.RemoveKeyValue("transfer-encoding", "chunked");

                // Decompress the body so filters don't have to repeatedly do this themselves
                body = DecompressBody(args, body);

                ServiceLog.Logger.Verbose("Evaluating body filters");

                foreach (var callback in _callbackList)
                {
                    filteredBody = callback( args, connectionId, body );

                    if(filteredBody!= null)
                    {
                        break;
                    }
                }
            }

            // Update the HTTP headers by adding the content length for the new body.
            // If the body was not modified by the filters then reset this to the
            // body that was passed in, which has possibly been decompressed.
            if (filteredBody != null)
            {
                args.Headers.UpsertKeyValue("Content-Length", filteredBody.Length.ToString());
            }
            else
            {
                filteredBody = body;
            }

            return filteredBody;
        }

        private static byte[] DecompressBody( IHttpResponse args, byte[] body )
        {
            var contentEncoding = args.Headers["content-encoding"];
            if ( contentEncoding != null
                 && contentEncoding.ToLower().Contains( "gzip" ) )
            {
                ServiceLog.Logger.Verbose( "Removing compression from HTTP response" );

                args.Headers.RemoveKeyValue( "content-encoding", "gzip" );
                MemoryStream memoryStream = new MemoryStream( body );
                using ( GZipStream gZipStream = new GZipStream( memoryStream, CompressionMode.Decompress ) )
                {
                    MemoryStream newBody = new MemoryStream();
                    gZipStream.CopyTo( newBody );

                    body = new byte[newBody.Length];
                    Array.Copy( newBody.ToArray(), body, body.Length );
                    args.Headers.UpsertKeyValue( "Content-Length", body.Length.ToString() );
                }
            }
            return body;
        }


        public bool TryEvaluateResponseFilters(IHttpResponse args, string connectionId, out string filterResponse)
        {
            Contract.Requires(args != null);
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires( ResponseFilters != null );

            string errorMessage = null;

            _callbackList.Clear();

            foreach (var filter in ResponseFilters.OrderBy(s => s.FilterSpeedType))
            {
                Func<IHttpResponse, string, byte[], byte[]> callback;
                var filterText = filter.EvaluateFilter(args, connectionId, out callback);

                if (!string.IsNullOrEmpty(filterText))
                {
                    // Fail fast
                    errorMessage = filterText;

                    // Don't bother with the callbacks. The response has been filtered without the HTTP body.
                    // These types of filters are preferable over filters that require the body.
                    _callbackList.Clear();
                    break;
                }

                if (callback != null)
                {
                    _callbackList.Add(callback);
                }
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                string body = string.Format("<html><head><title>Gallatin Proxy - Response Filtered</title></head><body>{0}</body></html>",
                                             errorMessage);

                filterResponse = string.Format("HTTP/{0} 200 OK\r\nConnection: close\r\nContent length: {1}\r\nContent-Type: text/html\r\n\r\n{2}",
                                      args.Version,
                                      body.Length,
                                      body);
            }
            else
            {
                // Clear the filters that require the HTTP body if there will be no body. Filters should
                // check this themselves. This is a catch-all. Without this, a bad filter could hang the proxy
                // by waiting for a body that will never arrive.
                if (!args.HasBody)
                {
                    _callbackList.Clear();
                }

                filterResponse = null;
            }

            return _callbackList.Count == 0;
        }

        public string EvaluateConnectionFilters( IHttpRequest args, string connectionId )
        {
            Contract.Requires(args != null);
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires(ConnectionFilters!=null);

            string errorMessage = null;

            foreach (var filter in ConnectionFilters.OrderBy( s => s.FilterSpeedType ))
            {
                var filterText = filter.EvaluateFilter( args, connectionId );

                if (!string.IsNullOrEmpty(filterText))
                {
                    // Fail fast
                    errorMessage = filterText;
                    break;
                }
            }

            if(!string.IsNullOrEmpty(errorMessage))
            {
                string body = string.Format( "<html><head><title>Gallatin Proxy - Connection Rejected</title></head><body>{0}</body></html>",
                                             errorMessage );

                return string.Format( "HTTP/{0} 200 OK\r\nConnection: close\r\nContent length: {1}\r\nContent-Type: text/html\r\n\r\n{2}",
                                      args.Version,
                                      body.Length,
                                      body );
            }

            return null;
        }

    }
}