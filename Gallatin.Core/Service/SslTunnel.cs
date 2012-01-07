using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;

namespace Gallatin.Core.Service
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export( typeof (ISslTunnel) )]
    internal class SslTunnel : ISslTunnel
    {
        private INetworkFacade _client;
        private string _httpVersion;
        private INetworkFacade _server;

        #region ISslTunnel Members

        public event EventHandler TunnelClosed;

        public void EstablishTunnel(INetworkFacade client, INetworkFacade server, string httpVersion)
        {
            if (_client != null)
            {
                throw new InvalidOperationException( "Tunnel already established" );
            }

            ServiceLog.Logger.Info( "Starting SSL connection" );

            _client = client;
            _server = server;
            _httpVersion = httpVersion;

            _client.BeginSend(
                Encoding.UTF8.GetBytes( string.Format(
                    "HTTP/{0} 200 Connection established\r\n" +
                    "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n",
                    _httpVersion ) ),
                HandleClientSend );

            _client.BeginReceive( HandleClientReceive );
        }

        #endregion

        private void OnTunnelClosed()
        {
            EventHandler tunnelClosed = TunnelClosed;
            if ( tunnelClosed != null )
            {
                tunnelClosed( this, new EventArgs() );
            }
        }

        private void HandleServerReceive( bool success, byte[] data, INetworkFacade server )
        {
            ServiceLog.Logger.Verbose("SSL - received data from server - forwarding to client");

            try
            {
                if (success)
                {
                    _client.BeginSend(data, HandleClientSend);
                }
                else
                {
                    OnTunnelClosed();
                    ServiceLog.Logger.Verbose("SSL: unable to receive data from server");
                }
            }
            catch
            {
                OnTunnelClosed();
            }
        }

        private void HandleClientReceive( bool success, byte[] data, INetworkFacade client )
        {
            ServiceLog.Logger.Verbose("SSL - received data from client - forwarding to server");

            try
            {
                if (success)
                {
                    _server.BeginSend(data, HandleServerSend);
                }
                else
                {
                    OnTunnelClosed();
                    ServiceLog.Logger.Verbose("SSL: unable to receive data from client");
                }
            }
            catch
            {
                OnTunnelClosed();
            }
        }

        private void HandleClientSend( bool succes, INetworkFacade client )
        {
            ServiceLog.Logger.Verbose("SSL - sending data to client - accepting more data from server");

            try
            {
                if (succes)
                {
                    _server.BeginReceive(HandleServerReceive);
                }
                else
                {
                    OnTunnelClosed();
                    ServiceLog.Logger.Verbose("SSL: unable to send data to client");
                }
            }
            catch
            {
                OnTunnelClosed();
            }

        }

        private void HandleServerSend( bool succes, INetworkFacade server )
        {
            ServiceLog.Logger.Verbose("SSL - sending data to server - accepting more data from client");

            try
            {
                if (succes)
                {
                    _client.BeginReceive(HandleClientReceive);
                }
                else
                {
                    OnTunnelClosed();
                    ServiceLog.Logger.Verbose("SSL: unable to send data to server");
                }
            }
            catch
            {
                OnTunnelClosed();
            }
        }
    }
}