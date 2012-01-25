using System;
using System.Diagnostics.Contracts;
using Gallatin.Core.Service;

namespace Gallatin.Core.Net
{
    internal class DefaultSessionLogger: ISessionLogger
    {
        public void Exception( string message, Exception ex )
        {
            ServiceLog.Logger.Exception(message,ex);
        }

        public void Error( string message )
        {
            ServiceLog.Logger.Error(message);
        }

        public void Info( string message )
        {
            ServiceLog.Logger.Info(message);
        }

        public void Verbose( string message )
        {
            ServiceLog.Logger.Verbose(message);
        }

        public void Verbose( Func<string> message )
        {
            ServiceLog.Logger.Verbose( message );
        }
    }

    internal class SessionLogger : ISessionLogger
    {
        private string _sessionId;

        public SessionLogger(string sessionId)
        {
            Contract.Ensures(!string.IsNullOrEmpty(sessionId));

            _sessionId = sessionId;
        }

        public void Exception(string message, Exception ex)
        {
            ServiceLog.Logger.Exception( string.Format("{0} {1}", _sessionId, message), ex );
        }

        public void Error(string message)
        {
            ServiceLog.Logger.Error("{0} {1}", _sessionId, message);
        }

        public void Info(string message)
        {
            ServiceLog.Logger.Info("{0} {1}", _sessionId, message);
        }

        public void Verbose(string message)
        {
            ServiceLog.Logger.Verbose("{0} {1}", _sessionId, message);
        }

        public void Verbose( Func<string> message )
        {
            ServiceLog.Logger.Verbose( () => string.Format("{0} {1}", _sessionId,  message()) );
        }
    }
}