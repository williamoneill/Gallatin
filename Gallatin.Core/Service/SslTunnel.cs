using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    public class SslTunnel
    {
        private INetworkFacade _client;
        private INetworkFacade _server;
        private string _httpVersion;

        public SslTunnel( INetworkFacade client, INetworkFacade server, string httpVersion )
        {
            Contract.Requires(client != null);
            Contract.Requires(server != null);
            Contract.Requires(!string.IsNullOrEmpty(httpVersion));

            _client = client;
            _server = server;
            _httpVersion = httpVersion;
        }

        private void HandleServerReceive(bool success, byte[] data, INetworkFacade server)
        {
            if(success)
            {
                _client.BeginSend(data, HandleClientSend);
            }
            else
            {
                Log.Error("SSL failure: unable to receive data from server");
            }
        }

        private void HandleClientReceive(bool success, byte[] data, INetworkFacade client)
        {
            if(success)
            {
                _server.BeginSend( data, HandleServerSend );
            }
            else
            {
                Log.Error("SSL failure: unable to receive data from client");
            }
        }

        private void HandleClientSend(bool succes, INetworkFacade client)
        {
            if(succes)
            {
                _server.BeginReceive(HandleServerReceive);   
            }
            else
            {
                Log.Error("SSL failure: unable to send data to client");
            }
        }

        private void HandleServerSend(bool succes, INetworkFacade server)
        {
            if(succes)
            {
                _client.BeginReceive(HandleClientReceive);
            }
            else
            {
                Log.Error("SSL failure: unable to send data to server");
            }
        }

        public void EstablishTunnel()
        {
            Log.Info( "Starting SSL connection" );

            _client.BeginReceive(HandleClientReceive);

            _client.BeginSend(
                Encoding.UTF8.GetBytes(string.Format(
                "HTTP/{0} 200 Connection established\r\n" +
                "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n",
                _httpVersion)), HandleClientSend);

        }



        //private void HandleServerSendComplete(IAsyncResult ar)
        //{
        //    try
        //    {
        //        TunnelSession tunnel = ar.AsyncState as TunnelSession;
        //        Trace.Assert(tunnel != null);

        //        SocketError socketError;
        //        int dataSent = _server.EndSend(ar, out socketError);

        //        if (dataSent > 0 && socketError == SocketError.Success)
        //        {
        //            TunnelSession receiveData = new TunnelSession(this);
        //            receiveData.Data = new byte[BufferSize];
        //            _client.BeginReceive(receiveData.Data,
        //                                        0,
        //                                        receiveData.Data.Length,
        //                                        SocketFlags.None,
        //                                        HandleNewDataFromClient,
        //                                        receiveData);
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Exception("{0} Error sending data to SSL host", ex);
        //    }

        //}

        //private void HandleNewDataFromClient(IAsyncResult ar)
        //{
        //    try
        //    {
        //        TunnelSession tunnel = ar.AsyncState as TunnelSession;
        //        Trace.Assert(tunnel != null);

        //        int dataReceived = _client.EndReceive(ar);

        //        if (dataReceived > 0)
        //        {
        //            _server.BeginSend(
        //                tunnel.Data, 0, dataReceived, SocketFlags.None, HandleServerSendComplete, tunnel);
        //        }

        //    }
        //    catch ( Exception ex )
        //    {
        //        Log.Exception("{0} Error receiving data from SSL client", ex);
        //    }
        //}


        //private void HandleNewDataFromServer(IAsyncResult ar)
        //{
        //    try
        //    {
        //        TunnelSession tunnel = ar.AsyncState as TunnelSession;
        //        Trace.Assert(tunnel != null);

        //        int dataReceived = _server.EndReceive(ar);

        //        if (dataReceived > 0)
        //        {
        //            _client.BeginSend(
        //                tunnel.Data, 0, dataReceived, SocketFlags.None, HandleClientSendComplete, tunnel);
        //        }

        //    }
        //    catch ( Exception ex )
        //    {
        //        Log.Exception("{0} Error receiving data from SSL host", ex);
        //    }

        //}


    }
}
