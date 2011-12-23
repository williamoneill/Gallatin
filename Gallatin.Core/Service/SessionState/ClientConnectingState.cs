using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading;
using Gallatin.Contracts;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState( SessionStateType = SessionStateType.ClientConnecting )]
    internal class ClientConnectingState : SessionStateBase
    {
        private readonly IProxyFilter _filter;

        [ImportingConstructor]
        public ClientConnectingState(IProxyFilter filter)
        {
            Contract.Requires(filter != null);

            _filter = filter;
        }

        public override void AcknowledgeClientShutdown(ISessionContext context)
        {
            
        }

        public override void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
            Contract.Requires(context.ClientConnection != null);

            ServiceLog.Logger.Verbose(
                () => string.Format(
                    "{0}\r\n========================\r\n{1}\r\n========================\r\n", 
                    context.Id, Encoding.UTF8.GetString(request.GetBuffer())));

            //string filter = _filter.EvaluateConnectionFilters(request, context.ClientConnection.ConnectionId);

            //if (filter != null)
            //{
            //    ServiceLog.Logger.Info("{0} Connection filtered. Sending response to client.", context.Id);
            //    context.SendClientData(Encoding.UTF8.GetBytes(filter));
            //    context.ChangeState(SessionStateType.Unconnected);
            //}
            //else
            {
                if (request.IsSsl)
                {
                    context.ChangeState(SessionStateType.Https);
                }
                else
                {
                    string host;
                    int port;

                    if (SessionStateUtils.TryParseAddress(request, out host, out port))
                    {
                        ServiceLog.Logger.Info("{0} Attempting to connect to remote host: [{1}] [{2}]", context.Id, host, port);

                        context.BeginConnectToRemoteHost(host, port);
                    }
                    else
                    {
                        throw new InvalidDataException("Unable to parse host address from HTTP request");
                    }
                }

            }
        }

        public override void ServerConnectionLost(ISessionContext context)
        {
            // Ignore this and remain in the current state. This should only occur when we are switching hosts
            // on a persistent connection. We know the connection was lost and don't care.
        }

        public override void ServerConnectionEstablished(ISessionContext context)
        {
            context.ChangeState(SessionStateType.Connected);
            context.SendServerData(context.RecentRequestHeader.GetBuffer());
        }

    }
}