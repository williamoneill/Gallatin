
using System;

namespace Gallatin.Core.Web
{
    public interface IHttpRequestMessage : IHttpMessage
    {
        string Method { get; }

        // Deprecated
        Uri Destination { get; }

        string Host
        {
            get; 
        }

        int Port
        {
            get;
        }

        bool IsSsl
        {
            get;
        }
    }
}