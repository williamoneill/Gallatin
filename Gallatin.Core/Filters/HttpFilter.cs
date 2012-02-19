using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Service;

namespace Gallatin.Core.Filters
{
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
}