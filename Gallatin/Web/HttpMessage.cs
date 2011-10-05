#region License

// Copyright 2011 Bill O'Neill
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the License for the
// specific language governing permissions and limitations under the License.

#endregion

using System.Collections.Generic;
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