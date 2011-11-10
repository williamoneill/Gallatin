using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    internal class HttpRequestHeaderEventArgs : HttpHeaderEventArgs
    {
        public HttpRequestHeaderEventArgs( string version, IHttpHeaders headers, string method, string path )
            : base( version, headers )
        {
            Contract.Requires( !string.IsNullOrEmpty( method ) );
            Contract.Requires( !string.IsNullOrEmpty( path ) );

            Path = path;
            Method = method;

            // Convert Proxy-Connection to Connection. This header causes some problems with certain sites.
            // See http://homepage.ntlworld.com./jonathan.deboynepollard/FGA/web-proxy-connection-header.html
            if (version == "1.0")
            {
                Headers.Remove("Proxy-Connection");
            }
            else
            {
                Headers.RenameKey("Proxy-Connection", "Connection");
            }
        }

        public bool IsSsl
        {
            get
            {
                return Method.Equals( "connect", StringComparison.InvariantCultureIgnoreCase );
            }
        }

        public string Method { get; private set; }
        public string Path { get; set; }

        protected override string CreateFirstLine()
        {
            if (Path.StartsWith("http"))
            {
                Uri hostUri = new Uri(Path);

                // Sites like YouTube get cranky if we send the complete path in the first line.
                // Only send the relative path and query.
                return string.Format("{0} {1} HTTP/{2}", Method, hostUri.PathAndQuery, Version);
            }

            return string.Format("{0} {1} HTTP/{2}", Method, Path, Version);
        }
    }
}