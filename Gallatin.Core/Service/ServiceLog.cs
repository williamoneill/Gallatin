using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    internal static class ServiceLog 
    {
        public static Logger Logger { get; private set; }

        static ServiceLog()
        {
            Logger = new Logger("proxyLog");
        }
    }
}
