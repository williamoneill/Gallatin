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
    internal class SslTunnel
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

        public event EventHandler TunnelClosed;

        private void OnTunnelClosed()
        {
            EventHandler tunnelClosed = TunnelClosed;
            if (tunnelClosed != null)
            {
                tunnelClosed(this, new EventArgs());
            }
        }

        private void HandleServerReceive(bool success, byte[] data, INetworkFacade server)
        {
            if(success)
            {
                _client.BeginSend(data, HandleClientSend);
            }
            else
            {
                OnTunnelClosed();
                ServiceLog.Logger.Verbose("SSL: unable to receive data from server");
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
                OnTunnelClosed();
                ServiceLog.Logger.Verbose("SSL: unable to receive data from client");
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
                OnTunnelClosed();
                ServiceLog.Logger.Verbose("SSL: unable to send data to client");
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
                OnTunnelClosed();
                ServiceLog.Logger.Verbose("SSL: unable to send data to server");
            }
        }

        public void EstablishTunnel()
        {
            ServiceLog.Logger.Info( "Starting SSL connection" );

            _client.BeginSend(
                Encoding.UTF8.GetBytes(string.Format(
                "HTTP/{0} 200 Connection established\r\n" +
                "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n",
                _httpVersion)), HandleClientSend);

            _client.BeginReceive(HandleClientReceive);

        }




    }
}
