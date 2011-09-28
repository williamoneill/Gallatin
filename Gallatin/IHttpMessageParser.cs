using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public interface IHttpMessageParser
    {
        IHttpMessage AppendData( IEnumerable<byte> rawNetworkContent );
    }
}
