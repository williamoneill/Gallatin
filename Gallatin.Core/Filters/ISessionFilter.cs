using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Service;

namespace Gallatin.Core.Filters
{
    internal interface IFilterCollections
    {
        IEnumerable<IConnectionFilter> ConnectionFilters { get; }

        IEnumerable<IResponseFilter> ResponseFilters { get; }

        IEnumerable<IWhitelistEvaluator> WhitelistEvaluators { get; }
    }

    internal interface IHttpFilter
    {
        byte[] ApplyConnectionFilters(IHttpRequest request, string connectionId);

        IHttpResponseFilter CreateResponseFilters(IHttpRequest request, string connectionId );
    }

    internal interface IHttpResponseFilter
    {
        byte[] ApplyResponseHeaderFilters(IHttpResponse response, out bool bodyRequired );

        byte[] ApplyResponseBodyFilter(IHttpResponse response, byte[] body);
    }






    [Export(typeof (IFilterCollections))]
    internal class FilterCollections : IFilterCollections
    {
        [ImportMany]
        public IEnumerable<IConnectionFilter> ConnectionFilters { get; set; }

        [ImportMany]
        public IEnumerable<IResponseFilter> ResponseFilters { get; set; }

        [ImportMany]
        public IEnumerable<IWhitelistEvaluator> WhitelistEvaluators { get; set; }
    }

    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(IHttpFilter))]
    internal class HttpFilter : IHttpFilter
    {
        private IFilterCollections _filterCollections;
        private IAccessLog _accessLog;
        private ICoreSettings _settings;

        [ImportingConstructor]
        public HttpFilter(IFilterCollections filterCollections, IAccessLog accessLog, ICoreSettings settings)
        {
            Contract.Requires(filterCollections!=null);
            Contract.Requires(accessLog!=null);
            Contract.Requires(settings!=null);

            _settings = settings;
            _filterCollections = filterCollections;
            _accessLog = accessLog;
        }

        private bool IsWhitelisted( IHttpRequest request, string connectionId )
        {
            if (_filterCollections.WhitelistEvaluators != null && _filterCollections.WhitelistEvaluators.Count() > 0)
            {
                return _filterCollections.WhitelistEvaluators
                    .OrderBy(s => s.FilterSpeedType)
                    .Any(whitelistEvaluator => whitelistEvaluator.IsWhitlisted(request, connectionId));

            }

            return false;
        }

        public byte[] ApplyConnectionFilters(IHttpRequest request, string connectionId)
        {
            if(IsWhitelisted(request,connectionId) || !_settings.FilteringEnabled.Value)
            {
                return null;
            }

            if ( _filterCollections.ConnectionFilters != null && _filterCollections.ConnectionFilters.Count() > 0)
            {
                string errorMessage = _filterCollections.ConnectionFilters.OrderBy(s => s.FilterSpeedType)
                    .Select(filter => filter.EvaluateFilter(request, connectionId))
                    .FirstOrDefault(filterText => !String.IsNullOrEmpty(filterText));

                if (!String.IsNullOrEmpty(errorMessage))
                {
                    _accessLog.Write(connectionId, request, AccessLogType.AccessBlocked);

                    string body = String.Format(
                        "<html><head><title>Gallatin Proxy - Connection Rejected</title></head><body>{0}</body></html>",
                        errorMessage);

                    return Encoding.UTF8.GetBytes(
                        String.Format("HTTP/{0} 200 OK\r\nConnection: close\r\nContent length: {1}\r\nContent-Type: text/html\r\n\r\n{2}",
                                       request.Version,
                                       body.Length,
                                       body));
                }
            }

            return null;
        }

        public IHttpResponseFilter CreateResponseFilters(IHttpRequest request, string connectionId)
        {
            if (IsWhitelisted(request, connectionId) || !_settings.FilteringEnabled.Value)
            {
                // Don't apply future filters for this request
                return null;
            }

            return new HttpResponseFilter(request, connectionId, _accessLog, _filterCollections);
        }
    }

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

        private readonly List<Func<IHttpResponse, string, byte[], byte[]>> _callbackList =
            new List<Func<IHttpResponse, string, byte[], byte[]>>();

        public byte[] ApplyResponseHeaderFilters(IHttpResponse response, out bool isBodyRequired)
        {
            isBodyRequired = false;

            if ( _filterCollections.ResponseFilters == null || _filterCollections.ResponseFilters.Count() == 0)
            {
                _accessLog.Write(_clientConnectionId, _request, AccessLogType.AccessGranted);
                return null;
            }

            string errorMessage = null;

            _callbackList.Clear();

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
                    _callbackList.Clear();
                    break;
                }

                if (callback != null)
                {
                    _callbackList.Add(callback);
                }
            }

            if (!String.IsNullOrEmpty(errorMessage))
            {
                _accessLog.Write(_clientConnectionId, _request, AccessLogType.AccessBlocked);

                string body = String.Format("<html><head><title>Gallatin Proxy - Response Filtered</title></head><body>{0}</body></html>",
                                                errorMessage);

                return Encoding.UTF8.GetBytes(
                    String.Format("HTTP/{0} 200 OK\r\nConnection: close\r\nContent length: {1}\r\nContent-Type: text/html\r\n\r\n{2}",
                                    response.Version,
                                    body.Length,
                                    body));
            }

            // Clear the filters that require the HTTP body if there will be no body. Filters should
            // check this themselves. This is a catch-all. Without this, a bad filter could hang the proxy
            // by waiting for a body that will never arrive.
            if (!response.HasBody)
            {
                _callbackList.Clear();
            }
            
            if(_callbackList.Count > 0)
            {
                isBodyRequired = true;
            }
            else
            {
                _accessLog.Write(_clientConnectionId, _request, AccessLogType.AccessGranted);
            }

            return null;
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


        public byte[] ApplyResponseBodyFilter(IHttpResponse response, byte[] body)
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

                foreach (Func<IHttpResponse, string, byte[], byte[]> callback in _callbackList)
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
