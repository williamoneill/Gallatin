using System;
using System.Diagnostics.Contracts;
using System.Text;
using Gallatin.Contracts;

namespace Gallatin.Core.Web
{
    internal abstract class HttpHeaderEventArgs :EventArgs
    {
        protected HttpHeaderEventArgs( string version, IHttpHeaders headers)
        {
            Contract.Requires(!string.IsNullOrEmpty(version));
            Contract.Requires(headers!=null);

            Version = version;
            Headers = headers;
        }

        public bool HasBody
        {
            get
            {
                string contentLength = Headers["content-length"];
                string chunkedData = Headers["transfer-encoding"];

                return ( (contentLength != null && int.Parse(contentLength) > 0)
                    || (chunkedData != null && chunkedData.ToLower().Contains("chunked")));
            }
        }

        public string Version { get; protected set; }
        public IHttpHeaders Headers { get; protected set; }

        protected abstract string CreateFirstLine();

        public byte[] GetBuffer()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendFormat("{0}\r\n", CreateFirstLine());

            foreach ( var header in Headers.AsEnumerable() )
            {
                builder.AppendFormat("{0}: {1}\r\n", header.Key, header.Value);
            }

            builder.AppendFormat("\r\n");

            return Encoding.UTF8.GetBytes( builder.ToString() );
        }


    }
}