using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using Gallatin.Core.Net;
using Gallatin.Core.Service;
using Gallatin.Core.Util;

namespace Gallatin.Core.Net
{   
    [Export( typeof (IProxyService) )]
    internal class Service : IProxyService
    {
        private readonly INetworkConnectionFactory _factory;
        private readonly object _mutex = new object();

        private bool _isRunning;
        private Pool<ISession> _sessionPool;
        private ICoreSettings _settings;
        private IAccessLog _accessLog;

        [ImportingConstructor]
        internal Service(INetworkConnectionFactory factory, ICoreSettings settings, IAccessLog accessLog)
        {
            Contract.Requires( factory != null );
            Contract.Requires(settings!=null);
            Contract.Requires(accessLog!=null);

            _accessLog = accessLog;
            _factory = factory;
            _settings = settings;
        }

        #region IProxyService Members

        private Timer _timer;

        private List<ISession> _activeSessions = new List<ISession>();

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

                _accessLog.Start(new DirectoryInfo(".\\Access Logs"));
                _timer = new Timer(TimerCallback, null, 10000, 5000);

                _sessionPool = new Pool<ISession>();
                _sessionPool.Init(_settings.MaxNumberClients, CoreFactory.Compose<ISession>);
                
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
                _accessLog.Stop();
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

        private void HandleClientConnected( INetworkConnection clientConnection )
        {
            try
            {
                //ServiceLog.Logger.Info("ProxyService notified of new client connect. Pool size: {0}", _sessionPool.AvailablePoolSize);

                ISession session = _sessionPool.Get();

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
            Contract.Requires( sender is ISession );

            ISession proxySession = sender as ISession;
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