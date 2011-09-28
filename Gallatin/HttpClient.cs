using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace Gallatin.Core
{
    public class HttpClient : IHttpClient
    {
        private class _Context
        {
            public Action<HttpResponse, ProxyClient> Callback { get; set; }
            public Socket ServerSocket { get; set; }
            public HttpRequest Request { get; set; }
            public byte[] Buffer { get; set; }
            public MemoryStream ContentStream { get; set; }
            public static int BUFFER_SIZE = 60000;
            public ProxyClient Client { get; set; }
        }

        public void BeginWebRequest(HttpRequest clientRequest, Action<HttpResponse, ProxyClient> callback, ProxyClient client)
        {
            // TODO: verify parameters

            try
            {
                _Context context = new _Context();
                context.Callback = callback;
                context.Request = clientRequest;
                context.Client = client;

                Uri uri = new Uri(clientRequest.DestinationAddress);

                context.ServerSocket = new Socket(AddressFamily.InterNetwork,
                                                   SocketType.Stream,
                                                   ProtocolType.Tcp);

                context.ServerSocket.BeginConnect(uri.Host, uri.Port, HandleServerConnect, context);

            }
            catch ( Exception ex )
            {
                Trace.TraceError("Unable to connect to remote host. {0}", ex.Message);
                throw;
            }

        }

        private void HandleServerConnect(IAsyncResult ar)
        {
            try
            {
                _Context context = ar.AsyncState as _Context;

                Trace.Assert(context != null);

                context.ServerSocket.EndConnect(ar);

                byte[] buffer = new byte[context.Request.OriginalStream.Length];

                // Some day, this buffer may be completely reconstructed, but for now we
                // will just stream the original request from the client
                context.Request.OriginalStream.Position = 0;
                context.Request.OriginalStream.Read(buffer, 0, buffer.Length);

                context.ServerSocket.BeginSend(buffer,
                                                0,
                                                buffer.Length,
                                                SocketFlags.None,
                                                HandleSend,
                                                context);

            }
            catch ( Exception ex )
            {
                Trace.TraceError("Unable to send data to remote host. {0}", ex.Message);

                // TODO: communicate error with client
            }

        }

        private void HandleSend(IAsyncResult ar)
        {
            try
            {
                _Context context = ar.AsyncState as _Context;

                Trace.Assert(context != null);

                SocketError socketError;

                long bytesSent = context.ServerSocket.EndSend(ar, out socketError);

                if(socketError != SocketError.Success)
                    Trace.TraceError("Socket error encountered while sending data. {0}", socketError);
                // TODO: communicate failure to client

                Trace.Assert(bytesSent == context.Request.OriginalStream.Length);

                context.Buffer = new byte[_Context.BUFFER_SIZE];
                context.ContentStream = new MemoryStream();

                context.ServerSocket.BeginReceive(context.Buffer,
                                                   0,
                                                   context.Buffer.Length,
                                                   SocketFlags.None,
                                                   HandleReceive,
                                                   context);

            }
            catch ( Exception ex )
            {
                Trace.TraceError("Failed to send data to remote server. {0}", ex.Message);
                // TODO: communicate failure to client
            }
        }

        private void HandleReceive(IAsyncResult ar )
        {
            try
            {
                _Context context = ar.AsyncState as _Context;

                Trace.Assert(context != null);

                int bytesReceived = context.ServerSocket.EndReceive(ar);

                if (bytesReceived > 0)
                {
                    context.ContentStream.Write(context.Buffer, 0, bytesReceived);

                    HttpMessageOld message;

                    if (HttpContentParser.TryParse(context.ContentStream, out message))
                    {
                        HttpResponse response = message as HttpResponse;

                        context.ServerSocket.Close();

                        // Full response received from server
                        if (response != null)
                        {
                            context.Callback(response, context.Client);
                        }
                    }
                    else
                    {
                        context.ServerSocket.BeginReceive(context.Buffer,
                                                          0,
                                                          context.Buffer.Length,
                                                          SocketFlags.None,
                                                          HandleReceive,
                                                          context);
                    }
                }
                else
                {
                    Trace.Write("foo");
                }

            }
            catch ( Exception ex)
            {
                Trace.TraceError("Failed to receive data from remote host. {0}", ex.Message);
                // TODO: communicate failure to client
            }

        }
    }
}