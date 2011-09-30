using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core;

namespace Runner
{
    class Program
    {
        

        static void Main(string[] args)
        {
            try
            {
                ProxyServer server = new ProxyServer();
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

        static void server_ServerResponsePosted(object sender, ServerResponseArgs e)
        {
            INetworkMessageService networkMessageService = sender as INetworkMessageService;
            networkMessageService.SendClientMessage(e.ResponseMessage, e.ClientSession);
        }

        static void server_ClientMessagePosted(object sender, ClientRequestArgs e)
        {
            INetworkMessageService networkMessageService = sender as INetworkMessageService;
            networkMessageService.SendServerMessage(e.RequestMessage, e.ClientSession);
        }
    }
}
