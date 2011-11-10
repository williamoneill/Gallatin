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
        private IPool<IProxySession> _sessionPool;

        public void Start()
        {
            lock (_mutex)
            {
                if (_isRunning)
                {
                    throw new InvalidOperationException( "Service has already been started" );
                }

                _factory.Listen(_settings.NetworkAddressBindingOrdinal, _settings.ServerPort, HandleClientConnected);
                //_sessionPool = CoreFactory.Compose<IPool<IProxySession>>();
                _sessionPool = new Pool<IProxySession>();
                _sessionPool.Init(_settings.MaxNumberClients, CoreFactory.Compose<IProxySession>);
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
                _sessionPool = null;
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
            try
            {
                IProxySession session = _sessionPool.Get();

                session.SessionEnded += HandleSessionEnded;

                session.Start(clientConnection);

                _sessions.Add(session);
            }
            catch ( InvalidOperationException ex )
            {
                ServiceLog.Logger.Exception("No more clients can be accepted; the pool of available sessions was exhausted. Increase the pool maximum number of clients in the proxy server settings.", ex);
            }
        }

        private void HandleSessionEnded( object sender, EventArgs e )
        {
            Contract.Requires(sender is IProxySession);

            IProxySession proxySession = sender as IProxySession;
            proxySession.SessionEnded -= HandleSessionEnded;

            _sessionPool.Put(proxySession);
        }
    }
}