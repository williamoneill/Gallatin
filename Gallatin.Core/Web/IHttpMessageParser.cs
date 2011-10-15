

using System.Collections.Generic;
using Gallatin.Core.Util;

namespace Gallatin.Core.Web
{
    public interface IHttpMessageParser : IPooledObject
    {
        IHttpMessage AppendData( IEnumerable<byte> rawNetworkContent );
        bool TryGetHeader( out IHttpMessage message );
        bool TryGetCompleteMessage( out IHttpMessage message );
        bool TryGetCompleteResponseMessage(out IHttpResponseMessage message);
        bool TryGetCompleteRequestMessage(out IHttpRequestMessage message);
        byte[] AllData { get; }
    }
}