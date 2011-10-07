using System;
using System.Collections.Generic;
using System.Linq;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class ProxyClient : IProxyClient
    {
        private readonly object _mutex = new object();
        private HttpMessageParser _clientMessageParser = new HttpMessageParser();
        private INetworkService _networkService;
        private HttpMessageParser _serverMessageParser = new HttpMessageParser();
        private bool _isPersistent = true;

        public ProxyClient()
        {
            _serverMessageParser = new HttpMessageParser();
            _clientMessageParser = new HttpMessageParser();
        }

        #region IProxyClient Members

        public void ServerSendComplete()
        {
            lock ( _mutex )
            {
                _serverMessageParser = new HttpMessageParser();
                _networkService.GetDataFromRemoteHost(this);
            }
        }

        public void ClientSendComplete()
        {
            lock ( _mutex )
            {
                _clientMessageParser = new HttpMessageParser();

                if (_isPersistent)
                {
                    _networkService.GetDataFromClient(this);
                }
                else
                {
                    _networkService.EndClientSession(this);
                }
            }
        }

        public void NewDataAvailableFromServer( byte[] data )
        {
            lock ( _mutex )
            {
                IHttpMessage message = _serverMessageParser.AppendData( data );
                if (message != null)
                {
                    IHttpResponseMessage responseMessage = message as IHttpResponseMessage;
                    if (responseMessage == null)
                    {
                        throw new InvalidCastException(
                            "Unable to create a HTTP response message from raw data");
                    }


                    // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html#sec19.6.2
                    // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec8.html#sec8.1
                    // See RFC 8.1.3 - proxy servers must not establish a HTTP/1.1 persistent connection with 1.0 client. For
                    // now, all 1.0 clients will not get persistent connections from the proxy.
                    if (responseMessage.Version == "1.1")
                    {
                        KeyValuePair<string, string> connectionHeader =
                            responseMessage.Headers.SingleOrDefault(
                                s =>
                                s.Key.Equals("Connection",
                                                StringComparison.
                                                    InvariantCultureIgnoreCase));

                        if (!connectionHeader.Equals(default(KeyValuePair<string, string>))
                             && !connectionHeader.Value.Equals("close", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Log.Info("Maintaining client connection. The server connection value was: {0}", connectionHeader.Value);
                        }
                        else
                        {
                            _isPersistent = false;
                        }
                    }
                    else
                    {
                        // Older or unrecognized HTTP version? Close.
                        _isPersistent = false;
                    }

                    _networkService.SendClientMessage(this, responseMessage.CreateHttpMessage());
                }
                else
                {
                    _networkService.GetDataFromRemoteHost(this);
                }
            }
        }

        public void NewDataAvailableFromClient( byte[] data )
        {
            lock (_mutex)
            {
                IHttpMessage message = _clientMessageParser.AppendData(data);
                if (message != null)
                {
                    IHttpRequestMessage requestMessage = message as IHttpRequestMessage;
                    if (requestMessage == null)
                    {
                        throw new InvalidCastException(
                            "Unable to create a HTTP request message from raw data");
                    }

                    string host = requestMessage.Destination.Host;
                    int port = requestMessage.Destination.Port;

                    // SSL?
                    if (port == -1)
                    {
                        string[] tokens = requestMessage.Destination.AbsoluteUri.Split(':');
                        if (tokens.Length == 2)
                        {
                            host = tokens[0];
                            port = int.Parse(tokens[1]);
                        }

                        const int HTTPS_PORT = 443;
                        const int SNEWS_PORT = 563;

                        if (port != HTTPS_PORT || port != SNEWS_PORT)
                        {
                            throw new ArgumentException("Unrecognized port for secure connection");
                        }
                    }

                    _networkService.SendServerMessage(this, requestMessage.CreateHttpMessage(), host, port);
                }
            }
        }

        public void StartSession( INetworkService networkService )
        {
            if (networkService == null)
                throw new ArgumentNullException( "networkService" );

            if (_networkService != null)
                throw new InvalidOperationException( "Client session already started" );

            _networkService = networkService;
            _networkService.GetDataFromClient(this);
        }

        #endregion
    }

}