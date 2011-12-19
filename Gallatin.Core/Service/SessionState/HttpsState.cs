using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState(SessionStateType = SessionStateType.Https)]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    internal class HttpsState : SessionStateBase
    {
        private INetworkFacadeFactory _facadeFactory;

        [ImportingConstructor]
        public  HttpsState(INetworkFacadeFactory factory)
        {
            Contract.Requires(factory!=null);

            _facadeFactory = factory;
        }

        public override void ServerConnectionLost(ISessionContext context)
        {
            context.Reset();
        }

        public override bool ShouldSendPartialDataToClient(byte[] data, ISessionContext context)
        {
            return base.ShouldSendPartialDataToClient(data, context);
        }

        public override bool ShouldSendPartialDataToServer(byte[] data, ISessionContext context)
        {
            return base.ShouldSendPartialDataToServer(data, context);
        }

        private ISessionContext _context;
        private ISslTunnel _tunnel;

        public override void RequestHeaderAvailable(Contracts.IHttpRequest request, ISessionContext context)
        {
            string[] pathTokens = request.Path.Split(':');

            if (pathTokens.Length == 2)
            {
                context.Port = Int32.Parse(pathTokens[1]);
                context.Host = pathTokens[0];
            }
            else
            {
                throw new InvalidDataException("Unable to determine SSL host address");
            }

            _facadeFactory.BeginConnect( context.Host,
                                         context.Port,
                                         ( success, connection ) =>
                                         {
                                             if ( success )
                                             {
                                                 _tunnel = CoreFactory.Compose<ISslTunnel>();
                                                 _tunnel.TunnelClosed += new EventHandler( HandleTunnelClosed );
                                                 _tunnel.EstablishTunnel( context.ClientConnection,
                                                                          connection,
                                                                          context.RecentRequestHeader.Version );
                                             }
                                             else
                                             {
                                                 ServiceLog.Logger.Warning( "{0} Unable to connect to HTTPS server {1} {2}",
                                                                            context.Id,
                                                                            context.Host,
                                                                            context.Port );
                                                 context.Reset();
                                             }
                                         } );

        }

        public override void TransitionToState(ISessionContext context)
        {
            Contract.Requires(context.ClientConnection!=null);
            Contract.Requires(context.ServerConnection==null);

            context.ClientConnection.CancelPendingReceive();
            context.UnwireClientParserEvents();

            ServiceLog.Logger.Info("{0} Establishing HTTPS tunnel", context.Id);

            _context = context;
        }

        void HandleTunnelClosed(object sender, EventArgs e)
        {
            ServiceLog.Logger.Info("{0} HTTPS tunnel closed", _context.Id);

            _tunnel.TunnelClosed -= HandleTunnelClosed;
            _context.Reset();
        }
    }
}