using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    public class SslTunnel
    {
        private Socket _clientSocket;
        private Socket _serverSocket;
        private string _httpVersion;
        private Guid _sessionId;

        private class TunnelSession
        {
            public TunnelSession(SslTunnel tunnel)
            {
                Tunnel = tunnel;
            }
            public SslTunnel Tunnel { get; private set; }
            public byte[] Data { get; set; }
        }

        public SslTunnel( Socket clientSocket, Socket serverSocket, string HttpVersion, Guid sessionId )
        {
            _clientSocket = clientSocket;
            _serverSocket = serverSocket;
            _httpVersion = HttpVersion;
            _sessionId = sessionId;
        }

        public void EstablishTunnel()
        {
            TunnelSession data = new TunnelSession(this);

            Log.Info( "{0} Starting SSL connection", _sessionId );

            data.Data = Encoding.UTF8.GetBytes( string.Format(
                "HTTP/{0} 200 Connection established\r\n" +
                "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n",
                _httpVersion ) );

            _clientSocket.BeginSend( data.Data,
                                     0,
                                     data.Data.Length,
                                     SocketFlags.None,
                                     HandleClientSend,
                                     data );
        }

        private const int BufferSize = 8000;

        private void HandleClientSend(IAsyncResult ar)
        {
            try
            {
                TunnelSession tunnel = ar.AsyncState as TunnelSession;
                Trace.Assert(tunnel != null);

                SocketError socketError;
                int dataSent = _clientSocket.EndSend(ar, out socketError);

                if (dataSent > 0 && socketError == SocketError.Success)
                {
                    tunnel.Data = new byte[BufferSize];
                    _clientSocket.BeginReceive(tunnel.Data,
                                                0,
                                                tunnel.Data.Length,
                                                SocketFlags.None,
                                                HandleNewDataFromClient,
                                                tunnel);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("{0} Error sending data to SSL client", ex);
            }
        }

        private void HandleNewDataFromClient(IAsyncResult ar)
        {
            try
            {
                TunnelSession tunnel = ar.AsyncState as TunnelSession;
                Trace.Assert(tunnel != null);

                int dataReceived = _clientSocket.EndReceive(ar);

                if (dataReceived > 0)
                {
                    _serverSocket.BeginSend(
                        tunnel.Data, 0, dataReceived, SocketFlags.None, HandleServerSend, tunnel);

                    TunnelSession data2 = new TunnelSession(this);
                    data2.Data = new byte[BufferSize];
                    _clientSocket.BeginReceive(data2.Data,
                                                0,
                                                data2.Data.Length,
                                                SocketFlags.None,
                                                HandleNewDataFromClient,
                                                data2);

                }

            }
            catch ( Exception ex )
            {
                Log.Exception("{0} Error receiving data from SSL client", ex);
            }
        }

        private void HandleServerSend(IAsyncResult ar)
        {
            try
            {
                TunnelSession tunnel = ar.AsyncState as TunnelSession;
                Trace.Assert(tunnel != null);

                SocketError socketError;
                int dataSent = _serverSocket.EndSend(ar, out socketError);

                if (dataSent > 0 && socketError == SocketError.Success)
                {
                    tunnel.Data = new byte[BufferSize];
                    _serverSocket.BeginReceive(tunnel.Data,
                                                0,
                                                tunnel.Data.Length,
                                                SocketFlags.None,
                                                HandleNewDataFromServer,
                                                tunnel);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("{0} Error sending data to SSL host", ex);
            }

        }

        private void HandleNewDataFromServer(IAsyncResult ar)
        {
            try
            {
                TunnelSession tunnel = ar.AsyncState as TunnelSession;
                Trace.Assert(tunnel != null);

                int dataReceived = _serverSocket.EndReceive(ar);

                if (dataReceived > 0)
                {
                    _clientSocket.BeginSend(
                        tunnel.Data, 0, dataReceived, SocketFlags.None, HandleClientSend, tunnel);

                    TunnelSession data2 = new TunnelSession(this);
                    data2.Data = new byte[BufferSize];
                    _serverSocket.BeginReceive(data2.Data,
                                                0,
                                                data2.Data.Length,
                                                SocketFlags.None,
                                                HandleNewDataFromServer,
                                                data2);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("{0} Error receiving data from SSL host", ex);
            }

        }


    }
}
