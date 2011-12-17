using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Gallatin.Contracts;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Inteface for client session state objects
    /// </summary>
    [ContractClass(typeof(SessionStateContract))]
    public interface ISessionState
    {
        /// <summary>
        /// Invoked whent the context is transitioned to that state
        /// </summary>
        /// <param name="context">State context</param>
        void TransitionToState( ISessionContext context );

        /// <summary>
        /// Invoked when the context is determining if it should send data to the client
        /// </summary>
        /// <param name="data">Proposed data to send</param>
        /// <param name="context">State context</param>
        /// <returns><c>true</c> if the data should be sent</returns>
        bool ShouldSendPartialClientData( byte[] data, ISessionContext context );

        /// <summary>
        /// Invoked when the context is determining if it should send data to the server
        /// </summary>
        /// <param name="data">Proposed data to send</param>
        /// <param name="context">State context</param>
        /// <returns><c>true</c> if the data should be sent</returns>
        bool ShouldSendPartialServerData( byte[] data, ISessionContext context );

        /// <summary>
        /// Invoked when a complete message has been sent to the client, useful in identifying persistent connections
        /// </summary>
        /// <param name="response">Original HTTP response from the server</param>
        /// <param name="context">State context</param>
        void SentFullServerResponseToClient( IHttpResponse response, ISessionContext context );

        /// <summary>
        /// Invoked whent the HTTP request header becomes available
        /// </summary>
        /// <param name="request">HTTP request header</param>
        /// <param name="context">State context</param>
        void RequestHeaderAvailable( IHttpRequest request, ISessionContext context );

        /// <summary>
        /// Invoked whent the HTTP response header becomes available
        /// </summary>
        /// <param name="response">HTTP response header</param>
        /// <param name="context">State context</param>
        void ResponseHeaderAvailable( IHttpResponse response, ISessionContext context );

        /// <summary>
        /// Informs the current state that the server context has been lost
        /// </summary>
        /// <param name="context">State context</param>
        void ServerConnectionLost( ISessionContext context );
    }

    [ContractClassFor(typeof(ISessionState))]
    internal abstract class SessionStateContract : ISessionState
    {
        public void TransitionToState( ISessionContext context )
        {
            Contract.Requires(context!=null);
        }

        public bool ShouldSendPartialClientData( byte[] data, ISessionContext context )
        {
            Contract.Requires(data != null);
            Contract.Requires(context != null);

            return false;
        }

        public bool ShouldSendPartialServerData( byte[] data, ISessionContext context )
        {
            Contract.Requires(data != null);
            Contract.Requires(context != null);

            return false;
        }

        public void SentFullServerResponseToClient( IHttpResponse response, ISessionContext context )
        {
            Contract.Requires(response != null);
            Contract.Requires(context!= null);
        }

        public void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
            Contract.Requires(request != null);
            Contract.Requires(context != null);
        }

        public void ResponseHeaderAvailable( IHttpResponse response, ISessionContext context )
        {
            Contract.Requires(response != null);
            Contract.Requires(context != null);
        }

        public void ServerConnectionLost( ISessionContext context )
        {
            Contract.Requires(context!= null);
        }
    }
}