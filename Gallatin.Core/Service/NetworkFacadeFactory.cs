using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;

namespace Gallatin.Core.Service
{
    [Export( typeof (INetworkFacadeFactory) )]
    internal class NetworkFacadeFactory : INetworkFacadeFactory
    {
        private Action<INetworkFacade> _clientConnectCallback;
        private Socket _socket;

        #region INetworkFacadeFactory Members

        public void BeginConnect( string host, int port, Action<bool, INetworkFacade> callback )
        {
            ConnectState state = new ConnectState
                                 {
                                     Callback = callback,
                                     Socket = new Socket( AddressFamily.InterNetwork,
                                                          SocketType.Stream,
                                                          ProtocolType.Tcp )
                                 };

            state.Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.Linger, false );
            state.Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.KeepAlive, false );
            state.Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, CoreSettings.Instance.ReceiveBufferSize );
            state.Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.SendBuffer, CoreSettings.Instance.ReceiveBufferSize );

            state.Socket.BeginConnect( host, port, HandleConnect, state );
        }

        public void Listen( int hostInterfaceIndex, int port, Action<INetworkFacade> callback )
        {
            if ( _socket != null )
            {
                throw new InvalidOperationException( "Factory already listening for connections. Listen method called twice." );
            }

            _clientConnectCallback = callback;

            _socket = new Socket( AddressFamily.InterNetwork,
                                  SocketType.Stream,
                                  ProtocolType.Tcp );

            IPHostEntry dnsEntry = Dns.GetHostEntry( CoreSettings.Instance.LocalHostDnsEntry );

            IPEndPoint endPoint =
                new IPEndPoint( dnsEntry.AddressList[hostInterfaceIndex], port );

            _socket.Bind( endPoint );

            _socket.Listen( CoreSettings.Instance.ProxyClientListenerBacklog );

            _socket.BeginAccept( HandleNewClientConnect, null );
        }

        public void EndListen()
        {
            Contract.Ensures( _socket == null );

            if ( _socket == null )
            {
                throw new InvalidOperationException( "Factory is not listening for connections. Unable to end listening." );
            }

            _socket.Close();
            _socket = null;
        }

        #endregion

        private void HandleConnect( IAsyncResult ar )
        {
            Contract.Requires( ar != null );
            Contract.Requires( ar.AsyncState is ConnectState );
            ConnectState state = ar.AsyncState as ConnectState;

            try
            {
                state.Socket.EndConnect( ar );
                state.Callback( true, new NetworkFacade( state.Socket ) );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( "Unable to connect to remote host", ex );
                state.Callback( false, null );
            }
        }

        private void HandleNewClientConnect( IAsyncResult ar )
        {
            Contract.Requires( ar != null );

            try
            {
                // Server may be in the process of shutting down. Ignore pending connect notifications.
                if ( _socket != null )
                {
                    Socket clientSocket = _socket.EndAccept( ar );

                    // Immediately listen for the next clientSession
                    _socket.BeginAccept( HandleNewClientConnect, null );

                    ServiceLog.Logger.Info( "{0} New client connect", clientSocket.GetHashCode() );

                    _clientConnectCallback( new NetworkFacade( clientSocket ) );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( "Error establishing client connect", ex );
            }
        }

        #region Nested type: ConnectState

        private class ConnectState
        {
            public Action<bool, INetworkFacade> Callback { get; set; }
            public Socket Socket { get; set; }
        }

        #endregion
    }
}