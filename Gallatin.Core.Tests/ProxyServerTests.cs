using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using System.Threading;

namespace Gallatin.Core.Tests
{
    [TestFixture]
    public class ProxyServerTests
    {
        private class MockServer
        {
            public byte[] ResponseData;
            public byte[] ExpectedData;
            public Socket ServerSocket;
            public bool ShouldTerminate;

            public void Listen(int port, bool isPersistent)
            {
                ServerSocket = new Socket(AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp);

                IPAddress hostAddress =
                    (Dns.Resolve(IPAddress.Any.ToString())).AddressList[0];
                IPEndPoint endPoint = new IPEndPoint(hostAddress, port);

                ServerSocket.Bind(endPoint);

                ServerSocket.Listen(30);

                while (true)
                {
                    Socket client = ServerSocket.Accept();

                    do
                    {
                        byte[] dataBuffer = new byte[ExpectedData.Length];
                        client.Receive( dataBuffer );

                        string expected = Encoding.UTF8.GetString( ExpectedData );
                        string received = Encoding.UTF8.GetString( dataBuffer );

                        Assert.That(expected, Is.EqualTo(received));

                        client.Send( ResponseData );
                    }
                    while ( isPersistent && !ShouldTerminate );

                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
            }
        }

        //[Test]
        //public void BasicSendTest()
        //{
        //    // Mock server setup
        //    MockServer server = new MockServer();
        //    server.ResponseData = Encoding.UTF8.GetBytes( "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n" );

        //    ThreadPool.QueueUserWorkItem( (state)=> server.Listen(80, false) );
        //    server.ExpectedData =
        //        Encoding.UTF8.GetBytes( "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n" );

        //    // Proxy server setup
        //    ProxyServerOld proxyServer = new ProxyServerOld();
        //    proxyServer.Start(2222);
        //    proxyServer.ClientMessagePosted += new EventHandler<ClientRequestArgs>(proxyServer_ClientMessagePosted);
        //    proxyServer.ServerResponsePosted += new EventHandler<ServerResponseArgs>(proxyServer_ServerResponsePosted);

        //    // Send request from client
        //    TcpClient client = new TcpClient();
        //    client.Connect("localhost", 2222);
        //    var stream = client.GetStream();
        //    var data = server.ExpectedData;
        //    stream.Write(data, 0, data.Length);

        //    byte[] dataFromServer = new byte[server.ResponseData.Length];
        //    stream.Read( dataFromServer, 0, dataFromServer.Length );

        //    Assert.That(dataFromServer, Is.EqualTo(server.ResponseData));
            
        //    proxyServer.Stop();

        //    server.ShouldTerminate = true;
        //}

        //void proxyServer_ServerResponsePosted(object sender, ServerResponseArgs e)
        //{
        //    INetworkMessageService networkMessageService = sender as INetworkMessageService;
        //    networkMessageService.SendClientMessage(e.ResponseMessage, e.ClientSession);
        //}

        //void proxyServer_ClientMessagePosted(object sender, ClientRequestArgs e)
        //{
        //    INetworkMessageService networkMessageService = sender as INetworkMessageService;
        //    networkMessageService.SendServerMessage( e.RequestMessage, e.ClientSession );
        //}

        //[Test]
        //public void HttpsTest()
        //{
        //    ProxyServerOld proxyServer = new ProxyServerOld();
        //    proxyServer.Start(2222);
        //    proxyServer.ClientMessagePosted += new EventHandler<ClientRequestArgs>(proxyServer_ClientMessagePosted);
        //    proxyServer.ServerResponsePosted += new EventHandler<ServerResponseArgs>(proxyServer_ServerResponsePosted);


        //    var request =
        //        WebRequest.Create(
        //            "http://www.gmail.com" );
        //    request.Proxy = new WebProxy( "127.0.0.1", 2222 );

        //    var response = request.GetResponse() as HttpWebResponse;

        //    var stream = response.GetResponseStream();

        //    FileStream fs = new FileStream("c:\\temp\\pb2.txt", FileMode.Create);

        //    stream.CopyTo(fs);

        //    fs.Close();

        //    proxyServer.Stop();

        //    // http://l.yimg.com/d/lib/can_interstitial/icons/adchoice_1.4.png
        //}

        //[Test]
        //public void YahooTest()
        //{
        //    ProxyServerOld proxyServer = new ProxyServerOld();
        //    proxyServer.Start(2222);
        //    proxyServer.ClientMessagePosted += new EventHandler<ClientRequestArgs>(proxyServer_ClientMessagePosted);
        //    proxyServer.ServerResponsePosted += new EventHandler<ServerResponseArgs>(proxyServer_ServerResponsePosted);


        //    var request =
        //        WebRequest.Create(
        //            "http://l.yimg.com/a/combo?metro/g/core_yui_3.3.0.css&metro/g/core_srvc_1.0.5.css&metro/g/core_mod_1.0.78.css&metro/g/fp/fp_0.1.120.css&metro/g/masthead/masthead_0.2.129.css&metro/g/navbar/navbar_0.1.134.css&metro/g/navbar/navbar_pageoptions_0.0.48.css&metro2/g/announcebar/announcebar_1.0.18.css&metro/g/footer/footer_0.1.78.css&metro/g/footer/subfooter_0.0.14.css&metro/g/news/offlead_0.1.10.css&metro/g/news/news_0.1.151.css&metro/g/fptoday/fptoday_0.1.175.css&metro/g/contentcarousel/contentcarousel_news_0.0.9.css&metro/g/pa/pa_0.1.202.css&metro/g/pa/pa_detached_0.1.90.css&metro/g/pa/pa_add_0.1.67.css");
        //    request.Proxy = new WebProxy("127.0.0.1", 2222);

        //    var response = request.GetResponse() as HttpWebResponse;

        //    var stream = response.GetResponseStream();

        //    FileStream fs = new FileStream("c:\\temp\\pb2.txt", FileMode.Create);

        //    stream.CopyTo(fs);

        //    fs.Close();

        //    proxyServer.Stop();

        //}

    }
}
