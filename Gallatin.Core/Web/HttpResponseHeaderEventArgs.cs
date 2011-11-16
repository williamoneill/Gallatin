using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    internal class HttpResponseHeaderEventArgs : HttpHeaderEventArgs
    {
        public HttpResponseHeaderEventArgs( string version, IHttpHeaders headers, int statusCode, string statusText )
            : base(version, headers)
        {
            Contract.Requires(statusText != null);
            Contract.Requires(statusCode > 99);
            Contract.Requires(statusCode<1000);

            StatusCode = statusCode;
            StatusText = statusText;
        }

        public bool IsPersistent
        {
            get
            {
                // HTTP 1.1, assume persistent connection
                if (Version == "1.1")
                {
                    string persistentConnection = Headers["connection"];
                    if (persistentConnection != null && persistentConnection.Equals("close", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return false;
                    }

                    string proxyConnection = Headers["proxy-connection"];
                    if (proxyConnection != null && proxyConnection.Equals("close", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return false;
                    }

                    return true;
                }
                return false;
            }
        }
        public int StatusCode { get; private set; }
        public string StatusText { get; private set; }

        protected override string CreateFirstLine()
        {
            return string.Format("HTTP/{0} {1} {2}", Version, StatusCode, StatusText);
        }
    }
}