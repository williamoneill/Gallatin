using System;
using System.ComponentModel.Composition;

namespace Gallatin.Core
{
    [Export( typeof (ICoreSettings) )]
    public class CoreSettings : ICoreSettings
    {
        #region ICoreSettings Members

        public int NetworkAddressBindingOrdinal { get; set; }

        public int ServerPort { get; set; }

        public int MaxNumberClients { get; set; }

        public int ReceiveBufferSize { get; set; }

        public int WatchdogThreadSleepInterval { get; set; }

        public int SessionInactivityTimeout { get; set; }

        #endregion
    }
}