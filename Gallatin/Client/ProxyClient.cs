using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    
    internal interface IProxyClientState
    {
        void ServerSendComplete( ProxyClient context );
        void ClientSendComplete( ProxyClient context );
        void NewDataFromServer( ProxyClient context, byte[] data );
        void NewDataFromClient( ProxyClient context, byte[] data );
    }

    internal class SslClientState : IProxyClientState
    {
        public void Initialize( ProxyClient context, IHttpRequestMessage requestMessage  )
        {
            string[] tokens = requestMessage.Destination.AbsoluteUri.Split(':');
            if (tokens.Length == 2)
            {
                context.Host = tokens[0];
                context.Port = int.Parse(tokens[1]);
            }

            const int HTTPS_PORT = 443;
            const int SNEWS_PORT = 563;

            if (context.Port != HTTPS_PORT && context.Port != SNEWS_PORT)
            {
                throw new ArgumentException(string.Format("Unrecognized port for secure connection: {0}", context.Port));
            }

            // Connect to the remote server
            context.NetworkService.SendServerMessage(context, null, context.Host, context.Port);

            // Send ACK to client
            context.NetworkService.SendClientMessage(context,
                        Encoding.UTF8.GetBytes("HTTP/1.0 200 Connection established\r\nProxy-agent: Gallatin-Proxy/1.1\r\n\r\n"));
        }

        public void ServerSendComplete( ProxyClient context )
        {
            // Prepare to read more data from the server
            context.NetworkService.GetDataFromRemoteHost( context );
        }

        public void ClientSendComplete( ProxyClient context )
        {
            // Prepare to read more data from the client
            context.NetworkService.GetDataFromClient( context );
        }

        public void NewDataFromServer( ProxyClient context, byte[] data )
        {
            // Pass directly to the client
            context.NetworkService.SendClientMessage(context, data);
        }

        public void NewDataFromClient( ProxyClient context, byte[] data )
        {
            // Pass directly to the server
        }
    }

    internal class DisconnectedState : IProxyClientState
    {
        public void ServerSendComplete( ProxyClient context )
        {
            Log.Error("Proxy client notified of server send when the session was disconnected");
        }

        public void ClientSendComplete( ProxyClient context )
        {
            Log.Error("Proxy client notified of server send when the session was disconnecte");
        }

        public void NewDataFromServer( ProxyClient context, byte[] data )
        {
            Log.Error("Proxy client notified of new data from the server when the session was disconnecte");
        }

        public void NewDataFromClient( ProxyClient context, byte[] data )
        {
            Log.Error("Proxy client notified of new data from the client when the session was disconnecte");
        }
    }

    internal class DefaultClientState : IProxyClientState
    {
        private bool _isPersistent;
        private HttpMessageParser _clientMessageParser = new HttpMessageParser();
        private HttpMessageParser _serverMessageParser = new HttpMessageParser();

        public void ServerSendComplete(ProxyClient context)
        {
            // Prepare to receive the response from the remote host
            _serverMessageParser = new HttpMessageParser();
            context.NetworkService.GetDataFromRemoteHost(context);
        }

        public void ClientSendComplete( ProxyClient context )
        {
            if (_isPersistent)
            {
                // Reset for the next request
                _clientMessageParser = new HttpMessageParser();
                context.NetworkService.GetDataFromClient(context);
            }
            else
            {
                context.State = new DisconnectedState();
                context.NetworkService.EndClientSession(context);
            }
        }

        public void NewDataFromServer( ProxyClient context, byte[] data )
        {
            // Evaluate the server response
            IHttpMessage message = _serverMessageParser.AppendData(data);

            // Read entire message?
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
                    string connectionValue = responseMessage["connection"];

                    if (connectionValue != null
                            && !connectionValue.Equals("close", StringComparison.InvariantCultureIgnoreCase))
                    {
                        Log.Info("Maintaining client connection. The server connection value was: {0}", connectionValue);
                        _isPersistent = true;
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

                // Send the entire message to the client
                context.NetworkService.SendClientMessage(context, responseMessage.CreateHttpMessage());
            }
            else
            {
                // Get more data from the server to complete this message
                context.NetworkService.GetDataFromRemoteHost(context);
            }

            //// TODO: this can be more efficient. We are evaluating the same message twice...
            //IHttpMessage header;
            //if (_serverMessageParser.TryGetHeader(out header))
            //{
            //    // Check the header for streaming
            //    string contentType = header["content-type"];

            //    if (contentType != null)
            //    {
            //        // TODO: regex for multiple stream types
            //        string hv = contentType.ToLower();
            //        _isStreaming = (hv.Contains("video"));

            //        if (_isStreaming)
            //        {
            //            Log.Info("Switching to streaming mode. Proxy filter bypassed.");

            //            // Send everything we've read up to this point.
            //            // The proxy is not going to interpret stream data.
            //            NetworkService.SendClientMessage(this, _serverMessageParser.RawData);
            //        }
            //    }
            //}
            
        }

        public void NewDataFromClient( ProxyClient context, byte[] data )
        {
            IHttpMessage message = _clientMessageParser.AppendData(data);

            // Read full message?
            if (message != null)
            {
                IHttpRequestMessage requestMessage = message as IHttpRequestMessage;
                if (requestMessage == null)
                {
                    throw new InvalidCastException(
                        "Unable to create a HTTP request message from raw data");
                }

                context.Host = requestMessage.Destination.Host;
                context.Port = requestMessage.Destination.Port;

                // SSL (HTTPS)? Change state.
                if (context.Port == -1)
                {
                    var state = new SslClientState();
                    state.Initialize(context, requestMessage);
                    context.State = state;
                }
                else
                {
                    // Forward client request to the server
                    context.NetworkService.SendServerMessage(
                        context, requestMessage.CreateHttpMessage(), context.Host, context.Port);
                }
            }
            else
            {
                // Not enough data. Get more to complete the message.
                context.NetworkService.GetDataFromClient(context);
            }
        }
    }

    internal class ProxyClient : IProxyClient
    {
        internal string Host { get; set; }
        internal int Port { get; set; }

        public IProxyClientState State { get; set; }
        public INetworkService NetworkService { get; private set; }

        public ProxyClient()
        {
            State = new DisconnectedState();
        }

        #region IProxyClient Members

        public void ServerSendComplete()
        {
            State.ServerSendComplete(this);

            //if(_isSecure || _isStreaming)
            //{
            //    // SSL connect to the server? (no data sent)
            //    if(!_sentClientSslConnectNotify)
            //    {
            //        _sentClientSslConnectNotify = true;

            //        NetworkService.SendClientMessage(this,
            //            Encoding.UTF8.GetBytes("HTTP/1.0 200 Connection established\r\nProxy-agent: Gallatin-Proxy/1.1\r\n\r\n"));
            //    }

            //    // Get more data when streaming or SSL
            //    NetworkService.GetDataFromRemoteHost(this);
            //}
        }

        public void ClientSendComplete()
        {
            State.ClientSendComplete(this);

            //if(_isStreaming || _isSecure)
            //{
            //    NetworkService.GetDataFromClient(this);
            //}
            //else if (_isPersistent)
            //{
            //    // Reset for the next request
            //    _clientMessageParser = new HttpMessageParser();
            //    NetworkService.GetDataFromClient(this);
            //}
            //else
            //{
            //    NetworkService.EndClientSession(this);
            //}
        }

        public void NewDataAvailableFromServer( byte[] data )
        {
            State.NewDataFromServer(this, data);

            // Get out of the way if we are streaming content or have established a secure connection
            //if(_isStreaming || _isSecure)
            //{
            //    NetworkService.SendClientMessage(this, data);
            //}
            //else
            //{
            //    // Evaluate the server response
            //    IHttpMessage message = _serverMessageParser.AppendData(data);
                
            //    // Read entire message?
            //    if (message != null)
            //    {
            //        IHttpResponseMessage responseMessage = message as IHttpResponseMessage;
            //        if (responseMessage == null)
            //        {
            //            throw new InvalidCastException(
            //                "Unable to create a HTTP response message from raw data");
            //        }

            //        // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html#sec19.6.2
            //        // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec8.html#sec8.1
            //        // See RFC 8.1.3 - proxy servers must not establish a HTTP/1.1 persistent connection with 1.0 client. For
            //        // now, all 1.0 clients will not get persistent connections from the proxy.
            //        if (responseMessage.Version == "1.1")
            //        {
            //            string connectionValue = responseMessage["connection"];

            //            if (connectionValue != null
            //                    && !connectionValue.Equals("close", StringComparison.InvariantCultureIgnoreCase))
            //            {
            //                Log.Info("Maintaining client connection. The server connection value was: {0}", connectionValue);
            //            }
            //            else
            //            {
            //                _isPersistent = false;
            //            }
            //        }
            //        else
            //        {
            //            // Older or unrecognized HTTP version? Close.
            //            _isPersistent = false;
            //        }

            //        NetworkService.SendClientMessage(this, responseMessage.CreateHttpMessage());
            //    }

            //    // TODO: this can be more efficient. We are evaluating the same message twice...
            //    IHttpMessage header;
            //    if (_serverMessageParser.TryGetHeader(out header))
            //    {
            //        // Check the header for streaming
            //        string contentType = header["content-type"];

            //        if (contentType != null)
            //        {
            //            // TODO: regex for multiple stream types
            //            string hv = contentType.ToLower();
            //            _isStreaming = (hv.Contains("video"));

            //            if (_isStreaming)
            //            {
            //                Log.Info("Switching to streaming mode. Proxy filter bypassed.");

            //                // Send everything we've read up to this point.
            //                // The proxy is not going to interpret stream data.
            //                NetworkService.SendClientMessage(this, _serverMessageParser.RawData);
            //            }
            //        }
            //    }

            //        NetworkService.GetDataFromRemoteHost(this);
                    
            //}
        }


        public void NewDataAvailableFromClient( byte[] data )
        {
            State.NewDataFromClient(this, data);
        }

        public void StartSession( INetworkService networkService )
        {
            if (networkService == null)
                throw new ArgumentNullException( "networkService" );

            if (NetworkService != null)
                throw new InvalidOperationException( "Client session already started" );

            State = new DefaultClientState();
            NetworkService = networkService;
            NetworkService.GetDataFromClient(this);
        }

        #endregion
    }

}