using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gallatin.Core
{
    public abstract class HttpMessage : IHttpMessage
    {
        public HttpMessage( byte[] body, string version, IEnumerable<KeyValuePair<string, string>> headers )
        {
            // TODO: assert all parameters

            Body = body;
            Version = version;
            Headers = headers;
        }

        public byte[] Body { get; private set; }

        public string Version { get; private set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; private set; }

        protected abstract string CreateHttpStatusLine();

        public byte[] CreateHttpMessage()
        {
            List<byte> message = new List<byte>();

            StringBuilder builder = new StringBuilder();

            builder.AppendFormat( "{0}\r\n", CreateHttpStatusLine() );

            // RFC 2612 - Proxy server cannot change order headers
            foreach ( KeyValuePair<string, string> keyValuePair in Headers )
            {
                builder.AppendFormat( "{0}: {1}\r\n", keyValuePair.Key, keyValuePair.Value );
            }

            builder.AppendFormat( "\r\n" );

            message.AddRange( Encoding.UTF8.GetBytes( builder.ToString() ) );

            if(Body != null)
                message.AddRange( this.Body );

            return message.ToArray();
        }
    }

}