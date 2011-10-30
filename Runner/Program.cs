using System;
using System.Reflection;
using Gallatin.Core;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using System.IO;

namespace Runner
{
    internal class Program
    {
        private static void Main(  )
        {
            try
            {
                IProxyService server = CoreFactory.Create<IProxyService>();
                server.Start();

                Console.WriteLine("Gallatin Proxy (www.gallatinproxy.com) v.{0}", Assembly.GetExecutingAssembly().GetName().Version);
                Console.WriteLine();
                Console.WriteLine("Listening for client connections...");
                Console.WriteLine();
                Console.WriteLine("Press any key to terminate");
                Console.ReadKey();

                server.Stop();

            }
            catch ( Exception ex )
            {
                Console.WriteLine("Unhandled exception: " + ex.Message);
            }
        }

    }
}