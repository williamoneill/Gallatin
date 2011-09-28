using System;
using System.Diagnostics;
using System.Net;

namespace Gallatin.Core
{
    public class RemoteServerProxy : IRemoteServerProxy
    {
        private class RemoteProxyClientRecord
        {
            public HttpRequest HttpRequest { get; set; }
            public Action<WebResponse, ProxyClient> Callback { get; set; }
            public ProxyClient ProxyClient { get; set; }
            public WebRequest WebRequest { get; set; }
        }

        public void BeginSendRequest( HttpRequest request,
                                      Action<WebResponse, ProxyClient> handleWebResponse,
                                      ProxyClient state )
        {
            if ( request == null )
                throw new ArgumentNullException( "request" );

            if ( handleWebResponse == null )
                throw new ArgumentNullException( "handleWebResponse" );

            if ( state == null )
                throw new ArgumentNullException( "state" );

            WebRequest webRequest = WebRequest.Create( request.DestinationAddress );

            // TODO: populate webrequest

            RemoteProxyClientRecord record = new RemoteProxyClientRecord()
                                             {
                                                 Callback = handleWebResponse,
                                                 ProxyClient = state,
                                                 HttpRequest = request,
                                                 WebRequest = webRequest

                                             };


            webRequest.BeginGetResponse( HandleWebResponse, record );
        }

        private void HandleWebResponse( IAsyncResult ar )
        {
            RemoteProxyClientRecord remoteProxyClientRecord =
                ar.AsyncState as RemoteProxyClientRecord;

            Trace.Assert(remoteProxyClientRecord != null);

            try
            {

                WebResponse response = remoteProxyClientRecord.WebRequest.EndGetResponse(ar);

                remoteProxyClientRecord.Callback(response, remoteProxyClientRecord.ProxyClient);

            }
            catch ( Exception ex )
            {
                Trace.TraceError("Error in request from web server:  " + ex.Message);
                remoteProxyClientRecord.ProxyClient.ClientSocket.Close();
            }

        }
    }
}