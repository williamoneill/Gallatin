using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState(SessionStateType = SessionStateType.Https)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class HttpsState : SessionStateBase
    {
        public override bool ShouldSendPartialClientData(byte[] data, ISessionContext context)
        {
            return false;
        }

        public override bool ShouldSendPartialServerData(byte[] data, ISessionContext context)
        {
            return false;
        }

        public override void ServerConnectionLost(ISessionContext context)
        {
            context.Reset();
        }

        private ISessionContext _context;
        private ISslTunnel _tunnel;

        public override void TransitionToState(ISessionContext context)
        {
            Contract.Requires(context.ClientConnection!=null);
            Contract.Requires(context.ServerConnection!=null);

            var client = context.ClientConnection;
            var server = context.ServerConnection;

            context.SetupServerConnection(null);
            context.SetupClientConnection(null);

            ServiceLog.Logger.Info("{0} Establishing HTTPS tunnel", context.Id);

            _context = context;
            _tunnel = CoreFactory.Compose<ISslTunnel>();
            _tunnel.TunnelClosed += new EventHandler(HandleTunnelClosed);
            _tunnel.EstablishTunnel(client, server, context.RecentRequestHeader.Version);
        }

        void HandleTunnelClosed(object sender, EventArgs e)
        {
            ServiceLog.Logger.Info("{0} HTTPS tunnel closed", _context.Id);

            _tunnel.TunnelClosed -= HandleTunnelClosed;
            _context.Reset();
        }
    }
}