using System.Diagnostics.Contracts;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    internal class HttpResponse : IHttpResponse
    {
        private HttpResponseHeaderEventArgs _args;

        public HttpResponse( HttpResponseHeaderEventArgs args )
        {
            Contract.Requires(args!=null);
            _args = args;
        }

        public string Version
        {
            get
            {
                return _args.Version;
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

        public int Status
        {
            get
            {
                return _args.StatusCode;
            }
        }

        public string StatusText
        {
            get
            {
                return _args.StatusText;
            }
        }

        public bool IsPersistent
        {
            get
            {
                return _args.IsPersistent;
            }
        }

        public byte[] GetBuffer()
        {
            return _args.GetBuffer();
        }
        public static HttpResponse CreateResponse( HttpResponseHeaderEventArgs args )
        {
            Contract.Requires(args != null);
            return new HttpResponse(args);
        }
    }
}