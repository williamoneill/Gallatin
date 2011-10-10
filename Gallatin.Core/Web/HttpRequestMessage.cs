
using System;
using System.Collections.Generic;

namespace Gallatin.Core.Web
{
    public class HttpRequestMessage : HttpMessage, IHttpRequestMessage
    {
        public HttpRequestMessage( byte[] body,
                                   string version,
                                   IEnumerable<KeyValuePair<string, string>> headers,
                                   string method,
                                   Uri destination )
            : base( body, version, headers )
        {
            // TODO: assert parameters

            Method = method;
            Destination = destination;
        }

        #region IHttpRequestMessage Members

        public string Method { get; private set; }

        public Uri Destination { get; private set; }

        #endregion

        protected override string CreateHttpStatusLine()
        {
            return string.Format( "{0} {1} HTTP/{2}", Method, Destination.PathAndQuery, Version );
        }
    }
}