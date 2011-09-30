using System.Collections.Generic;

namespace Gallatin.Core.Web
{
    public interface IHttpMessageParser
    {
        IHttpMessage AppendData( IEnumerable<byte> rawNetworkContent );
    }
}
