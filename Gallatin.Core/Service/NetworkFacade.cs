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
        private bool _hasShutdownReceive;
        private bool _hasShutdownSend;

        public NetworkFacade( Socket socket )
        {
            Contract.Requires( socket != null );
            Contract.Requires( socket.Connected );

            Socket = socket;

            _hasShutdownReceive = false;
            _hasShutdownSend = false;
        }

        private Semaphore _receiveSemaphore = new Semaphore(1,1);

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
            ServiceLog.Logger.Verbose("{0} Sending data, len: {1}", Id, buffer.Length);

            if (_hasShutdownReceive)
            {
                ServiceLog.Logger.Warning("{0} Socket has stopped receiving data. Send request ignored.", Id);
            }
            else
            {
                Socket.BeginSend(
                    buffer,
                    0,
                    buffer.Length,
                    SocketFlags.None,
                    HandleSend,
                    callback);
            }
        }

        public void BeginReceive( Action<bool, byte[], INetworkFacade> callback )
        {
            ServiceLog.Logger.Verbose("{0} Begin receive data", Id);

            // TODO: this should use the appliction settings
            const int BufferSize = 8192;

            _receiveBuffer = new byte[BufferSize];

            if (_hasShutdownSend)
            {
                ServiceLog.Logger.Warning("{0} Socket has stopped sending data. Receive request ignored.");
            }
            else
            {
                _receiveSemaphore.WaitOne();

                Socket.BeginReceive(
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
            if (!_hasShutdownReceive && !_hasShutdownSend)
            {
                _hasShutdownReceive = true;
                _hasShutdownSend = true;

                Socket.BeginDisconnect(false, HandleDisconnect, callback);
            }
            else
            {
                // Avoid object disposed exception
                callback( true, this );
            }

        }

        public bool IsConnected
        {
            get
            {
                // Do not hit the Socket first or risk getting an object disposed exception
                return !_hasShutdownReceive && !_hasShutdownSend && Socket.Connected;
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

            try
            {
                if (_hasShutdownReceive)
                {
                    ServiceLog.Logger.Info("{0} Network endpoint stopped receiving data. Ignoring send results.", Id);
                    callback( false, this );
                }
                else
                {
                    SocketError socketError;
                    int bytesSent = Socket.EndSend(ar, out socketError);

                    if (bytesSent == 0)
                    {
                        ServiceLog.Logger.Info("{0} Network endpoint stopped receiving data", Id);
                        _hasShutdownReceive = true;
                        callback(true, this);
                    }
                    else if (socketError != SocketError.Success)
                    {
                        ServiceLog.Logger.Error("{0} Socket error: {1}", Id, socketError);
                        OnConnectionClosed();
                        callback(false, this);
                    }
                    else
                    {
                        callback(true, this);
                    }
                    
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

            ServiceLog.Logger.Verbose("{0} NetworkFacade::HandleReceive", Id);

            Action<bool, byte[], INetworkFacade> callback = ar.AsyncState as Action<bool, byte[], INetworkFacade>;


            try
            {
                if (_hasShutdownSend)
                {
                    _receiveSemaphore.Release();
                    ServiceLog.Logger.Info("{0} Network endpoint stopped sending data. Ignoring receive results.", Id);
                    callback(false, null, this);
                }
                else
                {
                    SocketError socketError;
                    int bytesReceived = Socket.EndReceive(ar, out socketError);

                    if (bytesReceived == 0)
                    {
                        _hasShutdownSend = true;
                        _receiveSemaphore.Release();

                        ServiceLog.Logger.Info("{0} Network endpoint stopped sending data", Id);
                        callback(true, null, this);
                    }
                    else if (socketError != SocketError.Success)
                    {
                        _receiveSemaphore.Release();

                        ServiceLog.Logger.Info("{0} Network error encountered while receiving data: {1}", Id, socketError);
                        OnConnectionClosed();
                        callback(false, null, this);
                    }
                    else
                    {
                        byte[] buffer = new byte[bytesReceived];
                        Array.Copy(_receiveBuffer, buffer, bytesReceived);

                        // Release after we made a copy of the shared memory buffer
                        _receiveSemaphore.Release();

                        callback(true, buffer, this);
                    }
                    
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