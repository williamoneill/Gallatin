using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service
{
    internal class HttpRequestData
    {
        public HttpRequestHeaderEventArgs RequestArgs { get; private set; }
    }

    internal class ContentFilter
    {
        public byte[] EvaluateConnectionRequest(HttpRequestData requestData)
        {
            Contract.Requires(requestData!=null);
            throw new NotImplementedException();
        }
    }
}
