using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;
using Gallatin.Contracts;

namespace Gallatin.Core.Service.SessionState
{


    [ExportSessionState( SessionStateType = SessionStateType.Connected )]
    internal class ConnectedState : SessionStateBase
    {
        private List<ISessionContext>  _clientsPendingShutdown = new List<ISessionContext>();


        private readonly IProxyFilter _filter;

        [ImportingConstructor]
        public ConnectedState( IProxyFilter filter )
        {
            Contract.Requires( filter != null );
            _filter = filter;
        }

        public override void AcknowledgeClientShutdown(ISessionContext context)
        {
            ServiceLog.Logger.Verbose("{0} ACK client shutdown.", context.Id);

            // Shutdown the connection if there are no active transactions
            if (context.HttpPipelineDepth == 0)
            {
                context.Reset();
            }
            else
            {
                lock (_clientsPendingShutdown)
                {
                    _clientsPendingShutdown.Add(context);
                }
            }
        }

        public override void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
            string host;
            int port;

            ServiceLog.Logger.Verbose(
                () => string.Format(
                    "{0}\r\n=====existing conn======\r\n{1}\r\n========================\r\n", 
                    context.Id, Encoding.UTF8.GetString(request.GetBuffer())));

            if ( SessionStateUtils.TryParseAddress( request, out host, out port ) )
            {
                if ( host != context.Host
                     || port != context.Port )
                {
                    ServiceLog.Logger.Info( "{0} Client changed host/port on existing connection", context.Id );

                    // This will close the server connection and block new messages from the client
                    context.CloseServerConnection();

                    context.ChangeState( SessionStateType.ClientConnecting );

                    // Pass the request header to the new state so the connection filters can be evaluated
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

        public override void ServerConnectionEstablished(ISessionContext context)
        {
            throw new InvalidOperationException("Unable to accept a new server connection when already connected");
        }

        public override void SentFullServerResponseToClient( IHttpResponse response, ISessionContext context )
        {
            ServiceLog.Logger.Verbose("{0} Evaluating persistent connection. Pipeline: {1}  Client shutdown: {2}  ServerShutdown: {3}", context.Id, context.HttpPipelineDepth, context.HasClientBegunShutdown, context.HasServerBegunShutdown);

            if ( !response.IsPersistent || (context.HttpPipelineDepth == 0 && ( context.HasClientBegunShutdown || context.HasServerBegunShutdown ) ) )
            {
                lock (_clientsPendingShutdown)
                {
                    _clientsPendingShutdown.Remove( context );
                }

                ServiceLog.Logger.Info("{0} Non-persistent connection. Closing session.", context.Id);
                context.Reset();
            }
            else
            {
                ServiceLog.Logger.Info("{0} Maintaining persistent connection.", context.Id);
            }
        }
    }
}