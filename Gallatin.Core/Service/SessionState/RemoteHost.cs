using System.Diagnostics.Contracts;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.SessionState
{
    internal class RemoteHost
    {
        public RemoteHost( INetworkFacade networkFacade )
        {
            Contract.Requires(networkFacade!=null);

            Connection = networkFacade;
            Parser = new HttpStreamParser();
            HasDisconnected = false;
            HasStoppedSendingData = false;
        }

        public INetworkFacade Connection { get; set; }

        public IHttpStreamParser Parser { get; set; }

        public bool HasStoppedSendingData { get; set; }

        public bool HasDisconnected { get; set; }
    }
}