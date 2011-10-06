using System;
using System.Text;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class EstablishedSslConnectionState : ProxyClientStateBase
    {
        private readonly string _host;
        private readonly int _port;
        private IHttpRequestMessage _requestMessage;

        public EstablishedSslConnectionState( ProxyClient proxyClient, IHttpRequestMessage requestMessage )
            : base( proxyClient )
        {
            const int HOST_PORT_TOKEN_COUNT = 2;

            Log.Info( Encoding.UTF8.GetString( requestMessage.CreateHttpMessage()) );

            _requestMessage = requestMessage;

            Log.Info( "Connecting to remote host via SSL" );

            _host = requestMessage.Destination.Host;
            _port = requestMessage.Destination.Port;

            // Not the standard port 80? Probably not if we are SSL.
            if ( requestMessage.Destination.Port == -1 )
            {
                string[] tokens = requestMessage.Destination.AbsoluteUri.Split( ':' );
                if ( tokens.Length == HOST_PORT_TOKEN_COUNT )
                {
                    _host = tokens[0];
                    _port = int.Parse( tokens[1] );

                }

                const int HTTPS_PORT = 443;
                const int SNEWS_PORT = 563;

                if(_port != HTTPS_PORT || _port != SNEWS_PORT)
                {
                    // TODO: too much is going on in the constructor
                    throw new ArgumentException( "Unrecognized port for secure connection" );
                }
            }

        }

        public void StartSslSession()
        {
            ProxyClient.NetworkService.SendServerMessage(ProxyClient,
                                                          null,
                                                          _host,
                                                          _port);

            ProxyClient.NetworkService.SendClientMessage(
                ProxyClient,
                Encoding.UTF8.GetBytes( string.Format(
                    "HTTP/{0} 200 Connection Established\r\nProxy-agent: Gallatin-Proxy\r\n\r\n", _requestMessage.Version)));

            
        }

        public override void HandleSendComplete( INetworkService networkService )
        {
        }

        public override void HandleNewDataAvailableFromClient( INetworkService networkService,
                                                               byte[] data )
        {
            ProxyClient.NetworkService.SendServerMessage( ProxyClient, data, _host, _port );
        }

        public override void HandleNewDataAvailableFromServer( INetworkService networkService,
                                                               byte[] data )
        {
            ProxyClient.NetworkService.SendClientMessage( ProxyClient, data );
        }
    }

    internal class ReceiveRequestFromClientState : ProxyClientStateBase
    {
        private readonly HttpMessageParser _parser = new HttpMessageParser();

        public ReceiveRequestFromClientState( ProxyClient proxyClient )
            : base( proxyClient )
        {
            Log.Info( "Transitioning to ReceiveRequestFromClientState" );

            ProxyClient.NetworkService.GetDataFromClient( ProxyClient );
        }

        public override void HandleNewDataAvailableFromClient( INetworkService networkService,
                                                               byte[] data )
        {
            IHttpMessage message = _parser.AppendData( data );

            if ( message != null )
            {
                IHttpRequestMessage requestMessage = message as IHttpRequestMessage;

                if ( requestMessage != null )
                {
                    // SSL
                    if (requestMessage.Method.Equals("Connect",
                                                       StringComparison.InvariantCultureIgnoreCase))
                    {
                        Log.Info("Establishing SSL connection");

                        var ssl = new EstablishedSslConnectionState(ProxyClient,
                                                                               requestMessage);

                        ProxyClient.State = ssl;

                        ssl.StartSslSession();

                    }
                    else
                    {
                        ProxyClient.State = new SendDataToRemoteHostState(ProxyClient,
                                                                           requestMessage);
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        "Did not receive an HTTP request while awaiting request from client" );
                }
            }
            else
            {
                ProxyClient.NetworkService.GetDataFromClient( ProxyClient );
            }
        }
    }
}