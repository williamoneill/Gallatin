

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Web
{
    public abstract class HttpMessage : IHttpMessage
    {
        protected HttpMessage( byte[] body,
                               string version,
                               IEnumerable<KeyValuePair<string, string>> headers )
        {
            // TODO: assert all parameters

            Body = body;
            Version = version;
            Headers = headers;
        }

        #region IHttpMessage Members

        public string this[string key]
        {
            get
            {
                if(Headers != null)
                {
                    var header =
                        Headers.FirstOrDefault(
                            s => s.Key.Equals( key, StringComparison.InvariantCultureIgnoreCase ) );

                    if(header.Equals(default(KeyValuePair<string,string> )))
                    {
                        return null;
                    }
                    return header.Value;
                }
                return null;
            }
        }

        public byte[] Body { get; private set; }

        public string Version { get; private set; }

        public IEnumerable<KeyValuePair<string, string>> Headers { get; private set; }

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

            if ( Body != null )
            {
                message.AddRange( Body );
            }

            return message.ToArray();
        }

        #endregion

        protected abstract string CreateHttpStatusLine();
    }
}