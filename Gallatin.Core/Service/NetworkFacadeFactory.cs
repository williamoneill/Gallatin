using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
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

        public void Listen(int hostInterfaceIndex, int port, Action<INetworkFacade> callback)
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