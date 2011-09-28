using System;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public interface IHttpResponseMessage : IHttpMessage
    {
        int StatusCode { get;  }

        string StatusText { get; }
    }
}
