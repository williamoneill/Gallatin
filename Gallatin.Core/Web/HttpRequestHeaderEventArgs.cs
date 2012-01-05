using System;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// HTTP request header event arguments
    /// </summary>
    public class HttpRequestHeaderEventArgs : HttpHeaderEventArgs
    {
        /// <summary>
        /// Creates the default instance of the class
        /// </summary>
        /// <param name="version">HTTP version</param>
        /// <param name="headers">HTTP headers</param>
        /// <param name="method">HTTP request method</param>
        /// <param name="path">Remote host path</param>
        public HttpRequestHeaderEventArgs( string version, IHttpHeaders headers, string method, string path )
            : base( version, headers )
        {
            Contract.Requires( !string.IsNullOrEmpty( method ) );
            Contract.Requires( !string.IsNullOrEmpty( path ) );

            Path = path;
            Method = method;

            // Convert Proxy-Connection to Connection. This header causes some problems with certain sites.
            // See http://homepage.ntlworld.com./jonathan.deboynepollard/FGA/web-proxy-connection-header.html
            if ( version == "1.0" )
            {
                Headers.Remove( "Proxy-Connection" );
            }
            else
            {
                Headers.RenameKey( "Proxy-Connection", "Connection" );
            }
        }

        /// <summary>
        /// Gets a flag indicating if the connection is using SSL (HTTPS)
        /// </summary>
        public bool IsSsl
        {
            get
            {
                return Method.Equals( "connect", StringComparison.InvariantCultureIgnoreCase );
            }
        }

        /// <summary>
        /// Gets the HTTP request method
        /// </summary>
        public string Method { get; private set; }
        
        /// <summary>
        /// Gets and sets the path on the remote host
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Creates the first line in the HTTP request
        /// </summary>
        /// <returns>Formatted line for the HTTP request</returns>
        protected override string CreateFirstLine()
        {
            if (Path.StartsWith("http://"))
            {
                // Skip past http://fobar.com/somepage.html and reduce to /somepage.html

                const int SkipPastHttpPrefix = 7;

                var index = Path.IndexOf( '/', SkipPastHttpPrefix );

                //Uri hostUri = new Uri(Path);

                // Sites like YouTube get cranky if we send the complete path in the first line.
                // Only send the relative path and query.
                //return string.Format("{0} {1} HTTP/{2}", Method, hostUri.PathAndQuery, Version);
                return string.Format("{0} {1} HTTP/{2}", Method, Path.Substring(index), Version);
            }

            return string.Format( "{0} {1} HTTP/{2}", Method, Path, Version );
        }
    }
}