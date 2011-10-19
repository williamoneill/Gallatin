using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{
    public class ServerStream
    {
        private INetworkFacade _client;
        private INetworkFacade _server;
        private bool _isRunning;
        private object _mutex = new object();

        public ServerStream( INetworkFacade client, INetworkFacade server )
        {
            Contract.Requires( client != null );
            Contract.Requires( server != null );

            _isRunning = false;
            _client = client;
            _server = server;

        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant( _client != null );
            Contract.Invariant( _server != null );
        }

        private void ServerReceiveComplete( bool success, byte[] data, INetworkFacade server )
        {
            if ( success )
            {
                if(_isRunning)
                {
                    Log.Verbose("Receiving streaming data from server");
                    _client.BeginSend(data, ClientSendComplete);
                }
            }
            else
            {
                Log.Error( "Failed to receive streaming data from server" );
            }
        }

        private void ClientSendComplete( bool success, INetworkFacade client )
        {
            if ( success )
            {
                if(_isRunning)
                {
                    Log.Verbose("Streaming data sent to client");
                    _server.BeginReceive(ServerReceiveComplete);
                }
            }
            else
            {
                Log.Error( "Unable to stream data to client" );
            }
        }

        public void StartStreaming( byte[] initialData )
        {
            Contract.Requires( initialData != null );
            Contract.Requires( initialData.Length > 0 );

            lock(_mutex)
            {
                if(!_isRunning)
                {
                    _isRunning = true;
                    _client.BeginSend(initialData, ClientSendComplete);
                }
            }
        }

        public void Stop()
        {
            lock(_mutex)
            {
                _isRunning = false;
                _server.CancelPendingReceive();
            }
        }
    }

}
