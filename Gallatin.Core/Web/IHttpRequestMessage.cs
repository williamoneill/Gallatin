
using System;

namespace Gallatin.Core.Web
{
    public interface IHttpRequestMessage : IHttpMessage
    {
        string Method { get; }

        Uri Destination { get; }
    }
}