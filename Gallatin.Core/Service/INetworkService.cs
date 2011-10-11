using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    public interface INetworkService
    {
        void BeginSend(byte[] buffer, Action<bool> callback);
        void BeginConnect(string host, int port, Action<bool, INetworkService> callback);
        void BeginReceive(Action<bool> callback);
        void BeginClose(Action<bool> callback);
    }

    public interface INetworkServiceListener
    {
        void Start(int hostInterfaceOrdinal, Action<INetworkService> connectCallback);
        void Stop();
    }

    internal class NetworkService : INetworkService
    {
        private Socket _socket;
        private byte[] _sendBuffer;
        private byte[] _receiveBuffer;
        private Action<bool> _sendCallback;

        public NetworkService(Socket socket )
        {
            _socket = socket;
        }

        public void BeginSend(byte[] buffer, Action<bool> callback)
        {
            _sendBuffer = buffer;

            _sendCallback = callback;

            _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, HandleEndSend, this);
        }

        private void HandleEndSend(IAsyncResult result)
        {
            NetworkService networkService = result.AsyncState as NetworkService;
            Trace.Assert(networkService != null);

            try
            {
                SocketError error;
                int dataSent = _socket.EndSend(result, out error);
                if(error!= SocketError.Success)
                {
                    Log.Error("{0} Failed to send data: {1}", _socket.GetHashCode(), error);

                    BeginClose(null);

                    if (_sendCallback != null)
                        _sendCallback(true);
                }
                else if (dataSent == 0)
                {
                    Log.Info("{0} Socket remote closed connection during send.", _socket.GetHashCode());

                    BeginClose(null);
                    
                    if (_sendCallback != null)
                        _sendCallback(true);

                }
                else
                {
                    Log.Verbose("{0} Send complete", _socket.GetHashCode());

                    if (_sendCallback != null)
                        _sendCallback(true);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(string.Format("{0} Error sending data", _socket.GetHashCode()), ex);
                BeginClose(null);

                if (_sendCallback != null)
                    _sendCallback(true);
            }
        }


        private Action<bool, INetworkService> _connectCallback;
        private Socket _connectSocket;

        private void HandleConnect( IAsyncResult ar )
        {
            NetworkService networkService = ar.AsyncState as NetworkService;
            Trace.Assert(networkService != null);

            try
            {
                _connectSocket.EndConnect(ar);

                _connectCallback( false, new NetworkService(_connectSocket) );
            }
            catch (Exception ex)
            {
                Log.Exception(string.Format("{0} Error connecting to remote host", _socket.GetHashCode()), ex);
            }
        }

        public void BeginConnect(string host, int port, Action<bool, INetworkService> callback)
        {
            _connectCallback = callback;

            _connectSocket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp);

            _connectSocket.BeginConnect(
                host,
                port,
                HandleConnect,
                this);
        }

        public void BeginReceive(Action<bool, byte[]> callback)
        {

            todo; DebuggerNonUserCodeAttribute receive ubffer
        }

        private void HandleDisconnect(IAsyncResult ar)
        {
            NetworkService networkService = ar.AsyncState as NetworkService;
            Trace.Assert(networkService != null);

            try
            {
                Log.Verbose("{0} Closing socket", _socket.GetHashCode());

                _socket.EndDisconnect(ar);
                _socket.Close();
                _socket = null;
            }
            catch (Exception ex)
            {
                Log.Exception(string.Format("{0} Error closing socket", _socket.GetHashCode()), ex );
            }
        }

        public void BeginClose(Action<bool> callback)
        {
            if (_socket != null && _socket.Connected)
            {

                // Re-evaluate SO_REUSEADDRESS
                _socket.BeginDisconnect(false, HandleDisconnect, this);
            }
        }
    }
}
