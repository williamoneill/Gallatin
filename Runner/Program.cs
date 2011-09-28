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
            ProxyServer server = new ProxyServer();
            server.ClientMessagePosted += new EventHandler<ClientRequestArgs>(server_ClientMessagePosted);
            server.ServerResponsePosted += new EventHandler<ServerResponseArgs>(server_ServerResponsePosted);
            server.Start( 8080 );

            Console.WriteLine("Press any key to terminate");
            Console.ReadKey();

            server.Stop();
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
