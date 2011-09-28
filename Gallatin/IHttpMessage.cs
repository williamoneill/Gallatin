using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core
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
