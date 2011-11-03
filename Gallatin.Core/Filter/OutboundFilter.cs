using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Web;

namespace Gallatin.Core.Filter
{
    internal class OutboundFilter
    {
        public byte[] EvaluateFilterBlock( HttpRequestHeaderEventArgs args )
        {
            return Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-length: 2\r\n\r\nHi" );
        }

    }
}
