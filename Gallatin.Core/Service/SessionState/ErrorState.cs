using System.Text;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState(SessionStateType = SessionStateType.Error)]
    internal class ErrorState : SessionStateBase
    {
        public override void TransitionToState(ISessionContext context)
        {
            context.UnwireClientParserEvents();

            if (context.ClientConnection != null)
            {
                context.SendClientData(Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-length: 11\r\n\r\nProxy error"));
            }

            context.Reset();
        }
    }
}