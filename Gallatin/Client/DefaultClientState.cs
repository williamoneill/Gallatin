using System;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Client
{
    internal class DefaultClientState : IProxyClientState
    {
        private HttpMessageParser _clientMessageParser = new HttpMessageParser();
        private bool _isPersistent;
        private HttpMessageParser _serverMessageParser = new HttpMessageParser();

        #region IProxyClientState Members

        public DefaultClientState()
        {
            Log.Info("Changing to default client state");
        }

        public void ServerSendComplete( ProxyClient context )
        {
            // Prepare to receive the response from the remote host
            _serverMessageParser = new HttpMessageParser();
            context.NetworkService.GetDataFromRemoteHost( context );
        }

        public void ClientSendComplete( ProxyClient context )
        {
            if ( _isPersistent )
            {
                // Reset for the next request
                _clientMessageParser = new HttpMessageParser();
                context.NetworkService.GetDataFromClient( context );
            }
            else
            {
                context.State = new DisconnectedState();
                context.NetworkService.EndClientSession( context );
            }
        }

        public void NewDataFromServer( ProxyClient context, byte[] data )
        {
            // Evaluate the server response
            IHttpMessage message = _serverMessageParser.AppendData( data );

            // Read entire message?
            if ( message != null )
            {
                IHttpResponseMessage responseMessage = message as IHttpResponseMessage;
                if ( responseMessage == null )
                {
                    throw new InvalidCastException(
                        "Unable to create a HTTP response message from raw data" );
                }

                // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec19.html#sec19.6.2
                // See http://www.w3.org/Protocols/rfc2616/rfc2616-sec8.html#sec8.1
                // See RFC 8.1.3 - proxy servers must not establish a HTTP/1.1 persistent connection with 1.0 client. For
                // now, all 1.0 clients will not get persistent connections from the proxy.
                if ( responseMessage.Version == "1.1" )
                {
                    string connectionValue = responseMessage["connection"];

                    if ( connectionValue != null
                         &&
                         !connectionValue.Equals( "close",
                                                  StringComparison.InvariantCultureIgnoreCase ) )
                    {
                        Log.Info(
                            "Maintaining client connection. The server connection value was: {0}",
                            connectionValue );
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
                context.NetworkService.SendClientMessage( context,
                                                          responseMessage.CreateHttpMessage() );
            }
            else
            {
                // Get more data from the server to complete this message
                context.NetworkService.GetDataFromRemoteHost( context );
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
            IHttpMessage message = _clientMessageParser.AppendData( data );

            // Read full message?
            if ( message != null )
            {
                IHttpRequestMessage requestMessage = message as IHttpRequestMessage;
                if ( requestMessage == null )
                {
                    throw new InvalidCastException(
                        "Unable to create a HTTP request message from raw data" );
                }

                context.Host = requestMessage.Destination.Host;
                context.Port = requestMessage.Destination.Port;

                // SSL (HTTPS)? Change state.
                if ( context.Port == -1 && requestMessage.Method.Equals("CONNECT", StringComparison.InvariantCultureIgnoreCase))
                {
                    SslClientState state = new SslClientState();
                    state.Initialize( context, requestMessage );
                    context.State = state;
                }
                else
                {
                    // Forward client request to the server
                    context.NetworkService.SendServerMessage(
                        context, requestMessage.CreateHttpMessage(), context.Host, context.Port );
                }
            }
            else
            {
                // Not enough data. Get more to complete the message.
                context.NetworkService.GetDataFromClient( context );
            }
        }

        #endregion
    }
}