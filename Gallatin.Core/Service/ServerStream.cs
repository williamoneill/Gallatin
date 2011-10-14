﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    public class ServerStream
    {
        private Socket _clientSocket;
        private Socket _serverSocket;
        private Guid _sessionId;
        private byte[] _buffer = new byte[8000];
        private byte[] _initialData;
        private int? _dataLength;
        private int totalBytesSent;

        public void Stop()
        {
            _serverSocket.EndReceive( _serverSocketBeginReceiveAsyncResult );
        }

        public void Start()
        {
            _clientSocket.BeginSend(_initialData,
                                     0,
                                     _initialData.Length,
                                     SocketFlags.None,
                                     HandleDataSentToClient,
                                     this);
            
        }

        public ServerStream(  Socket clientSocket, Socket serverSocket, Guid sessionId, byte[] initialData, int? dataLength )
        {
            _clientSocket = clientSocket;
            _serverSocket = serverSocket;
            _sessionId = sessionId;
            _initialData = initialData;
            _dataLength = dataLength;
        }

        private void HandleDataSentToClient(IAsyncResult ar)
        {
            ServerStream serverStream = ar.AsyncState as ServerStream;
            Trace.Assert(serverStream != null);

            try
            {
                SocketError error;
                int dataSent = _clientSocket.EndSend(ar, out error);
                if( dataSent == 0 )
                {
                    Log.Info("{0} Client disconnected while server was streaming data", _sessionId);
                }
                else if( error != SocketError.Success )
                {
                    Log.Error("{0} An error was encountered when attempting to stream data to client", _sessionId );
                }
                else
                {
                    if (_dataLength.HasValue)
                    {
                        totalBytesSent += dataSent;
                    }

                    if( _dataLength.HasValue && totalBytesSent >= _dataLength.Value )
                    {
                        _clientSocket.Close();
                    }
                    else
                    {
                        // Get more data from the server
                        _serverSocketBeginReceiveAsyncResult =
                            _serverSocket.BeginReceive(_buffer,
                                                    0,
                                                    _buffer.Length,
                                                    SocketFlags.None,
                                                    HandleDataFromServer,
                                                    this);
                    }

                }

            }
            catch ( Exception ex)
            {
                Log.Exception( string.Format( "{0} Failed to stream server data to client", _sessionId), ex);
            }
        }

        private IAsyncResult _serverSocketBeginReceiveAsyncResult;

        private void HandleDataFromServer(IAsyncResult ar)
        {
            ServerStream serverStream = ar.AsyncState as ServerStream;
            Trace.Assert(serverStream != null);

            try
            {
                int bytesReceived = serverStream._serverSocket.EndReceive( ar );
                if(bytesReceived == 0)
                {
                    Log.Info("{0} Client terminated session", _sessionId);
                }
                else
                {
                    _clientSocket.BeginSend( _buffer,
                                             0,
                                             bytesReceived,
                                             SocketFlags.None,
                                             HandleDataSentToClient,
                                             this );
                }
            }
            catch ( Exception ex )
            {
                Log.Exception(string.Format("{0} Failed to receive data from server", _sessionId), ex);
            }   
        }
    }
}