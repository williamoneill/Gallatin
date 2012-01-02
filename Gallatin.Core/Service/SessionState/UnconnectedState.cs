namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState(SessionStateType = SessionStateType.Uninitialized)]
    internal class UninitializedState : SessionStateBase
    {
        public override bool ShouldSendPartialDataToClient(byte[] data, ISessionContext context)
        {
            return false;
        }

        public override bool ShouldSendPartialDataToServer(byte[] data, ISessionContext context)
        {
            return false;
        }
    }

    [ExportSessionState(SessionStateType = SessionStateType.Unconnected)]
    internal class UnconnectedState : SessionStateBase
    {
        public override void TransitionToState( ISessionContext context )
        {
            context.Reset();
        }

        public override bool ShouldSendPartialDataToClient( byte[] data, ISessionContext context )
        {
            return false;
        }

        public override bool ShouldSendPartialDataToServer( byte[] data, ISessionContext context )
        {
            return false;
        }
    }
}