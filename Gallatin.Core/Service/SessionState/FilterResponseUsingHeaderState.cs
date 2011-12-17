using Gallatin.Contracts;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState( SessionStateType = SessionStateType.ResponseHeaderFilter )]
    internal class FilterResponseUsingHeaderState : SessionStateBase
    {
        public override bool ShouldSendPartialServerData( byte[] data, ISessionContext context )
        {
            return false;
        }

        public override void SentFullServerResponseToClient( IHttpResponse response, ISessionContext context )
        {
            context.ChangeState( SessionStateType.Unconnected );
        }
    }
}