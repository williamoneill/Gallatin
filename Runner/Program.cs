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
                if(File.Exists("proxy.log.txt"))
                    File.Delete("proxy.log.txt");

                //http://msdn.microsoft.com/en-us/library/dd460648.aspx
                //http://msdn.microsoft.com/en-us/magazine/ee291628.aspx
                //var catalog = new AggregateCatalog();
                //catalog.Catalogs.Add(new AssemblyCatalog(typeof()));

                ICoreSettings settings = CoreSettings.Load();

                IProxyService server = new LeanProxyService( settings );
                server.Start( 8080 );

                Console.WriteLine("Gallatin Proxy (www.gallatinproxy.com) v.{0}", Assembly.GetExecutingAssembly().GetName().Version);
                Console.WriteLine();
                Console.WriteLine("Listening for client connections...");
                Console.WriteLine();
                Console.WriteLine("Press any key to terminate");
                Console.ReadKey();

                server.Stop();

                CoreSettings.Save(settings);
            }
            catch ( Exception ex )
            {
                Log.Exception( "app exception", ex );
            }
        }

    }
}