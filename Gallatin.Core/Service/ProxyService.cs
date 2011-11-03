using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    [Export(typeof(IProxyService))]
    internal class ProxyService : IProxyService
    {
        private INetworkFacadeFactory _factory;
        private readonly List<IProxySession> _sessions = new List<IProxySession>();
        private readonly ICoreSettings _settings;

        [ImportingConstructor]
        internal ProxyService( ICoreSettings settings, INetworkFacadeFactory factory )
        {
            Contract.Requires(settings!=null);
            Contract.Requires(factory!=null);

            _settings = settings;
            _factory = factory;
        }

        #region IProxyService Members

        private bool _isRunning;
        private object _mutex = new object();
        //private Pool<IProxySession> _sessionPool = new Pool<IProxySession>(); 

        public void Start()
        {
            lock (_mutex)
            {
                if (_isRunning)
                {
                    throw new InvalidOperationException( "Service has already been started" );
                }

                _factory.Listen(_settings.NetworkAddressBindingOrdinal, _settings.ServerPort, HandleClientConnected);
                _isRunning = true;
            }
        }

        public void Stop()
        {
            lock (_mutex)
            {
                if (!_isRunning)
                {
                    throw new InvalidOperationException("Service has not been started");
                }

                _factory.EndListen();
                _isRunning = false;
            }
        }

        public int ActiveClients
        {
            get
            {
                return _sessions.Count;
            }
        }

        #endregion

        private void HandleClientConnected( INetworkFacade clientConnection )
        {
            // TODO: this is confirmed to be a performance issue. We should look at pooling again.
            IProxySession session = CoreFactory.Compose<IProxySession>();

            session.SessionEnded += HandleSessionEnded;

            session.Start(clientConnection);

            _sessions.Add( session );
        }

        private void HandleSessionEnded( object sender, EventArgs e )
        {
            IProxySession proxySession = sender as IProxySession;

            if ( proxySession != null )
            {
                _sessions.Remove( proxySession );
            }
        }
    }
}