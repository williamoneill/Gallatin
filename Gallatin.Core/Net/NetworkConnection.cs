using System;
using System.Diagnostics.Contracts;
using System.Net.Sockets;
using System.Threading;

namespace Gallatin.Core.Net
{
    internal class NetworkConnection : INetworkConnection
    {
        // TODO: make this a setting
        public const int BufferLength = 8192;


        private readonly Socket _socket;
        private bool _hasClosed;
        private bool _hasShutdown;

        public NetworkConnection( Socket socket )
        {
            Contract.Requires(socket!=null);
            Logger = new DefaultSessionLogger();
            Logger.Verbose(string.Format("{0} NetworkConnection Constructor", socket.GetHashCode()));

            _socket = socket;

            _hasClosed = false;
            _hasShutdown = false;
        }

        #region INetworkConnection Members

        public event EventHandler ConnectionClosed;
        public event EventHandler Shutdown;


        public event EventHandler DataSent;

        public event EventHandler<DataAvailableEventArgs> DataAvailable;

        public string Id
        {
            get { return _socket.RemoteEndPoint.ToString(); }
        }

        public ISessionLogger Logger { private get; set; }

        public void SendData( byte[] data )
        {
            Logger.Verbose( "Socket -- SendData" );

            lock ( _socket )
            {
                if ( _socket.Connected  )
                {
                    Logger.Verbose( "Sending socket data" );
                    _socket.BeginSend( data, 0, data.Length, SocketFlags.None, HandleSend, null );
                }
                else
                {
                    throw new SocketException((int)SocketError.Shutdown);
                }
            }
        }

        public void Close()
        {
            Logger.Verbose( "Socket Close" );

            lock ( _socket )
            {
                if ( _socket.Connected && !_hasClosed )
                {
                    if ( !_hasShutdown )
                    {
                        _socket.Shutdown( SocketShutdown.Both );
                    }

                    _socket.BeginDisconnect( false, HandleDisconnect, null );
                }
            }
        }

        public void Start()
        {
            byte[] buffer = new byte[BufferLength];
            _socket.BeginReceive( buffer, 0, BufferLength, SocketFlags.None, HandleReceive, buffer );
        }

        #endregion



        private void OnDataSent()
        {
            Logger.Verbose( "Socket -- OnDataSent" );

            EventHandler e = DataSent;
            if ( e != null )
            {
                e( this, new EventArgs() );
            }
        }

        private void OnShutdown()
        {
            Logger.Verbose( "Socket -- OnReceiveShutdown" );

            lock ( _socket )
            {
                if ( !_hasShutdown )
                {
                    _hasShutdown = true;

                    EventHandler e = Shutdown;
                    if ( e != null )
                    {
                        e( this, new EventArgs() );
                    }
                }
            }
        }

        private void OnDataAvailable( byte[] data )
        {
            Logger.Verbose( "Socket -- OnDataAvailable" );

            EventHandler<DataAvailableEventArgs> dataAvailableEvent = DataAvailable;
            if ( dataAvailableEvent != null )
            {
                dataAvailableEvent( this, new DataAvailableEventArgs( data ) );
            }
        }

        private void OnSocketClosed()
        {
            Logger.Verbose( "Socket -- OnSocketClosed" );

            lock ( _socket )
            {
                if ( !_hasClosed )
                {
                    _hasClosed = true;

                    EventHandler e = ConnectionClosed;
                    if ( e != null )
                    {
                        e( this, new EventArgs() );
                    }
                }
            }
        }

        private void HandleReceive( IAsyncResult ar )
        {
            //Contract.Ensures( ar.AsyncState is byte[] );

            Logger.Verbose( "Socket received data" );

            try
            {
                if (_hasShutdown || _hasClosed)
                {
                    Logger.Info("Receive handler - socket stopped sending data - ignoring invocation");
                }
                else
                {
                    SocketError socketError;
                    int bytesReceived = _socket.EndReceive(ar, out socketError);

                    if (bytesReceived == 0)
                    {
                        OnShutdown();
                        Logger.Verbose("Socket stopped sending data");
                    }
                    else if (socketError != SocketError.Success)
                    {
                        OnSocketClosed();
                        Logger.Verbose("Socket closed");
                    }
                    else
                    {
                        Logger.Verbose(string.Format("{0} NetworkConnection HandleReceive", _socket.GetHashCode()));
                        byte[] buffer = ar.AsyncState as byte[];
                        byte[] trimmedBuffer = new byte[bytesReceived];
                        Array.Copy(buffer, trimmedBuffer, bytesReceived);
                        OnDataAvailable(trimmedBuffer);

                        lock (_socket)
                        {
                            if (_socket.Connected && !_hasClosed
                                 && !_hasShutdown)
                            {
                                byte[] receiveBuffer = new byte[BufferLength];
                                _socket.BeginReceive(receiveBuffer, 0, BufferLength, SocketFlags.None, HandleReceive, receiveBuffer);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception("Unhandled exception receiving data on socket", ex);
                OnSocketClosed();
            }
        }

        private void HandleSend( IAsyncResult ar )
        {
            Logger.Verbose( "Socket sent data" );

            try
            {
                if ( _hasShutdown || _hasClosed )
                {
                    Logger.Info( "Send handler - socket stopped receiving data - ignoring invocation" );
                }
                else
                {
                    SocketError socketError;
                    int bytesSent = _socket.EndSend( ar, out socketError );

                    if ( bytesSent == 0 )
                    {
                        Logger.Info( "Stopped receiving data" );
                        OnShutdown();
                    }
                    else if ( socketError != SocketError.Success )
                    {
                        Logger.Error( "Error sending data" );
                        OnSocketClosed();
                    }
                    else
                    {
                        OnDataSent();
                    }
                }
            }
            catch ( Exception ex )
            {
                Logger.Exception( "Unhandled data sending data", ex );
                OnSocketClosed();
            }
        }

        private void HandleDisconnect( IAsyncResult ar )
        {
            try
            {
                _socket.EndDisconnect( ar );
                _socket.Close();
                OnSocketClosed();
            }
            catch ( Exception ex )
            {
                Logger.Exception( "Unhandled exception disconnecting socket", ex );
                OnSocketClosed();
            }
        }
    }
}