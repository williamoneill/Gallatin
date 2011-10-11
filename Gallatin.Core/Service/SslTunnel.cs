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
            TunnelSession sendData = new TunnelSession(this);

            Log.Info( "{0} Starting SSL connection", _sessionId );

            sendData.Data = Encoding.UTF8.GetBytes( string.Format(
                "HTTP/{0} 200 Connection established\r\n" +
                "Proxy-agent: Gallatin-Proxy/1.1\r\n\r\n",
                _httpVersion ) );

            TunnelSession receiveData = new TunnelSession(this);
            receiveData.Data = new byte[BufferSize];
            _clientSocket.BeginReceive( receiveData.Data,
                                        0,
                                        receiveData.Data.Length,
                                        SocketFlags.None,
                                        HandleNewDataFromClient,
                                        receiveData );

            _clientSocket.BeginSend( sendData.Data,
                                     0,
                                     sendData.Data.Length,
                                     SocketFlags.None,
                                     HandleClientSendComplete,
                                     sendData );
        }

        private const int BufferSize = 8000;

        private void HandleClientSendComplete(IAsyncResult ar)
        {
            try
            {
                TunnelSession tunnel = ar.AsyncState as TunnelSession;
                Trace.Assert(tunnel != null);

                SocketError socketError;
                int dataSent = _clientSocket.EndSend(ar, out socketError);

                if (dataSent > 0 && socketError == SocketError.Success)
                {
                    TunnelSession receiveData = new TunnelSession(this);
                    receiveData.Data = new byte[BufferSize];
                    _serverSocket.BeginReceive( receiveData.Data,
                                                0,
                                                receiveData.Data.Length,
                                                SocketFlags.None,
                                                HandleNewDataFromServer,
                                                receiveData );
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("{0} Error sending data to SSL client", ex);
            }
        }

        private void HandleServerSendComplete(IAsyncResult ar)
        {
            try
            {
                TunnelSession tunnel = ar.AsyncState as TunnelSession;
                Trace.Assert(tunnel != null);

                SocketError socketError;
                int dataSent = _serverSocket.EndSend(ar, out socketError);

                if (dataSent > 0 && socketError == SocketError.Success)
                {
                    TunnelSession receiveData = new TunnelSession(this);
                    receiveData.Data = new byte[BufferSize];
                    _clientSocket.BeginReceive(receiveData.Data,
                                                0,
                                                receiveData.Data.Length,
                                                SocketFlags.None,
                                                HandleNewDataFromClient,
                                                receiveData);
                }

            }
            catch (Exception ex)
            {
                Log.Exception("{0} Error sending data to SSL host", ex);
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
                        tunnel.Data, 0, dataReceived, SocketFlags.None, HandleServerSendComplete, tunnel);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("{0} Error receiving data from SSL client", ex);
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
                        tunnel.Data, 0, dataReceived, SocketFlags.None, HandleClientSendComplete, tunnel);
                }

            }
            catch ( Exception ex )
            {
                Log.Exception("{0} Error receiving data from SSL host", ex);
            }

        }


    }
}
