using System.Collections.Generic;

namespace Gallatin.Core.Web
{
    public interface IHttpMessage
    {
        byte[] Body
        {
            get;
        }

        string Version
        {
            get;
        }

        IEnumerable<KeyValuePair<string, string>> Headers { get; }

        byte[] CreateHttpMessage();
    }
}
