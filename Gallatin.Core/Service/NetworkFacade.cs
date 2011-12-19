using System;
using System.Diagnostics.Contracts;
using System.Net.Sockets;
using System.Threading;
using System.ComponentModel.Composition;

namespace Gallatin.Core.Service
{
    internal class NetworkFacade : INetworkFacade
    {
        private byte[] _receiveBuffer;
        private byte[] _sendBuffer;
        private bool _hasShutdown;

        public NetworkFacade( Socket socket )
        {
            Contract.Requires( socket != null );
            Contract.Requires( socket.Connected );

            Socket = socket;
        }

        internal Socket Socket { get; set; }

        public object Context { get; set; }

        #region INetworkFacade Members

        public int Id
        {
            get
            {
                return Socket.GetHashCode();
            }
        }

        public string ConnectionId
        {
            get
            {
                return Socket.RemoteEndPoint.ToString();
            }
        }

        public void BeginSend( byte[] buffer, Action<bool, INetworkFacade> callback )
        {
            if (!_hasShutdown)
            {
                ServiceLog.Logger.Verbose("{0} Sending data, len: {1}", Id, buffer.Length);

                _sendBuffer = buffer;
                Socket.BeginSend(
                    _sendBuffer,
                    0,
                    _sendBuffer.Length,
                    SocketFlags.None,
                    HandleSend,
                    callback);
            }
        }

        private IAsyncResult _receiveHandle;

        public void BeginReceive( Action<bool, byte[], INetworkFacade> callback )
        {
            const int BufferSize = 8192;

            if (!_hasShutdown)
            {
                ServiceLog.Logger.Verbose("{0} Receiving data", Id);

                _receiveBuffer = new byte[BufferSize];

                _receiveHandle = Socket.BeginReceive(
                    _receiveBuffer,
                    0,
                    _receiveBuffer.Length,
                    SocketFlags.None,
                    HandleReceive,
                    callback);
            }
        }

        public void BeginClose( Action<bool, INetworkFacade> callback )
        {
            // After further reseach, this is not needed despite MSDN documentation.
            //Socket.Shutdown(SocketShutdown.Both);

            Socket.BeginDisconnect( false, HandleDisconnect, callback );
        }

        public void CancelPendingReceive()
        {
            if (_receiveHandle != null)
            {
                Socket.EndReceive( _receiveHandle );
                _receiveHandle = null;
            }
        }

        private void OnConnectionClosed()
        {
            EventHandler connectionClosed = ConnectionClosed;
            if (connectionClosed != null)
            {
                connectionClosed(this, new EventArgs());
            }
        }

        public event EventHandler ConnectionClosed;

        #endregion

        private void HandleSend( IAsyncResult ar )
        {
            Contract.Requires( ar != null );
            Contract.Requires( ar.AsyncState is Action<bool, INetworkFacade> );

            Action<bool, INetworkFacade> callback = ar.AsyncState as Action<bool, INetworkFacade>;

            if ( _hasShutdown )
            {
                return;
            }

            try
            {
                SocketError socketError;
                int bytesSent = Socket.EndSend( ar, out socketError );

                if ( bytesSent == 0 )
                {
                    ServiceLog.Logger.Info( "{0} Socket disconnected", Id );
                    OnConnectionClosed();
                    callback( false, this );
                }
                else if ( socketError != SocketError.Success )
                {
                    ServiceLog.Logger.Error( "{0} Socket error: {1}", Id, socketError );
                    OnConnectionClosed();
                    callback( false, this );
                }
                else
                {
                    callback( true, this );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception while sending network data", Id ), ex );
                OnConnectionClosed();
                callback(false, this);
            }
        }

        private void HandleReceive( IAsyncResult ar )
        {
            Contract.Requires( ar != null );
            Contract.Requires( ar.AsyncState is Action<bool, byte[], INetworkFacade> );

            Action<bool, byte[], INetworkFacade> callback = ar.AsyncState as Action<bool, byte[], INetworkFacade>;

            if ( _hasShutdown || _receiveHandle == null )
            {
                return;
            }

            try
            {
                _receiveHandle = null;
                SocketError socketError;
                int bytesReceived = Socket.EndReceive( ar, out socketError );

                if ( bytesReceived == 0 )
                {
                    ServiceLog.Logger.Info( "{0} Network endpoint is shutting down the socket.", Id );
                    //OnConnectionClosed();
                    //callback( false, null, this );

                    _hasShutdown = true;
                    callback(true, null, this);
                }
                else if ( socketError != SocketError.Success )
                {
                    ServiceLog.Logger.Info( "{0} Network error encountered while receiving data: {1}", Id, socketError );
                    OnConnectionClosed();
                    callback( false, null, this );
                }
                else
                {
                    byte[] buffer = new byte[bytesReceived];
                    Array.Copy( _receiveBuffer, buffer, bytesReceived );
                    callback( true, buffer, this );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception while receiving data", Id ), ex );
                OnConnectionClosed();
                callback(false, null, this);
            }
        }

        private void HandleDisconnect( IAsyncResult ar )
        {
            Contract.Requires(ar != null);
            Contract.Requires( ar.AsyncState is Action<bool, INetworkFacade> );

            if (_hasShutdown)
            {
                return;
            }

            _hasShutdown = true;

            Action<bool, INetworkFacade> callback = ar.AsyncState as Action<bool, INetworkFacade>;

            try
            {
                Socket.EndDisconnect( ar );
                Socket.Close();
                OnConnectionClosed();
                callback( true, this );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception( string.Format( "{0} Unhandled exception when shutting down connection", Id ), ex );
                OnConnectionClosed();
                callback(false, this);
            }
        }
    }
}