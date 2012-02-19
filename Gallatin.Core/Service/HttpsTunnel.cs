using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;
using Gallatin.Core.Service;

namespace Gallatin.Core.Net
{
    [Export(typeof(IHttpsTunnel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class HttpsTunnel : IHttpsTunnel
    {
        private INetworkConnectionFactory _factory;
        private INetworkConnection _client;
        private INetworkConnection _server;
        private string _httpVersion;

        [ImportingConstructor]
        public HttpsTunnel( INetworkConnectionFactory factory )
        {
            Contract.Requires(factory!=null);

            _factory = factory;
        }

        public void EstablishTunnel( string host, int port, string httpVersion, INetworkConnection client )
        {
            _client = client;
            _httpVersion = httpVersion;

            _factory.BeginConnect(host, port, HandleConnect );
        }

        private void OnTunnelClosed()
        {
            var ev = this.TunnelClosed;
            if(ev!=null)
                ev(this,new EventArgs());
        }

        private void HandleConnect(bool success, INetworkConnection server)
        {
            try
            {
                if (success)
                {
                    ServiceLog.Logger.Info("Established HTTPS tunnel");
                    
                    _server = server;

                    _server.DataAvailable += new EventHandler<DataAvailableEventArgs>(_server_DataAvailable);
                    _server.ConnectionClosed += new EventHandler(_client_ConnectionClosed);
                    _server.Shutdown += new EventHandler(_client_ConnectionClosed);
                    _server.Start();

                    _client.ConnectionClosed += new EventHandler(_client_ConnectionClosed);
                    _client.Shutdown += new EventHandler(_client_ConnectionClosed);
                    _client.DataAvailable += new EventHandler<DataAvailableEventArgs>(_client_DataAvailable );

                    _client.SendData( Encoding.UTF8.GetBytes( string.Format(
                        "HTTP/{0} 200 Connection established\r\n" +
                        "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n",
                        _httpVersion ) ) );


                }
                else
                {
                    ServiceLog.Logger.Warning("Unable to establish HTTPS tunnel");
                    OnTunnelClosed();
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception("Unhandled exception while trying to connect to HTTPS host", ex);
                OnTunnelClosed();
            }
        }

        void _client_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            ServiceLog.Logger.Verbose("HTTPS - Data available from the client");
            _server.SendData(e.Data);
        }

        void _client_ConnectionClosed(object sender, EventArgs e)
        {
            ServiceLog.Logger.Verbose("HTTPS - Connection closed");


            _server.DataAvailable -= new EventHandler<DataAvailableEventArgs>(_server_DataAvailable);
            _server.ConnectionClosed -= new EventHandler(_client_ConnectionClosed);
            _server.Shutdown -= new EventHandler(_client_ConnectionClosed);
            _server = null;

            _client.ConnectionClosed -= new EventHandler(_client_ConnectionClosed);
            _client.Shutdown -= new EventHandler(_client_ConnectionClosed);
            _client.DataAvailable -= new EventHandler<DataAvailableEventArgs>(_client_DataAvailable);

            OnTunnelClosed();
        }


        void _server_DataAvailable(object sender, DataAvailableEventArgs e)
        {
            ServiceLog.Logger.Verbose("HTTPS - Data available from the server");
            _client.SendData(e.Data);
        }

        public event EventHandler TunnelClosed;
    }
}