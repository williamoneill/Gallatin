using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Service
{
    public class ProxyService : IProxyService
    {
        private ICoreSettings _settings;
        private INetworkFacadeFactory _factory;

        public ProxyService(ICoreSettings settings, INetworkFacadeFactory factory)
        {
            _settings = settings;
            _factory = factory;
        }

        public void Start()
        {
            _factory.Listen(_settings.NetworkAddressBindingOrdinal, _settings.ServerPort, HandleClientConnected);
        }

        private List<ProxySession> _sessions = new List<ProxySession>();

        private void HandleClientConnected(INetworkFacade clientConnection)
        {
            ProxySession session = new ProxySession(clientConnection, _factory);
            
            session.SessionEnded += new EventHandler(session_SessionEnded);

            session.Start();

            _sessions.Add(session);
        }

        void session_SessionEnded(object sender, EventArgs e)
        {
            ProxySession proxySession = sender as ProxySession;

            if (proxySession != null)
            {
                _sessions.Remove( proxySession );
            }
        }


        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
