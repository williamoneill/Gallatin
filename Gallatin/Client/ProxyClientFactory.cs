

namespace Gallatin.Core.Client
{
    public class ProxyClientFactory : IProxyClientFactory
    {
        #region IProxyClientFactory Members

        public IProxyClient CreateClient()
        {
            return new ProxyClient();
        }

        #endregion
    }
}