using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Web
{
    internal class HttpRequestHeaderEventArgs : HttpHeaderEventArgs
    {
        public HttpRequestHeaderEventArgs( string version, HttpHeaders headers, string method, string path )
            : base( version, headers )
        {
            Contract.Requires( !string.IsNullOrEmpty( method ) );
            Contract.Requires( !string.IsNullOrEmpty( path ) );

            Path = path;
            Method = method;
        }

        public bool IsSsl
        {
            get
            {
                return Method.Equals( "connect", StringComparison.InvariantCultureIgnoreCase );
            }
        }

        public string Method { get; private set; }
        public string Path { get; private set; }

        protected override string CreateFirstLine()
        {
            return string.Format( "{0} {1} HTTP/{2}", Method, Path, Version );
        }
    }
}