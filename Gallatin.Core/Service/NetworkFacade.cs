using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    public class NetworkFacade : INetworkFacade
    {
        private byte[] _sendBuffer;
        private byte[] _receiveBuffer;
        private Socket _socket;

        public NetworkFacade(Socket socket)
        {
            Contract.Requires(socket != null);
            Contract.Requires(socket.Connected);

            _socket = socket;
            _lastAccessTime = DateTime.Now;
        }

        private void HandleSend(IAsyncResult ar)
        {
            Contract.Requires(ar != null);
            Contract.Requires(ar.AsyncState is Action<bool,INetworkFacade>);

            LastActivityTime = DateTime.Now;

            Action<bool, INetworkFacade> callback = ar.AsyncState as Action<bool, INetworkFacade>;

            try
            {
                SocketError socketError;
                int bytesSent = _socket.EndSend( ar, out socketError );

                if( bytesSent == 0)
                {
                    Log.Logger.Info("{0} Socket disconnected", _socket.GetHashCode());
                    callback( false, this );
                }
                else if(socketError != SocketError.Success)
                {
                    Log.Logger.Error("{0} Socket error: {1}", _socket.GetHashCode(), socketError);
                    callback( false, this );
                }
                else
                {
                    callback( true, this );
                }

            }
            catch ( Exception ex )
            {
                Log.Logger.Exception(string.Format("{0} Error receiving network data", _socket.GetHashCode()), ex );
                callback( false, this );
            }
        }

        public void BeginSend( byte[] buffer, Action<bool,INetworkFacade> callback )
        {
            LastActivityTime = DateTime.Now;

            _sendBuffer = buffer;
            _socket.BeginSend( _sendBuffer,
                               0,
                               _sendBuffer.Length,
                               SocketFlags.None,
                               HandleSend,
                               callback );
        }

        private void HandleReceive(IAsyncResult ar)
        {
            Contract.Requires(ar != null);
            Contract.Requires(ar.AsyncState is Action<bool, byte[], INetworkFacade>);

            LastActivityTime = DateTime.Now;

            Action<bool, byte[], INetworkFacade> callback = ar.AsyncState as Action<bool, byte[], INetworkFacade>;

            try
            {
                lock (_socket)
                {
                    _pendingReceiveHandle = null;
                }

                SocketError socketError;
                int bytesReceived = _socket.EndReceive( ar, out socketError );
                
                if(bytesReceived == 0)
                {
                    Log.Logger.Info("{0} Lost connection while receiving data", _socket.GetHashCode());
                    callback( false, null, this );
                }
                else if(socketError != SocketError.Success)
                {
                    Log.Logger.Info("{0} Network error encountered while receiving data: {1}", _socket.GetHashCode(), socketError);
                    callback(false, null, this);
                }
                else
                {
                    callback( true, _receiveBuffer.Take( bytesReceived ).ToArray(), this );
                }
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception(string.Format("{0} Unhandled exception while receiving data", _socket.GetHashCode()), ex);
                callback( false, null, this );
            }

        }

        private IAsyncResult _pendingReceiveHandle;

        public void BeginReceive(Action<bool, byte[], INetworkFacade> callback)
        {
            LastActivityTime = DateTime.Now;

            _receiveBuffer = new byte[8000];
            _pendingReceiveHandle =  
                _socket.BeginReceive(
                    _receiveBuffer,
                                  0,
                                  _receiveBuffer.Length,
                                  SocketFlags.None,
                                  HandleReceive,
                                  callback );
        }

        private void HandleDisconnect(IAsyncResult ar)
        {
            Contract.Requires(ar.AsyncState is Action<bool, INetworkFacade>);

            LastActivityTime = DateTime.Now;

            Action<bool, INetworkFacade> callback = ar.AsyncState as Action<bool, INetworkFacade>;

            try
            {
                _socket.EndDisconnect(ar);
                _socket.Close();
                callback( true, this );
            }
            catch ( Exception ex )
            {
                Log.Logger.Exception(string.Format("{0} Unhandled exception when shutting down connection", _socket.GetHashCode()), ex);
                callback( false, this );
            }
        }

        private DateTime _lastAccessTime;

        public void BeginClose(Action<bool, INetworkFacade> callback)
        {
            LastActivityTime = DateTime.Now;

            _socket.Shutdown(SocketShutdown.Both);
            _socket.BeginDisconnect( false, HandleDisconnect, callback );
        }


        public DateTime LastActivityTime
        {
            get
            {
                return _lastAccessTime;
            }
            private set
            {
                _lastAccessTime = value;
            }
        }

        public void CancelPendingReceive()
        {
            lock(_socket)
            {
                if (_pendingReceiveHandle != null)
                {
                    Log.Logger.Verbose("{0} Cancelling pending receive", _socket.GetHashCode());
                    _socket.EndReceive(_pendingReceiveHandle);
                }
            }
        }

        public object Context
        {
            get; set;
        }
    }
}