using System;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// This class is used to start the proxy server from another app domain
    /// </summary>
    public class CrossDomainProxyService : MarshalByRefObject, IProxyService
    {
        /// <summary>
        /// Starts the default proxy service
        /// </summary>
        public void Start()
        {
            var service = CoreFactory.Compose<IProxyService>();
            service.Start();
        }

        /// <summary>
        /// Stops the default proxy service
        /// </summary>
        public void Stop()
        {
            var service = CoreFactory.Compose<IProxyService>();
            service.Stop();
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        public int ActiveClients
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}