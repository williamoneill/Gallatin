using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Service;

namespace Gallatin.Core.Filters
{
    internal class HttpResponseFilter : IHttpResponseFilter
    {
        private IHttpRequest _request;
        private string _clientConnectionId;
        private IAccessLog _accessLog;
        private IFilterCollections _filterCollections;


        public HttpResponseFilter(IHttpRequest request, string connectionId, IAccessLog accessLog, IFilterCollections collections)
        {
            Contract.Requires(request!=null);
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires(accessLog!=null);
            Contract.Requires(collections!=null);

            _request = request;
            _clientConnectionId = connectionId;
            _accessLog = accessLog;
            _filterCollections = collections;
        }


        public byte[] ApplyResponseHeaderFilters(IHttpResponse response, out IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> bodyCallbacks)
        {
            bodyCallbacks = null;

            List<Func<IHttpResponse, string, byte[], byte[]>> callbacks = new List<Func<IHttpResponse, string, byte[], byte[]>>();

            if ( _filterCollections.ResponseFilters == null || _filterCollections.ResponseFilters.Count() == 0)
            {
                _accessLog.Write(_clientConnectionId, _request, AccessLogType.AccessGranted);
                return null;
            }

            byte[] returnData = null;
            string errorMessage = null;

            foreach (IResponseFilter filter in _filterCollections.ResponseFilters.OrderBy(s => s.FilterSpeedType))
            {
                Func<IHttpResponse, string, byte[], byte[]> callback;
                string filterText = filter.EvaluateFilter(response, _clientConnectionId, out callback);

                if (!String.IsNullOrEmpty(filterText))
                {
                    // Fail fast
                    errorMessage = filterText;

                    // Don't bother with the callbacks. The response has been filtered without the HTTP body.
                    // For speed, these types of filters are preferable over filters that require the body.
                    callbacks.Clear();
                    break;
                }

                if (callback != null)
                {
                    callbacks.Add(callback);
                }
            }

            if (!String.IsNullOrEmpty(errorMessage))
            {
                _accessLog.Write(_clientConnectionId, _request, AccessLogType.AccessBlocked);

                string body = String.Format("<html><head><title>Gallatin Proxy - Response Filtered</title></head><body>{0}</body></html>",
                                            errorMessage);

                returnData = Encoding.UTF8.GetBytes(
                    String.Format("HTTP/{0} 200 OK\r\nConnection: close\r\nContent length: {1}\r\nContent-Type: text/html\r\n\r\n{2}",
                                  response.Version,
                                  body.Length,
                                  body));
            }
            else
            {
                // Clear the filters that require the HTTP body if there will be no body. Filters should
                // check this themselves. This is a catch-all. Without this, a bad filter could hang the proxy
                // by waiting for a body that will never arrive.
                if (!response.HasBody)
                {
                    callbacks.Clear();
                }

                if (callbacks.Count == 0)
                {
                    _accessLog.Write(_clientConnectionId, _request, AccessLogType.AccessGranted);
                }
                else
                {
                    bodyCallbacks = callbacks;
                }
            }

            return returnData;
        }

        private static byte[] DecompressBody(IHttpResponse args, byte[] body)
        {
            string contentEncoding = args.Headers["content-encoding"];
            if (contentEncoding != null
                && contentEncoding.ToLower().Contains("gzip"))
            {
                ServiceLog.Logger.Verbose("Removing compression from HTTP response");

                args.Headers.RemoveKeyValue("content-encoding", "gzip");
                MemoryStream memoryStream = new MemoryStream(body);
                using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    MemoryStream newBody = new MemoryStream();
                    gZipStream.CopyTo(newBody);

                    body = new byte[newBody.Length];
                    Array.Copy(newBody.ToArray(), body, body.Length);
                    args.Headers.UpsertKeyValue("Content-Length", body.Length.ToString());
                }
            }
            return body;
        }


        public byte[] ApplyResponseBodyFilter(IHttpResponse response, byte[] body, IEnumerable<Func<IHttpResponse, string, byte[], byte[]>> bodyCallbacks)
        {
            byte[] filteredBody = null;

            if (body != null && body.Length > 0)
            {
                ServiceLog.Logger.Verbose("Evaluating body filters");

                // The body has been re-assembled. Remove the "chunked" header.
                response.Headers.RemoveKeyValue("transfer-encoding", "chunked");

                // Decompress the body so filters don't have to repeatedly do this themselves
                body = DecompressBody(response, body);

                ServiceLog.Logger.Verbose(() => Encoding.UTF8.GetString(body));

                foreach (Func<IHttpResponse, string, byte[], byte[]> callback in bodyCallbacks)
                {
                    filteredBody = callback(response, _clientConnectionId, body);

                    if (filteredBody != null)
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
                _accessLog.Write(_clientConnectionId, _request, AccessLogType.AccessBlocked);

                ServiceLog.Logger.Verbose("Response body has been modified by proxy server");
                response.Headers.UpsertKeyValue("Content-Length", filteredBody.Length.ToString());
            }
            else
            {
                _accessLog.Write(_clientConnectionId, _request, AccessLogType.AccessGranted);

                ServiceLog.Logger.Verbose("Proxy server did not modify response body");
                filteredBody = body;
            }

            byte[] header = response.GetBuffer();

            byte[] returnBuffer = new byte[header.Length + filteredBody.Length];
            Array.Copy(header, returnBuffer, header.Length);
            Array.Copy(filteredBody, 0, returnBuffer, header.Length, filteredBody.Length);

            return returnBuffer;
        }
    }
}