using System;
using System.Text;
using Gallatin.Core.Util;
using Gallatin.Core.Web;
using System.Threading;

namespace Gallatin.Core.Client
{
    internal class SslClientState : IProxyClientState
    {
        #region IProxyClientState Members

        public SslClientState()
        {
            Log.Info("Changing to SSL connection state");
        }

        //private bool _isFirstSendToServerComplete;


        public void ServerSendComplete( ProxyClient context )
        {
            // Notify the client that the send was complete
            //if (!_isFirstSendToServerComplete)
            //{
            //    _isFirstSendToServerComplete = true;

            //    // Verified remote host connection complete. Let client know it can continue to tunnel.
            //    Log.Info("Sending SSL connection success notification to client");
            //    context.NetworkService.SendClientMessage(context,
            //                                                Encoding.UTF8.GetBytes(
            //                                                    "HTTP/1.0 200 Connection established\r\n" +
            //                                                    "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n"));
            //}

            //else
            //{
            //    context.NetworkService.GetDataFromRemoteHost(context);
            //}
        }

        public void ClientSendComplete( ProxyClient context )
        {
        }

        public bool TryCompleteMessageFromServer(ProxyClient context, byte[] data)
        {
            context.NetworkService.SendClientMessage(context, data);

            return false;
        }

        public bool TryCompleteMessageFromClient(ProxyClient context, byte[] data)
        {   
            context.NetworkService.SendServerMessage(context, data, context.Host, context.Port);

            return false;
        }

        #endregion

        public void Initialize( ProxyClient context, IHttpRequestMessage requestMessage )
        {
            string[] tokens = requestMessage.Destination.AbsoluteUri.Split( ':' );
            if ( tokens.Length == 2 )
            {
                context.Host = tokens[0];
                context.Port = int.Parse( tokens[1] );
            }

            const int HTTPS_PORT = 443;
            const int SNEWS_PORT = 563;

            // Only allow SSL on well-known ports. This is the general guidance for HTTPS.
            if ( context.Port != HTTPS_PORT
                 && context.Port != SNEWS_PORT )
            {
                throw new ArgumentException(
                    string.Format( "Unrecognized port for secure connection: {0}", context.Port ) );
            }

            Log.Info("SSL--SENDING ACK TO CLIENT");

            context.NetworkService.SendClientMessage(context,
                                                        Encoding.UTF8.GetBytes( string.Format(
                                                            "HTTP/{0} 200 Connection established\r\n" +
                                                            "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n", 
                                                            requestMessage.Version)));


            // Connect to the remote server. Do not send data, only establish the connection.
            //Log.Info("Establishing SSL connection with remote host {0}:{1}",
            //          context.Host,
            //          context.Port);
            //context.NetworkService.SendServerMessage(context, null, context.Host, context.Port);

        }
    }
}