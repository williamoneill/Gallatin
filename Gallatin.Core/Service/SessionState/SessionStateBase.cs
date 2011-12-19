using Gallatin.Contracts;

namespace Gallatin.Core.Service.SessionState
{
    internal abstract class SessionStateBase : ISessionState
    {
        #region ISessionState Members

        public virtual void TransitionToState( ISessionContext context )
        {
        }

        public virtual bool ShouldSendPartialDataToClient( byte[] data, ISessionContext context )
        {
            return true;
        }

        public virtual bool ShouldSendPartialDataToServer( byte[] data, ISessionContext context )
        {
            return true;
        }

        public virtual void SentFullServerResponseToClient( IHttpResponse response, ISessionContext context )
        {
        }

        public virtual void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
        }

        public virtual void ResponseHeaderAvailable( IHttpResponse response, ISessionContext context )
        {
        }

        public virtual void ServerConnectionLost( ISessionContext context )
        {
            context.ChangeState(SessionStateType.Unconnected);
        }

        #endregion
    }
}