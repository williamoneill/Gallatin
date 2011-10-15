using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public interface ICoreSettings
    {
        /// <summary>
        /// Ordinal for the address the server will bind to
        /// </summary>
        int NetworkAddressBindingOrdinal
        {
            get;
            set;
        }

        int ServerPort { get; set; }

        int MaxNumberClients { get; set; }

        int ReceiveBufferSize { get; set; }
    }
}
