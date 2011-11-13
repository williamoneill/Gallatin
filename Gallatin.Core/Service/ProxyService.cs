using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    [Export( typeof (IProxyService) )]
    internal class ProxyService : IProxyService
    {
        private readonly INetworkFacadeFactory _factory;
        private readonly object _mutex = new object();
        private readonly List<IProxySession> _sessions = new List<IProxySession>();

        private bool _isRunning;
        //private Pool<IProxySession> _sessionPool;

        [ImportingConstructor]
        internal ProxyService( INetworkFacadeFactory factory )
        {
            Contract.Requires( factory != null );

            _factory = factory;
        }

        #region IProxyService Members

        public void Start()
        {
            lock ( _mutex )
            {
                if ( _isRunning )
                {
                    throw new InvalidOperationException( "Service has already been started" );
                }

                //_sessionPool = new Pool<IProxySession>();
                //_sessionPool.Init( CoreSettings.Instance.MaxNumberClients, CoreFactory.Compose<IProxySession> );

                _factory.Listen( CoreSettings.Instance.NetworkAddressBindingOrdinal, CoreSettings.Instance.ServerPort, HandleClientConnected );
                _isRunning = true;
            }
        }

        public void Stop()
        {
            lock ( _mutex )
            {
                if ( !_isRunning )
                {
                    throw new InvalidOperationException( "Service has not been started" );
                }

                _factory.EndListen();
                //_sessionPool = null;
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
                //IProxySession session = _sessionPool.Get();

                IProxySession session = CoreFactory.Compose<IProxySession>();

                session.SessionEnded += HandleSessionEnded;

                session.Start( clientConnection );

                _sessions.Add( session );
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

           // _sessionPool.Put( proxySession );
        }
    }
}