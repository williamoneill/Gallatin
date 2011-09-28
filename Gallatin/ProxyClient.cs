using System.IO;
using System.Net.Sockets;

namespace Gallatin.Core
{
    public class ProxyClient
    {
        internal ProxyClient()
        {
            Buffer = new byte[BufferSize];
            ContentStream = new MemoryStream();
            
        }

        public ProxyClient( Socket clientSocket ) : this()
        {
            ClientSocket = clientSocket;
        }

        public Socket ClientSocket { get; private set; }

        public MemoryStream ContentStream { get; private set; }

        public byte[] Buffer { get; private set; }

        public static readonly int BufferSize = 10000;
    }
}