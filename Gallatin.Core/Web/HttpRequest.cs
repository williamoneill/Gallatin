using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    internal class HttpRequest : IHttpRequest
    {
        private readonly HttpRequestHeaderEventArgs _args;

        private HttpRequest( HttpRequestHeaderEventArgs args )
        {
            Contract.Requires(args!=null);
            _args = args;
        }

        #region IHttpRequest Members

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

        public string Version
        {
            get
            {
                return _args.Version;
            }
        }

        public string Method
        {
            get
            {
                return _args.Method;
            }
        }

        public IHttpHeaders Headers
        {
            get
            {
                return _args.Headers;
            }
        }

        public bool HasBody
        {
            get
            {
                return _args.HasBody;
            }
        }

        public bool IsSsl
        {
            get
            {
                return _args.IsSsl;
            }
        }

        #endregion

        internal static HttpRequest CreateRequest( HttpRequestHeaderEventArgs args )
        {
            Contract.Requires( args != null );
            return new HttpRequest( args );
        }
    }
}