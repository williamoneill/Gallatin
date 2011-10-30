namespace Gallatin.Core.Util
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
