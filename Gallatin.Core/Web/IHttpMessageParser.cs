

using System.Collections.Generic;

namespace Gallatin.Core.Web
{
    public interface IHttpMessageParser
    {
        IHttpMessage AppendData( IEnumerable<byte> rawNetworkContent );
        bool TryGetHeader( out IHttpMessage message );
        bool TryGetCompleteMessage( out IHttpMessage message );
        bool TryGetCompleteResponseMessage(out IHttpResponseMessage message);
        bool TryGetCompleteRequestMessage(out IHttpRequestMessage message);
    }
}