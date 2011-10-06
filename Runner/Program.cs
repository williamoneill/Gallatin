using System;
using Gallatin.Core.Client;
using Gallatin.Core.Service;
using Gallatin.Core.Util;

namespace Runner
{
    internal class Program
    {
        private static void Main( string[] args )
        {
            try
            {
                ProxyService server = new ProxyService( new ProxyClientFactory() );
                server.Start( 8080 );

                Console.WriteLine( "Press any key to terminate" );
                Console.ReadKey();
            }
            catch ( Exception ex )
            {
                Log.Exception( "app exception", ex );
            }
        }

    }
}