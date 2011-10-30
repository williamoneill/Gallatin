using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Gallatin.Core.Util;

namespace Gallatin.Core.Web
{
    internal static class WebLog
    {
        public static Logger Logger { get; private set; }

        static WebLog()
        {
            Logger = new Logger("webLog");
        }
    }
}
