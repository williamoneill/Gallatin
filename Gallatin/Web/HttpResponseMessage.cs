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

namespace Gallatin.Core.Web
{
    public class HttpResponseMessage : HttpMessage, IHttpResponseMessage
    {
        public HttpResponseMessage( byte[] body,
                                    string version,
                                    IEnumerable<KeyValuePair<string, string>> headers,
                                    int statusCode,
                                    string statusText )
            : base( body, version, headers )
        {
            // TODO: assert parameters

            StatusCode = statusCode;
            StatusText = statusText;
        }

        #region IHttpResponseMessage Members

        public int StatusCode { get; private set; }

        public string StatusText { get; private set; }

        #endregion

        protected override string CreateHttpStatusLine()
        {
            return string.Format( "HTTP/{0} {1} {2}", Version, StatusCode, StatusText );
        }
    }
}