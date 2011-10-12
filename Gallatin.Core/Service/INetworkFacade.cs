using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    public interface INetworkFacadeFactory
    {
        void BeginConnect( string host, int port, Action<bool,INetworkFacade> callback );
        void Start(int hostInterfaceIndex, int port, Action<INetworkFacade> callback);
    }

    public interface INetworkFacade
    {
        void BeginSend( byte[] buffer, Action<bool> callback );
        void BeginReceive( Action<bool, byte[]> callback );
        void BeginClose( Action<bool> callback );
    }

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

        public void BeginClose( Action<bool> callback )
        {
            throw new NotImplementedException();
        }
    }

    public class NetworkFacadeFactory : INetworkFacadeFactory
    {
        private class ConnectState
        {
            public Socket Socket { get; set; }
            public Action<bool, INetworkFacade> Callback { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }
        }

        private void HandleConnect(IAsyncResult ar)
        {
            ConnectState state = ar.AsyncState as ConnectState;
            Trace.Assert(state != null);

            try
            {
                state.Socket.EndConnect(ar);

                state.Callback(true, new NetworkFacade(state.Socket));
            }
            catch ( Exception ex )
            {
                Log.Exception(
                    string.Format( "Unable to connect to remote host {0} port {1}",
                                   state.Host,
                                   state.Port ),
                    ex );

                state.Callback( false, null );
            }
        }

        public void BeginConnect( string host, int port, Action<bool, INetworkFacade> callback )
        {
            ConnectState state = new ConnectState();

            state.Socket = new Socket(AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp);

            state.Callback = callback;
            state.Host = host;
            state.Port = port;

            state.Socket.BeginConnect( host, port, HandleConnect, state );

        }

        private Socket _socket;
        private Action<INetworkFacade> _clientConnectCallback;

        private void HandleNewClientConnect(IAsyncResult ar)
        {
            try
            {
                // Server may be in the process of shutting down. Ignore pending connect notifications.
                if (_socket != null)
                {
                    Socket _clientSocket = _socket.EndAccept(ar);

                    // Immediately listen for the next clientSession
                    _socket.BeginAccept(HandleNewClientConnect, null);

                    Log.Info("{0} New client connect", _clientSocket.GetHashCode());

                    _clientConnectCallback( new NetworkFacade(_clientSocket) );

                    //session.ClientSocket.BeginReceive(request.Buffer,
                    //                                   0,
                    //                                   request.Buffer.Length,
                    //                                   SocketFlags.None,
                    //                                   HandleDataFromClient,
                    //                                   request);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("Error establishing client connect", ex);
            }
            
        }

        public void Start(int hostInterfaceIndex, int port, Action<INetworkFacade> callback)
        {
            if(_socket != null)
            {
                throw new InvalidOperationException( "Instance has already been started" );
            }

            _clientConnectCallback = callback;

            _socket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp);

            IPHostEntry dnsEntry = Dns.GetHostEntry("localhost");

            IPEndPoint endPoint =
                new IPEndPoint(dnsEntry.AddressList[hostInterfaceIndex], port);

            _socket.Bind(endPoint);

            _socket.Listen(30);

            _socket.BeginAccept(HandleNewClientConnect, null);
        }
    }
}
