using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Sockets;
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
            _socket = socket;
        }

        private void HandleSend(IAsyncResult ar)
        {
            Action<bool> callback = ar.AsyncState as Action<bool>;
            Trace.Assert(callback != null);

            try
            {
                SocketError socketError;
                int bytesSent = _socket.EndSend( ar, out socketError );

                if( bytesSent == 0)
                {
                    Log.Info("{0} Socket disconnected", _socket.GetHashCode());
                    callback( false );
                }
                else if(socketError != SocketError.Success)
                {
                    Log.Error("{0} Socket error: {1}", _socket.GetHashCode(), socketError);
                    callback( false );
                }
                else
                {
                    callback( true );
                }

            }
            catch ( Exception ex )
            {
                Log.Exception(string.Format("{0} Error receiving network data", _socket.GetHashCode()), ex );
                callback( false );
            }
        }

        public void BeginSend( byte[] buffer, Action<bool> callback )
        {
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
            Action<bool, byte[]> callback = ar.AsyncState as Action<bool, byte[]>;
            Trace.Assert(callback!= null);

            try
            {
                SocketError socketError;
                int bytesReceived = _socket.EndReceive( ar, out socketError );

                if(bytesReceived == 0)
                {
                    Log.Info("{0} Lost connection while receiving data", _socket.GetHashCode());
                    callback( false, null );
                }
                else if(socketError != SocketError.Success)
                {
                    Log.Info("{0} Network error encountered while receiving data: {1}", _socket.GetHashCode(), socketError);
                    callback(false, null);
                }
                else
                {
                    callback( true, _receiveBuffer.Take( bytesReceived ).ToArray() );
                }
            }
            catch ( Exception ex )
            {
                Log.Exception(string.Format("{0} Unhandled exception while receiving data", _socket.GetHashCode()), ex);
                callback( false, null );
            }

        }

        public void BeginReceive( Action<bool, byte[]> callback )
        {
            _receiveBuffer = new byte[8000];
            _socket.BeginReceive( _receiveBuffer,
                                  0,
                                  _receiveBuffer.Length,
                                  SocketFlags.None,
                                  HandleReceive,
                                  callback );
        }

        private void HandleDisconnect(IAsyncResult ar)
        {
            Contract.Assert(ar.AsyncState is Action<bool>);

            Action<bool> callback = ar.AsyncState as Action<bool>;
            Trace.Assert(callback != null);

            try
            {

            }
            catch ( Exception ex )
            {
                Log.Exception(string.Format("{0} Unhandled exception when shutting down connection", _socket.GetHashCode()), ex);
            }
        }

        public void BeginClose( Action<bool> callback )
        {
            Contract.Requires( callback != null );

            _socket.BeginDisconnect( false, HandleDisconnect, callback );
        }
    }
}