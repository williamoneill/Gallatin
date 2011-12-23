using System;
using Gallatin.Contracts;

namespace Gallatin.Core.Service.SessionState
{
    internal abstract class SessionStateBase : ISessionState
    {
        #region ISessionState Members

        public virtual void TransitionToState( ISessionContext context )
        {
        }

        public virtual void TransitionFromState( ISessionContext context )
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
            throw new InvalidOperationException("Unable to send server response to client in current state");
        }

        public virtual void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
            throw new InvalidOperationException("Unable to accept HTTP request header in current state");
        }

        public virtual void ResponseHeaderAvailable( IHttpResponse response, ISessionContext context )
        {
            throw new InvalidOperationException("Unable to accept HTTP response header in current state");
        }

        public virtual void ServerConnectionLost( ISessionContext context )
        {
            context.Reset();
        }

        public virtual void ServerConnectionEstablished(ISessionContext context)
        {
            throw new InvalidOperationException("Unable to accept a new server connection in current state");
        }

        public virtual void AcknowledgeClientShutdown( ISessionContext context )
        {
            ServiceLog.Logger.Info("{0} SessionStateBase::AcknowledgeClientShutdown", context.Id);
            context.Reset();
        }

        public virtual void AcknowledgeServerShutdown( ISessionContext context )
        {
            ServiceLog.Logger.Info("{0} SessionStateBase::AcknowledgeServerShutdown", context.Id);
            context.Reset();
        }

        #endregion
    }
}