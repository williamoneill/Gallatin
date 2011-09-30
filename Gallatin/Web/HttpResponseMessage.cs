using System.Collections.Generic;

namespace Gallatin.Core.Web
{
    public class HttpResponseMessage : HttpMessage, IHttpResponseMessage
    {
        public HttpResponseMessage( byte[] body, string version, IEnumerable<KeyValuePair<string, string>> headers, int statusCode, string statusText )
            : base(body, version, headers )
        {
            // TODO: assert parameters

            StatusCode = statusCode;
            StatusText = statusText;
        }

        public int StatusCode { get; private set; }

        public string StatusText { get; private set; }

        protected override string CreateHttpStatusLine()
        {
            return string.Format( "HTTP/{0} {1} {2}", Version, StatusCode, StatusText );
        }
    }
}