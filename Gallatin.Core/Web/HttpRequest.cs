using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// Represents the HTTP request
    /// </summary>
    public class HttpRequest : IHttpRequest
    {
        private readonly HttpRequestHeaderEventArgs _args;

        private HttpRequest( HttpRequestHeaderEventArgs args )
        {
            _args = args;
        }

        #region IHttpRequest Members

        /// <summary>
        /// Relative path of the remote resource
        /// </summary>
        public string Path
        {
            get
            {
                return _args.Path;
            }
            set
            {
                _args.Path = value;
            }
        }

        /// <summary>
        /// HTTP version
        /// </summary>
        public string Version
        {
            get
            {
                return _args.Version;
            }
        }

        /// <summary>
        /// HTTP method
        /// </summary>
        public string Method
        {
            get
            {
                return _args.Method;
            }
        }

        /// <summary>
        /// HTTP headers
        /// </summary>
        public IHttpHeaders Headers
        {
            get
            {
                return _args.Headers;
            }
        }

        /// <summary>
        /// Gets the flag indicating if the request uses SSL (HTTPS)
        /// </summary>
        public bool IsSsl
        {
            get
            {
                return _args.IsSsl;
            }
        }

        /// <summary>
        /// Gets the HTTP body
        /// </summary>
        public byte[] Body { get; private set; }

        #endregion

        internal static HttpRequest CreateRequest( HttpRequestHeaderEventArgs args )
        {
            Contract.Requires( args != null );

            HttpRequest request = new HttpRequest( args );

            return request;
        }
    }
}