using System;
using System.Net.Sockets;
using System;
using System.Text;

namespace Gallatin.Core
{
    internal class ClientSession : IClientSession
    {
        public const int BufferSize = 60000;

        public byte[] Buffer
        {
            get; set; 
        }

        // TODO: this breaks encapsulation
        public Socket ActiveSocket { get; set; }

        public HttpMessageParser HttpMessageParser
        {
            get; private set; }

        public Socket ServerSocket { get; set; }

        public ClientSession(Socket clientSocket)
        {
            ClientSocket = clientSocket;
            Buffer = new byte[BufferSize];
            HttpMessageParser = new HttpMessageParser();
            ActiveSocket = ClientSocket;
            CreationTime = DateTime.Now;
            SessionId = Guid.NewGuid();
        }

        internal Socket ClientSocket { get; private set; }

        public void ResetBuffer()
        {
            Buffer = new byte[BufferSize];
            HttpMessageParser = new HttpMessageParser();
        }

        public Guid SessionId { get; set; }

        public void EndSession( bool inError )
        {
            Log.Info("{0} Ending client connection. Error: {1} ", SessionId, inError);

            if( ClientSocket != null )
            {
                if(ClientSocket.Connected)
                {
                    if( inError )
                    {
                        ClientSocket.Send(Encoding.UTF8.GetBytes("HTTP/1.0 500 Internal Server Error\r\nContent-Length: 11\r\n\r\nProxy error"));
                    }

                    ClientSocket.Shutdown(SocketShutdown.Both);
                }

                ClientSocket.Close();
                ClientSocket.Dispose();
            }

            if (ServerSocket != null)
            {
                if(ServerSocket.Connected)
                    ServerSocket.Shutdown(SocketShutdown.Both);

                ServerSocket.Close();
                ServerSocket.Dispose();
            }

            HttpMessageParser = null;
            ActiveSocket = null;
        }

        public System.DateTime CreationTime
        {
            get; private set;
        }

        // TODO: this should get bumped internally as to no break encapsulation
        public System.DateTime LastActivity
        {
            get; set;
        }


        public bool IsActive
        {
            get
            {
                return ClientSocket != null;
            }
        }
    }
}