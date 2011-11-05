using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    internal class NetworkFacade : INetworkFacade
    {
        private byte[] _sendBuffer;
        private byte[] _receiveBuffer;

        public NetworkFacade(Socket socket)
        {
            Contract.Requires(socket != null);
            Contract.Requires(socket.Connected);

            Socket = socket;
        }

        private void HandleSend(IAsyncResult ar)
        {
            Contract.Requires(ar != null);
            Contract.Requires(ar.AsyncState is Action<bool,INetworkFacade>);

            Action<bool, INetworkFacade> callback = ar.AsyncState as Action<bool, INetworkFacade>;

            if(_shutdown)
                return;
            

            try
            {
                SocketError socketError;
                int bytesSent = Socket.EndSend( ar, out socketError );

                if( bytesSent == 0)
                {
                    ServiceLog.Logger.Info("{0} Socket disconnected", Id);
                    callback( false, this );
                }
                else if(socketError != SocketError.Success)
                {
                    ServiceLog.Logger.Error("{0} Socket error: {1}", Id, socketError);
                    callback( false, this );
                }
                else
                {
                    callback( true, this );
                }

            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception while sending network data", Id), ex);
                callback( false, this );
            }
        }

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

        public void BeginSend( byte[] buffer, Action<bool,INetworkFacade> callback )
        {
            ServiceLog.Logger.Info("{0} Sending data {1}", Id, buffer.Length);

            _sendBuffer = buffer;
            Socket.BeginSend( _sendBuffer,
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

            Action<bool, byte[], INetworkFacade> callback = ar.AsyncState as Action<bool, byte[], INetworkFacade>;

            if (_shutdown)
                return;

            try
            {
                lock (Socket)
                {
                    _pendingReceiveHandle = null;
                }

                SocketError socketError;
                int bytesReceived = Socket.EndReceive( ar, out socketError );
                
                if(bytesReceived == 0)
                {
                    ServiceLog.Logger.Info("{0} Lost connection while receiving data", Id);
                    callback( false, null, this );
                }
                else if(socketError != SocketError.Success)
                {
                    ServiceLog.Logger.Info("{0} Network error encountered while receiving data: {1}", Id, socketError);
                    callback(false, null, this);
                }
                else
                {
                    byte[] buffer = new byte[bytesReceived];
                    Array.Copy( _receiveBuffer, buffer, bytesReceived  );
                    callback( true, buffer, this );
                }
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception while receiving data", Id), ex);
                callback( false, null, this );
            }

        }

        private IAsyncResult _pendingReceiveHandle;

        public void BeginReceive(Action<bool, byte[], INetworkFacade> callback)
        {
            ServiceLog.Logger.Info("{0} Receiving data", Id);

            _receiveBuffer = new byte[8192];
            _pendingReceiveHandle =  
                Socket.BeginReceive(
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

            Action<bool, INetworkFacade> callback = ar.AsyncState as Action<bool, INetworkFacade>;

            try
            {
                Socket.EndDisconnect(ar);
                Socket.Close();
                callback( true, this );
            }
            catch ( Exception ex )
            {
                ServiceLog.Logger.Exception(string.Format("{0} Unhandled exception when shutting down connection", Id), ex);
                callback( false, this );
            }
        }

        // Implement idisposable
        private bool _shutdown = false;

        public void BeginClose(Action<bool, INetworkFacade> callback)
        {
            _shutdown = true;
            
            // After further reseach, this is not needed despite MSDN documentation.
            //_socket.Shutdown(SocketShutdown.Both);
            
            Socket.BeginDisconnect( false, HandleDisconnect, callback );
        }

        internal Socket Socket { get; set; }

        public void CancelPendingReceive()
        {
            lock(Socket)
            {
                if (_pendingReceiveHandle != null)
                {
                    ServiceLog.Logger.Verbose("{0} Cancelling pending receive", Id);
                    Socket.EndReceive(_pendingReceiveHandle);
                }
            }
        }

        public object Context
        {
            get; set;
        }
    }
}