using System;
using Gallatin.Core.Client;
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

                ProxyService server = new ProxyService( new ProxyClientFactory() );
                server.Start( 8080 );

                Console.WriteLine( "Press any key to terminate" );
                Console.ReadKey();

                server.Stop();
            }
            catch ( Exception ex )
            {
                Log.Exception( "app exception", ex );
            }
        }

    }
}