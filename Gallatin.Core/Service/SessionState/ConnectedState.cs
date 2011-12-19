using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;
using Gallatin.Contracts;

namespace Gallatin.Core.Service.SessionState
{

    [ExportSessionState(SessionStateType = SessionStateType.Error)]
    internal class ErrorState : SessionStateBase
    {
        public override void TransitionToState(ISessionContext context)
        {
            if (context.ClientConnection != null)
            {
                context.UnwireClientParserEvents();
                context.SendClientData(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length: 11\r\n\r\nProxy error"));
                context.Reset();
            }
        }
    }

    [ExportSessionState( SessionStateType = SessionStateType.Connected )]
    internal class ConnectedState : SessionStateBase
    {
        private readonly IProxyFilter _filter;

        [ImportingConstructor]
        public ConnectedState( IProxyFilter filter )
        {
            Contract.Requires( filter != null );
            _filter = filter;
        }

        public override bool ShouldSendPartialDataToClient(byte[] data, ISessionContext context)
        {
            return true;
        }

        public override bool ShouldSendPartialDataToServer(byte[] data, ISessionContext context)
        {
            return true;
        }

        public override void TransitionToState( ISessionContext context )
        {
            // Send the initial request to the server. This was the request that was evaluated in the last
            // state to determine if we should connect to the server.
            context.SendServerData( context.RecentRequestHeader.GetBuffer() );
        }

        public override void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
            string host;
            int port;

            ServiceLog.Logger.Verbose(() => string.Format("{0}\r\n=====existing conn======\r\n{1}\r\n========================\r\n", context.Id, Encoding.UTF8.GetString(request.GetBuffer())));

            if ( SessionStateUtils.TryParseAddress( request, out host, out port ) )
            {
                if ( host != context.Host
                     || port != context.Port )
                {
                    ServiceLog.Logger.Info( "{0} Client changed host/port on existing connection", context.Id );

                    context.ChangeState( SessionStateType.ClientConnecting );

                    // Pass the header we used to determine the connection should be closed
                    context.State.RequestHeaderAvailable( request, context );
                }
                else
                {
                    context.SendServerData( request.GetBuffer() );
                }
            }
            else
            {
                ServiceLog.Logger.Error("{0} Unable to parse host address: {1}", context.Id, Encoding.UTF8.GetString(request.GetBuffer()));
                context.Reset();
            }
        }

        public override void ResponseHeaderAvailable(IHttpResponse response, ISessionContext context)
        {
            response.Headers.UpsertKeyValue("Content-Filter", "Gallatin Proxy");

            ServiceLog.Logger.Verbose(() => string.Format("{0}\r\n===RESPONSE=============\r\n{1}\r\n========================\r\n", context.Id, Encoding.UTF8.GetString(response.GetBuffer())));

            context.SendClientData(response.GetBuffer());

            //// Consult the response filters to see if any are interested in the entire body.
            //// Don't build the response body unless we have to; it's expensive.
            //// If any filters can make the judgement now, before we read the body, then use their response to
            //// short-circuit the body evaluation.
            //string filterResponse;
            //if (_filter.TryEvaluateResponseFilters(response,
            //                                         context.ClientConnection.ConnectionId,
            //                                         out filterResponse))
            //{
            //    // Filter active and does not need HTTP body
            //    if (filterResponse != null)
            //    {
            //        ServiceLog.Logger.Info("{0} *** PERFORMANCE HIT *** Response filter blocking content", context.Id);

            //        // Stop listening for more data from the server. We are creating our own response.
            //        // The session will terminate once this response is sent to the client.
            //        context.ChangeState(SessionStateType.ResponseHeaderFilter);
            //        context.SendClientData(Encoding.UTF8.GetBytes(filterResponse));
            //    }
            //    else
            //    {
            //        // Normal behavior. No filter activated.
            //        context.SendClientData(response.GetBuffer());
            //    }
            //}
            //else
            //{
            //    // Prepare to receive the entire HTTP body
            //    ServiceLog.Logger.Info("{0} *** PERFORMANCE HIT *** Response filter requires entire body. Building HTTP body.", context.Id);
            //    context.ChangeState(SessionStateType.ResponseBodyFilter);
            //}
        }


        public override void SentFullServerResponseToClient( IHttpResponse response, ISessionContext context )
        {
            ServiceLog.Logger.Info("{0} Evaluating persistent connection...", context.Id);

            if ( !response.IsPersistent )
            {
                ServiceLog.Logger.Info("{0} Non-persistent connection. Closing session.", context.Id);
                context.ChangeState( SessionStateType.Unconnected );
            }
            else
            {
                ServiceLog.Logger.Info("{0} Maintaining persistent connection.", context.Id);
            }
        }
    }
}