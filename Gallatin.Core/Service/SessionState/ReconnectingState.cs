using System.Text;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState(SessionStateType = SessionStateType.Reconnecting)]
    internal class ReconnectingState : SessionStateBase
    {
        public override void TransitionToState(ISessionContext context)
        {
            context.CloseServerConnection();

            if (context.RecentRequestHeader.IsSsl)
            {
                context.ChangeState(SessionStateType.Https);
            }
            else
            {
                string host;
                int port;

                if (SessionStateUtils.TryParseAddress(context.RecentRequestHeader, out host, out port))
                {
                    context.BeginConnectToRemoteHost(host, port);
                }
                else
                {
                    ServiceLog.Logger.Error("{0} Unable to parse host address: {1}", context.Id, Encoding.UTF8.GetString(context.RecentRequestHeader.GetBuffer()));
                    context.Reset();
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
        }
    }
}