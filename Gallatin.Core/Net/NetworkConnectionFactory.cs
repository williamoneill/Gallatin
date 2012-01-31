using System;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using Gallatin.Core.Service;

namespace Gallatin.Core.Net
{
    [Export( typeof (INetworkConnectionFactory) )]
    internal class NetworkConnectionFactory : INetworkConnectionFactory
    {
        private readonly ICoreSettings _settings;
        private Action<INetworkConnection> _clientConnectCallback;
        private Socket _socket;

        [ImportingConstructor]
        public NetworkConnectionFactory( ICoreSettings settings )
        {
            Contract.Requires( settings != null );
            _settings = settings;
        }

        #region INetworkConnectionFactory Members

        public void BeginConnect( string host, int port, Action<bool, INetworkConnection> callback )
        {
            ConnectState state = new ConnectState
                                 {
                                     Callback = callback,
                                     Socket = new Socket( AddressFamily.InterNetwork,
                                                          SocketType.Stream,
                                                          ProtocolType.Tcp )
                                 };

            //state.Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.Linger, false );
            //state.Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.KeepAlive, false );
            state.Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, NetworkConnection.BufferLength );
            state.Socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.SendBuffer, NetworkConnection.BufferLength );

            state.Socket.BeginConnect( host, port, HandleConnect, state );
        }

        public void Listen( string address, int port, Action<INetworkConnection> callback )
        {
            Contract.Requires( !string.IsNullOrEmpty( address ) );
            Contract.Requires( port > 0 );
            Contract.Requires( callback != null );

            if ( _socket != null )
            {
                throw new InvalidOperationException( "Factory already listening for connections. Listen method called twice." );
            }

            ServiceLog.Logger.Info( "Listening to client connections -- Address {0}, port {1}", address, port );

            _clientConnectCallback = callback;

            _socket = new Socket( AddressFamily.InterNetwork,
                                  SocketType.Stream,
                                  ProtocolType.Tcp );

            _socket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.Linger, false );

            IPHostEntry dnsEntry = Dns.GetHostEntry( address );

            // For now, we only accept addresses in dotted quad notation that reside on the host.
            // This is desireable for multi-homed systems.
            const int NoValue = -1;
            int index = NoValue;
            bool foundMatch = false;
            for ( int i = 0; i < dnsEntry.AddressList.Length && !foundMatch; i++ )
            {
                byte[] addressBytes = dnsEntry.AddressList[i].GetAddressBytes();

                string[] parts = address.Split( '.' );

                if ( addressBytes.Length
                     == parts.Length )
                {
                    foundMatch = true;

                    for ( int j = 0; j < addressBytes.Length; j++ )
                    {
                        if ( addressBytes[j]
                             != byte.Parse( parts[j] ) )
                        {
                            foundMatch = false;
                        }
                    }
                }

                if ( foundMatch )
                {
                    index = i;
                }
            }

            if ( index == NoValue )
            {
                throw new ArgumentException( string.Format(
                    "The server address {0} was invalid or does not exist on the host. It must be a dotted quad IP address.", address ) );
            }

            IPEndPoint endPoint =
                new IPEndPoint( dnsEntry.AddressList[index], port );

            _socket.Bind( endPoint );

            _socket.Listen( _settings.ProxyClientListenerBacklog );

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

                if ( state.Socket == null )
                {
                    ServiceLog.Logger.Error( "Failed to connect to remote host. Server socket is null." );
                    state.Callback( false, null );
                }
                else
                {
                    state.Callback( true, new NetworkConnection( state.Socket ) );
                }
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

                    if ( clientSocket == null )
                    {
                        ServiceLog.Logger.Warning( "The client connection failed and the client session will be aborted." );
                    }
                    else
                    {
                        ServiceLog.Logger.Info( "{0} New client connect", clientSocket.GetHashCode() );

                        // TODO: Unsure if this is really needed. Client sessions remain in memory longer if this is not set
                        //clientSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.Linger, false );
                        //clientSocket.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.KeepAlive, false );

                        NetworkConnection networkConnection = new NetworkConnection( clientSocket );

                        _clientConnectCallback( networkConnection );
                    }
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
            public Action<bool, INetworkConnection> Callback { get; set; }
            public Socket Socket { get; set; }
        }

        #endregion
    }
}