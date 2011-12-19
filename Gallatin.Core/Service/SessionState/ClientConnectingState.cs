using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading;
using Gallatin.Contracts;

namespace Gallatin.Core.Service.SessionState
{
    [ExportSessionState( SessionStateType = SessionStateType.ClientConnecting )]
    internal class ClientConnectingState : SessionStateBase
    {
        private IProxyFilter _filter;
        private INetworkFacadeFactory _facadeFactory;

        Dictionary<ISessionContext,ManualResetEvent> _connectionBlocks = new Dictionary<ISessionContext, ManualResetEvent>();

        [ImportingConstructor]
        public ClientConnectingState(IProxyFilter filter, INetworkFacadeFactory factory)
        {
            Contract.Requires(filter != null);
            Contract.Requires(factory!=null);

            _filter = filter;
            _facadeFactory = factory;
        }

        public override bool ShouldSendPartialDataToClient(byte[] data, ISessionContext context)
        {
            throw new InvalidOperationException("An attempt was made to send partial data to server while unconnected");
        }

        public override bool ShouldSendPartialDataToServer(byte[] data, ISessionContext context)
        {
            Contract.Ensures(context.ServerConnection != null);

            // Wait for the server connect before sending any data to the server
            WaitForConnection(context);
            return true;
        }

        public override void TransitionToState(ISessionContext context)
        {
            // We can return to this state when the client changes hosts on a persistent connection. If we have
            // a server connection, close it.
            context.SetupServerConnection(null);
        }

        public override void ServerConnectionLost(ISessionContext context)
        {
            // Ignore this and remain in the current state. This should only occur when we are switching hosts
            // on a persistent connection. We know the connection was lost and don't care.
        }

        private void WaitForConnection(ISessionContext context)
        {
            ManualResetEvent connectionEvent = null;

            lock (_connectionBlocks)
            {
                if (_connectionBlocks.ContainsKey(context))
                {
                    connectionEvent = _connectionBlocks[context];
                }
            }

            // TODO: use timeout value
            if (connectionEvent != null)
            {
                ServiceLog.Logger.Verbose("{0} Waiting to send data to remote host. Connection pending.");

                if (!connectionEvent.WaitOne())
                {
                    throw new TimeoutException(string.Format("{0} Timed out waiting for connection", context.Id));
                }

                ServiceLog.Logger.Verbose("{0} Connection established. Sending initial data.");
            }
            else
            {
                throw new InvalidOperationException("Could not locate connection reset event for context");
            }
        }

        private void BlockSession(ISessionContext context)
        {
            ServiceLog.Logger.Info("{0} Blocking session while connecting to remote host");

            lock (_connectionBlocks)
            {
                if (_connectionBlocks.ContainsKey(context))
                {
                    _connectionBlocks.Remove(context);
                }

                _connectionBlocks.Add(context, new ManualResetEvent(false));
            }
        }

        private void UnblockSession(ISessionContext context)
        {
            ServiceLog.Logger.Info("{0} Connected to remote host. Unblocking session.");

            lock (_connectionBlocks)
            {
                ManualResetEvent connectionEvent;
                if (_connectionBlocks.TryGetValue(context, out connectionEvent))
                {
                    connectionEvent.Set();
                    _connectionBlocks.Remove( context );
                }
                else
                {
                    throw new InvalidOperationException("Could not locate connection reset event for context");
                }
            }
        }

        public override void RequestHeaderAvailable( IHttpRequest request, ISessionContext context )
        {
            Contract.Requires(context.ClientConnection != null);

            ServiceLog.Logger.Verbose(() => string.Format("{0}\r\n========================\r\n{1}\r\n========================\r\n", context.Id, Encoding.UTF8.GetString(request.GetBuffer())));

            BlockSession(context);

            string filter = _filter.EvaluateConnectionFilters(request, context.ClientConnection.ConnectionId);

            if (filter != null)
            {
                ServiceLog.Logger.Info("{0} Connection filtered. Sending response to client.", context.Id);
                context.SendClientData(Encoding.UTF8.GetBytes(filter));
                context.ChangeState(SessionStateType.Unconnected);
            }
            else
            {
                if (request.IsSsl)
                {
                    context.ChangeState(SessionStateType.Https);
                    context.State.RequestHeaderAvailable( request, context );
                }
                else
                {
                    ConnectToServer(request, context);
                }

            }
        }

        private void ConnectToServer( IHttpRequest request, ISessionContext context )
        {
            string host;
            int port;

            if ( SessionStateUtils.TryParseAddress( request, out host, out port ) )
            {
                context.Host = host;
                context.Port = port;
            }
            else
            {
                throw new InvalidDataException("Unable to parse host address from HTTP request");
            }

            ServiceLog.Logger.Info("{0} Attempting to connect to remote host: {1} {2}", context.Id, context.Host, context.Port);

            _facadeFactory.BeginConnect(context.Host,
                                        context.Port,
                                        (success, connection) =>
                                        {
                                            if (success)
                                            {
                                                ServiceLog.Logger.Info("{0} Connected to remote host: {1} {2}", context.Id, context.Host, context.Port);

                                                context.SetupServerConnection(connection);
                                                context.ChangeState(SessionStateType.Connected);

                                            }
                                            else
                                            {
                                                ServiceLog.Logger.Warning("{0} Unable to connect to remote host: {1} {2}", context.Id, context.Host, context.Port);
                                                context.ChangeState(SessionStateType.Error);
                                            }

                                            UnblockSession(context);

                                        }
                );

        }
    }
}