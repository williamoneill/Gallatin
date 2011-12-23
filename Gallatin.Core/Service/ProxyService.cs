using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Threading;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    [Export( typeof (IProxyService) )]
    internal class ProxyService : IProxyService
    {
        private readonly INetworkFacadeFactory _factory;
        private readonly object _mutex = new object();

        private bool _isRunning;
        private Pool<IProxySession> _sessionPool;
        private ICoreSettings _settings;

        [ImportingConstructor]
        internal ProxyService( INetworkFacadeFactory factory, ICoreSettings settings )
        {
            Contract.Requires( factory != null );
            Contract.Requires(settings!=null);

            _factory = factory;
            _settings = settings;
        }

        #region IProxyService Members

        private Timer _timer;

        private List<IProxySession> _activeSessions = new List<IProxySession>();

        private void TimerCallback(object state)
        {
            ServiceLog.Logger.Verbose(() =>
                                      {
                                          lock (_activeSessions)
                                          {
                                              ServiceLog.Logger.Verbose("DUMPING ACTIVE SESSIONS");

                                              foreach (var session in _activeSessions)
                                              {
                                                  ServiceLog.Logger.Verbose("{0} is still active", session.Id);
                                              }
                                          }

                                          return "DUMP COMPLETE";
                                      });
        }

        public void Start()
        {
            ServiceLog.Logger.Info("Starting proxy service");

            lock ( _mutex )
            {
                if ( _isRunning )
                {
                    throw new InvalidOperationException( "Service has already been started" );
                }

                _timer = new Timer(TimerCallback, null, 10000, 5000);

                _sessionPool = new Pool<IProxySession>();
                _sessionPool.Init(_settings.MaxNumberClients, CoreFactory.Compose<IProxySession>);
                
                _factory.Listen( _settings.ListenAddress, _settings.ServerPort, HandleClientConnected );
                _isRunning = true;
            }
        }

        public void Stop()
        {
            ServiceLog.Logger.Info("Stopping proxy service");

            lock ( _mutex )
            {
                if ( !_isRunning )
                {
                    throw new InvalidOperationException( "Service has not been started" );
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
                return _sessionPool.AllocatedPoolSize;
            }
        }

        #endregion

        private void HandleClientConnected( INetworkFacade clientConnection )
        {
            try
            {
                //ServiceLog.Logger.Info("ProxyService notified of new client connect. Pool size: {0}", _sessionPool.AvailablePoolSize);

                IProxySession session = _sessionPool.Get();

                lock (_activeSessions)
                {
                    _activeSessions.Add(session);
                }

                session.SessionEnded += HandleSessionEnded;

                session.Start(clientConnection);
            }
            catch ( InvalidOperationException ex )
            {
                ServiceLog.Logger.Exception(
                    "No more clients can be accepted; the pool of available sessions was exhausted. Increase the pool maximum number of clients in the proxy server settings.",
                    ex );
            }
        }

        private void HandleSessionEnded( object sender, EventArgs e )
        {
            Contract.Requires( sender is IProxySession );

            IProxySession proxySession = sender as IProxySession;
            proxySession.SessionEnded -= HandleSessionEnded;

            lock (_activeSessions)
            {
                _activeSessions.Remove(proxySession);
            }

           _sessionPool.Put( proxySession );

           //ServiceLog.Logger.Info("ProxyService notified that session ended. Pool size: {0}.", _sessionPool.AvailablePoolSize);
        }
    }
}