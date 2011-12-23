using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState( SessionStateType = SessionStateType.Https )]
    [PartCreationPolicy( CreationPolicy.NonShared )]
    internal class HttpsState : SessionStateBase
    {
        //private ISessionContext _context;
        private ISslTunnel _tunnel;

        [ImportingConstructor]
        public HttpsState( INetworkFacadeFactory factory )
        {
            Contract.Requires( factory != null );

            ServiceLog.Logger.Info( "Creating new HTTPS state object" );
        }

        public override void ServerConnectionLost( ISessionContext context )
        {
            ServiceLog.Logger.Info( "{0} HTTPS connection lost", context.Id );
            context.Reset();
        }

        public override void ServerConnectionEstablished( ISessionContext context )
        {
            context.UnwireServerParserEvents();

            ServiceLog.Logger.Info("{0} HTTPS server connection established", context.Id);

            _tunnel = CoreFactory.Compose<ISslTunnel>();
            _tunnel.TunnelClosed += HandleTunnelClosed;
            _tunnel.EstablishTunnel( context.ClientConnection,
                                     context.ServerConnection,
                                     context.RecentRequestHeader.Version );
        }

        public override void TransitionToState( ISessionContext context )
        {
            Contract.Requires( context.ClientConnection != null );
            Contract.Requires( context.ServerConnection == null );

            context.SendClientData(Encoding.UTF8.GetBytes("HTTPS not supported"));
            context.Reset();

            //// TODO: is this required?
            //context.ClientConnection.CancelPendingReceive();
            //context.UnwireClientParserEvents();

            //ServiceLog.Logger.Info( "{0} Establishing HTTPS tunnel", context.Id );

            //string[] pathTokens = context.RecentRequestHeader.Path.Split( ':' );

            //if ( pathTokens.Length == 2 )
            //{
            //    context.ConnectToRemoteHost( pathTokens[0], Int32.Parse( pathTokens[1] ) );
            //}
            //else
            //{
            //    throw new InvalidDataException( "Unable to determine SSL host address" );
            //}

            //_context = context;
        }

        private void HandleTunnelClosed( object sender, EventArgs e )
        {
            //ServiceLog.Logger.Info( "{0} HTTPS tunnel closed", _context.Id );

            //_tunnel.TunnelClosed -= HandleTunnelClosed;
            //_context.Reset();
        }
    }
}