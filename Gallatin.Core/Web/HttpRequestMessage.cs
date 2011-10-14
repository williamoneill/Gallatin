
using System;
using System.Collections.Generic;
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
            // TODO: assert parameters

            Method = method;
            Destination = destination;

            if (method.Equals("connect", StringComparison.InvariantCultureIgnoreCase))
            {
                IsSsl = true;
                Host = destination.Host;
                Port = destination.Port;

                if (destination.Port == -1)
                {
                    const int HTTPS_PORT = 443;
                    const int SNEWS_PORT = 563;

                    string[] tokens = destination.AbsoluteUri.Split(':');
                    if (tokens.Length == 2)
                    {
                        Host = tokens[0];
                        Port = int.Parse(tokens[1]);
                    }

                    // Only allow SSL on well-known ports. This is the general guidance for HTTPS.
                    if (Port != HTTPS_PORT
                         && Port != SNEWS_PORT && IsSsl)
                    {
                        Log.Error(
                            "{0} Client attempted to connect via SSL to an unsupported port {1}", Port);

                        IsSsl = false;
                    }
                    
                }
            }
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