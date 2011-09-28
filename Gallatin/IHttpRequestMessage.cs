using System;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public interface IHttpRequestMessage : IHttpMessage 
    {
        string Method
        {
            get;
        }

        Uri Destination
        {
            get;
        }
    }
}
