using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core;
using Gallatin.Core.Client;
using Gallatin.Core.Service;
using Gallatin.Core.Util;

namespace Runner
{
    class Program
    {
        

        static void Main(string[] args)
        {
            try
            {
                ProxyService server = new ProxyService( new ProxyClientFactory() );
                server.Start(8080);

                Console.WriteLine("Press any key to terminate");
                Console.ReadKey();
                
            }
            catch(Exception ex)
            {
                Log.Exception("app exception", ex);
            }

        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Exception("Unhandled exception", (Exception)e.ExceptionObject);
        }

    }
}
