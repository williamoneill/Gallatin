﻿using System;
using System.Reflection;
using Gallatin.Core;
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