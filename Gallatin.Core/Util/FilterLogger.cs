using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Service;

namespace Gallatin.Core.Util
{
    internal static class FilterLog
    {
        public static Logger Logger { get; private set; }

        static FilterLog()
        {
            Logger = new Logger("filterLog");
        }
    }

    [Export(typeof(ILogger))]
    internal class FilterLogger : ILogger
    {
        public void WriteInfo( string info )
        {
            FilterLog.Logger.Info(info);
        }

        public void WriteError( string error )
        {
            FilterLog.Logger.Error(error);
        }

        public void WriteWarning( string warning )
        {
            FilterLog.Logger.Warning(warning);
        }
    }
}
