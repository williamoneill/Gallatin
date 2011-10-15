
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Gallatin.Core.Util;

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
            Contract.Requires(!string.IsNullOrEmpty(version));
            Contract.Requires(!string.IsNullOrEmpty(method));
            Contract.Requires(destination != null);
            Contract.Ensures(Port > 0);
            Contract.Ensures(!string.IsNullOrEmpty(Host));
            Contract.Ensures(!string.IsNullOrEmpty(Method));
            Contract.Ensures(Destination != null);

            Method = method;
            Destination = destination;
            Host = destination.Host;
            Port = destination.Port;

            if (destination.Port == -1)
            {
                string[] tokens = destination.AbsoluteUri.Split(':');
                if (tokens.Length == 2)
                {
                    Host = tokens[0];
                    Port = int.Parse(tokens[1]);
                }
            }

            IsSsl = method.Equals( "connect", StringComparison.InvariantCultureIgnoreCase );
        }

        #region IHttpRequestMessage Members

        public string Method { get; private set; }

        public Uri Destination { get; private set; }

        // TODO: update unit tests
        public string Host
        {
            get; private set;
        }

        public int Port
        {
            get; private set;
        }

        public bool IsSsl
        {
            get; private set;
        }

        #endregion

        protected override string CreateHttpStatusLine()
        {
            return string.Format( "{0} {1} HTTP/{2}", Method, Destination.PathAndQuery, Version );
        }
    }
}