using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public interface IProxyServer
    {
        void Start(int port);

        void Stop();
    }
}
