using System;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    public class NetworkFacadeFactory : INetworkFacadeFactory
    {
        private Action<INetworkFacade> _clientConnectCallback;
        private Socket _socket;

        #region INetworkFacadeFactory Members

        public void BeginConnect<T>( string host, int port, Action<bool, INetworkFacade, T> callback, T state )
        {
            ConnectState<T> connectState = new ConnectState<T>
                                           {
                                               Socket = new Socket( AddressFamily.InterNetwork,
                                                                    SocketType.Stream,
                                                                    ProtocolType.Tcp ),
                                               Callback = callback,
                                               Host = host,
                                               Port = port,
                                               State = state
                                           };

            connectState.Socket.BeginConnect( host, port, HandleConnect<T>, connectState );
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

            IPHostEntry dnsEntry = Dns.GetHostEntry( "localhost" );

            IPEndPoint endPoint =
                new IPEndPoint( dnsEntry.AddressList[hostInterfaceIndex], port );

            _socket.Bind( endPoint );

            _socket.Listen( 30 );

            _socket.BeginAccept( HandleNewClientConnect, null );
        }

        #endregion

        private void HandleConnect<T>( IAsyncResult ar )
        {
            Contract.Assert( ar != null );
            Contract.Assert( ar.AsyncState is ConnectState<T> );

            ConnectState<T> connectState = ar.AsyncState as ConnectState<T>;

            try
            {
                connectState.Socket.EndConnect( ar );

                connectState.Callback( true, new NetworkFacade( connectState.Socket ), connectState.State );
            }
            catch ( Exception ex )
            {
                Log.Exception(
                    string.Format( "Unable to connect to remote host {0} port {1}",
                                   connectState.Host,
                                   connectState.Port ),
                    ex );

                connectState.Callback( false, null, default( T ) );
            }
        }

        private void HandleNewClientConnect( IAsyncResult ar )
        {
            try
            {
                // Server may be in the process of shutting down. Ignore pending connect notifications.
                if ( _socket != null )
                {
                    Socket _clientSocket = _socket.EndAccept( ar );

                    // Immediately listen for the next clientSession
                    _socket.BeginAccept( HandleNewClientConnect, null );

                    Log.Info( "{0} New client connect", _clientSocket.GetHashCode() );

                    _clientConnectCallback( new NetworkFacade( _clientSocket ) );
                }
            }
            catch ( Exception ex )
            {
                Log.Exception( "Error establishing client connect", ex );
            }
        }

        #region Nested type: ConnectState

        private class ConnectState<T>
        {
            public Socket Socket { get; set; }
            public Action<bool, INetworkFacade, T> Callback { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }
            public T State { get; set; }
        }

        #endregion
    }
}