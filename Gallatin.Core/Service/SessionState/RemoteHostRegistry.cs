using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;

namespace Gallatin.Core.Service.SessionState
{
    internal class RemoteHostRegistry
    {
        private readonly List<RemoteHost> _remoteHosts;

        public RemoteHostRegistry()
        {
            _remoteHosts = new List<RemoteHost>();
        }

        public IEnumerable<RemoteHost> RemoteHosts
        {
            get
            {
                return _remoteHosts.AsReadOnly();
            }
        }

        private RemoteHost _activeHost;

        ReaderWriterLockSlim _changingActiveHostLock = new ReaderWriterLockSlim();

        public RemoteHost ActiveHost
        {
            get
            {
                try
                {
                    _changingActiveHostLock.EnterReadLock();
                    return _activeHost;
                }
                finally
                {
                    _changingActiveHostLock.ExitReadLock();
                }
            }

            private set
            {
                try
                {
                    _changingActiveHostLock.EnterWriteLock();
                    _activeHost = value;
                }
                finally
                {
                    _changingActiveHostLock.ExitWriteLock();
                }             
            }
        }

        public void Add( RemoteHost remoteHost )
        {
            Contract.Ensures(ActiveHost == remoteHost);

            // Wire up all events



            ActiveHost = remoteHost;

            _remoteHosts.Add( remoteHost );
        }
    }
}