namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState( SessionStateType = SessionStateType.Unconnected )]
    internal class UnconnectedState : SessionStateBase
    {
        public override void TransitionToState( ISessionContext context )
        {
            // Don't fire events if this is the first transition to this state
            if ( context.ClientParser != null )
            {
                context.SetupClientConnection( null );
                context.SetupServerConnection( null );
                context.OnSessionEnded();
            }
        }

        public override bool ShouldSendPartialClientData( byte[] data, ISessionContext context )
        {
            return false;
        }

        public override bool ShouldSendPartialServerData( byte[] data, ISessionContext context )
        {
            return false;
        }
    }
}