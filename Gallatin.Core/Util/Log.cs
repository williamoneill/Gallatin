namespace Gallatin.Core.Util
{
    public static class Log 
    {
        public static Logger Logger { get; private set; }

        static Log()
        {
            Logger = new Logger("proxyLog");
        }
    }
}
