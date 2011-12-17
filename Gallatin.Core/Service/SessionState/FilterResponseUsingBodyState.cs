using System.ComponentModel.Composition;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState(SessionStateType = SessionStateType.ResponseBodyFilter)]
    internal class FilterResponseUsingBodyState : SessionStateBase
    {
        [Import]
        public IProxyFilter Filter { get; set; }

        public override void TransitionToState(ISessionContext context)
        {
            context.HttpResponseBodyRequested(ServerParserBodyAvailable);
        }

        void ServerParserBodyAvailable(byte[] data, ISessionContext context)
        {
            byte[] filter = Filter.EvaluateResponseFiltersWithBody(context.RecentResponseHeader,
                                                                   context.ClientConnection.ConnectionId,
                                                                   data);

            // Wait until now to send the header in case the filter modifies it, such as updating content-length.
            context.SendClientData(context.RecentResponseHeader.GetBuffer());

            // The body changed. Send the modified body and not the original body. Disconnect afterwards.
            if (filter != data)
            {
                ServiceLog.Logger.Info("{0} *** PERFORMANCE HIT *** Response filter activated. Body modified.", context.Id);

                if (filter.Length > 0)
                {
                    context.SendClientData(filter);
                    context.ChangeState(SessionStateType.Unconnected);
                }
            }
            else
            {
                // Change back to the normal connnection state
                context.ChangeState(SessionStateType.Connected);
                context.SendClientData(data);
            }
        }

        public override bool ShouldSendPartialClientData(byte[] data, ISessionContext context)
        {
            // Don't sent partial data. We'll send the complete body when it's available
            return false;
        }
    }
}