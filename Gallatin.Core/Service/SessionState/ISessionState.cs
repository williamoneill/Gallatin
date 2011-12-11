using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Util;
using Gallatin.Core.Web;

namespace Gallatin.Core.Service.SessionState
{
    /// <summary>
    /// Inteface for client session state objects
    /// </summary>
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
        bool ShouldSendClientData( byte[] data, ISessionContext context );

        /// <summary>
        /// Invoked when the context is determining if it should send data to the server
        /// </summary>
        /// <param name="data">Proposed data to send</param>
        /// <param name="context">State context</param>
        /// <returns><c>true</c> if the data should be sent</returns>
        bool ShouldSendServerData( byte[] data, ISessionContext context );

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
    }

    internal abstract class SessionStateBase : ISessionState
    {
        #region ISessionState Members

        public virtual void TransitionToState( ISessionContext context )
        {
        }

        public virtual bool ShouldSendClientData( byte[] data, ISessionContext context )
        {
            return true;
        }

        public virtual bool ShouldSendServerData( byte[] data, ISessionContext context )
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

        #endregion
    }


    /// <summary>
    /// Session state export attribute used to identify specific states in MEF
    /// </summary>
    [MetadataAttribute]
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false )]
    public class ExportSessionStateAttribute : ExportAttribute
    {
        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        public ExportSessionStateAttribute() : base( typeof (ISessionState) )
        {
        }

        /// <summary>
        /// Gets and sets the session state type
        /// </summary>
        public SessionStateType SessionStateType { get; set; }
    }

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

        public override bool ShouldSendClientData( byte[] data, ISessionContext context )
        {
            return false;
        }

        public override bool ShouldSendServerData( byte[] data, ISessionContext context )
        {
            return false;
        }
    }

    internal static class SessionStateUtils
    {
        public static bool TryParseAddress( IHttpRequest e, out string host, out int port )
        {
            const int HttpPort = 80;

            host = e.Headers["Host"];
            port = HttpPort;

            if ( host == null )
            {
                return false;
            }

            // Get the port from the host address if it set
            string[] tokens = host.Split( ':' );
            if ( tokens.Length == 2 )
            {
                host = tokens[0];
                port = Int32.Parse( tokens[1] );
            }

            return true;
        }
    }

    [ExportSessionState( SessionStateType = SessionStateType.ClientConnecting )]
    internal class ClientConnectingState : SessionStateBase
    {
        [Import]
        public IProxyFilter ProxyFilter { get; set; }

        [Import]
        public INetworkFacadeFactory FacadeFactory { get; set; }

        public override void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
            string filter = ProxyFilter.EvaluateConnectionFilters( request, context.ClientConnection.ConnectionId );

            if ( filter != null )
            {
                context.SendClientData( Encoding.UTF8.GetBytes( filter ) );
                SessionContext.ChangeState( SessionStateType.Unconnected, context );
            }
            else
            {
                ConnectToServer( request, context );
            }
        }

        private void ConnectToServer( IHttpRequest request, ISessionContext context )
        {
            // With SSL (HTTPS) the path is the host name and port
            if ( request.IsSsl )
            {
                string[] pathTokens = request.Path.Split( ':' );

                if ( pathTokens.Length == 2 )
                {
                    context.Port = Int32.Parse( pathTokens[1] );
                    context.Host = pathTokens[0];
                }

                // TODO: transition to SSL state
            }
            else
            {
                string host;
                int port;

                if ( SessionStateUtils.TryParseAddress( request, out host, out port ) )
                {
                    context.Host = host;
                    context.Port = port;

                    FacadeFactory.BeginConnect( context.Host,
                                                context.Port,
                                                ( success, connection ) =>
                                                {
                                                    if ( success )
                                                    {
                                                        context.SetupServerConnection( connection );
                                                        SessionContext.ChangeState( SessionStateType.Connected, context );
                                                    }
                                                }
                        );
                }
            }
        }
    }

    [ExportSessionState( SessionStateType = SessionStateType.Connected )]
    internal class ConnectedState : SessionStateBase
    {
        private readonly IProxyFilter _filter;

        [ImportingConstructor]
        public ConnectedState( IProxyFilter filter )
        {
            Contract.Requires( filter != null );
            _filter = filter;
        }

        public override void TransitionToState( ISessionContext context )
        {
            // Send the initial request to the server
            context.SendServerData( context.LastRequestHeader.GetBuffer() );
        }

        public override void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
            string host;
            int port;

            if ( SessionStateUtils.TryParseAddress( request, out host, out port ) )
            {
                if ( host != context.Host
                     || port != context.Port )
                {
                    ServiceLog.Logger.Info( "{0} Client changed host/port on existing connection", context.Id );

                    // Close the current server connection
                    context.SetupClientConnection( null );

                    SessionContext.ChangeState( SessionStateType.ClientConnecting, context );
                    context.State.RequestHeaderAvailable( request, context );
                }
            }
        }

        public override void ResponseHeaderAvailable( IHttpResponse response, ISessionContext context )
        {
            // Consult the response filters to see if any are interested in the entire body.
            // Don't build the response body unless we have to; it's expensive.
            // If any filters can make the judgement now, before we read the body, then use their response to
            // short-circuit the body evaluation.
            string filterResponse;
            if ( _filter.TryEvaluateResponseFilters( response,
                                                     context.ClientConnection.ConnectionId,
                                                     out filterResponse ) )
            {
                // Filter active and does not need HTTP body
                if ( filterResponse != null )
                {
                    ServiceLog.Logger.Info( "{0} *** PERFORMANCE HIT *** Response filter blocking content", context.Id );

                    // Stop listening for more data from the server. We are creating our own response.
                    // The session will terminate once this response is sent to the client.
                    SessionContext.ChangeState( SessionStateType.ResponseHeaderFilter, context );
                    context.SendClientData( Encoding.UTF8.GetBytes( filterResponse ) );
                }
                else
                {
                    // Normal behavior. No filter activated.
                    context.SendClientData( response.GetBuffer() );
                }
            }
            else
            {
                // Prepare to receive the entire HTTP body
                ServiceLog.Logger.Info( "{0} *** PERFORMANCE HIT *** Response filter requires entire body. Building HTTP body.", context.Id );
                SessionContext.ChangeState(SessionStateType.ResponseBodyFilter, context);
            }
        }


        public override void SentFullServerResponseToClient( IHttpResponse response, ISessionContext context )
        {
            if ( !response.IsPersistent )
            {
                SessionContext.ChangeState( SessionStateType.Unconnected, context );
            }
        }
    }

    [ExportSessionState( SessionStateType = SessionStateType.ResponseHeaderFilter )]
    internal class FilterResponseUsingHeaderState : SessionStateBase
    {
        public override bool ShouldSendServerData( byte[] data, ISessionContext context )
        {
            return false;
        }

        public override void SentFullServerResponseToClient( IHttpResponse response, ISessionContext context )
        {
            SessionContext.ChangeState( SessionStateType.Unconnected, context );
        }
    }

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
            byte[] filter = Filter.EvaluateResponseFiltersWithBody(context.LastResponseHeader,
                                                                      context.ClientConnection.ConnectionId,
                                                                      data);

            // Wait until now to send the header in case the filter modifies it, such as updating content-length.
            context.SendClientData(context.LastResponseHeader.GetBuffer());

            // The body changed. Send the modified body and not the original body. Disconnect afterwards.
            if (filter != data)
            {
                ServiceLog.Logger.Info("{0} *** PERFORMANCE HIT *** Response filter activated. Body modified.", context.Id);

                if (filter.Length > 0)
                {
                    context.SendClientData(filter);
                    SessionContext.ChangeState(SessionStateType.Unconnected, context);
                }
            }
            else
            {
                // Change back to the normal connnection state
                SessionContext.ChangeState(SessionStateType.Connected, context);
                context.SendClientData(data);
            }
        }

        public override bool ShouldSendClientData(byte[] data, ISessionContext context)
        {
            // Don't sent partial data. We'll send the complete body when it's available
            return false;
        }
    }

    [ExportSessionState(SessionStateType = SessionStateType.Https)]
    internal class HttpsState : SessionStateBase
    {
        public override bool ShouldSendClientData(byte[] data, ISessionContext context)
        {
            return false;
        }

        public override bool ShouldSendServerData(byte[] data, ISessionContext context)
        {
            return false;
        }

        private ISessionContext _context;
        private ISslTunnel _tunnel;

        public override void TransitionToState(ISessionContext context)
        {
            _context = context;
            _tunnel = CoreFactory.Compose<ISslTunnel>();
            _tunnel.TunnelClosed += new EventHandler(HandleTunnelClosed);
            _tunnel.EstablishTunnel(context.ClientConnection, context.ServerConnection, context.LastRequestHeader.Version);
        }

        void HandleTunnelClosed(object sender, EventArgs e)
        {
            _tunnel.TunnelClosed -= HandleTunnelClosed;
            SessionContext.ChangeState(SessionStateType.Unconnected, _context);
        }
    }
}