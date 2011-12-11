using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// HTTP response header event arguments
    /// </summary>
    public class HttpResponseHeaderEventArgs : HttpHeaderEventArgs
    {
        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        /// <param name="version">HTTP version</param>
        /// <param name="headers">HTTP headers</param>
        /// <param name="statusCode">HTTP response status code</param>
        /// <param name="statusText">HTTP response status text</param>
        public HttpResponseHeaderEventArgs( string version, IHttpHeaders headers, int statusCode, string statusText )
            : base(version, headers)
        {
            Contract.Requires(statusText != null);
            Contract.Requires(statusCode > 99);
            Contract.Requires(statusCode<1000);

            StatusCode = statusCode;
            StatusText = statusText;
        }

        /// <summary>
        /// Gets a flag indicating if the connection is persistent
        /// </summary>
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

        /// <summary>
        /// Gets the HTTP response status code
        /// </summary>
        public int StatusCode { get; private set; }

        /// <summary>
        /// Gets the HTTP response status text
        /// </summary>
        public string StatusText { get; private set; }

        /// <summary>
        /// Creates the first line in the HTTP response
        /// </summary>
        /// <returns>Formatted line for the HTTP response</returns>
        protected override string CreateFirstLine()
        {
            return string.Format("HTTP/{0} {1} {2}", Version, StatusCode, StatusText);
        }
    }
}