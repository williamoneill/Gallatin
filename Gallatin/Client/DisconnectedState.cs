using Gallatin.Core.Util;

namespace Gallatin.Core.Client
{
    internal class DisconnectedState : IProxyClientState
    {
        #region IProxyClientState Members

        public DisconnectedState()
        {
            Log.Info("Changing to client disconnected state");
        }

        public void ServerSendComplete( ProxyClient context )
        {
            Log.Error( "Proxy client notified of server send when the session was disconnected" );
        }

        public void ClientSendComplete( ProxyClient context )
        {
            Log.Error( "Proxy client notified of server send when the session was disconnecte" );
        }

        public void NewDataFromServer( ProxyClient context, byte[] data )
        {
            Log.Error(
                "Proxy client notified of new data from the server when the session was disconnecte" );
        }

        public void NewDataFromClient( ProxyClient context, byte[] data )
        {
            Log.Error(
                "Proxy client notified of new data from the client when the session was disconnecte" );
        }

        #endregion
    }
}