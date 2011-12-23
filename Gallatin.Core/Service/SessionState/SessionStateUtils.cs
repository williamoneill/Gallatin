using System;
using Gallatin.Contracts;

namespace Gallatin.Core.Service.SessionState
{
    internal static class SessionStateUtils
    {
        public static bool TryParseAddress( IHttpRequest e, out string host, out int port )
        {
            const int HttpPort = 80;

            host = e.Headers["Host"];
            port = HttpPort;

            // Get the port from the host address if it set
            string[] tokens = host.Split( ':' );
            if ( tokens.Length == 2 )
            {
                host = tokens[0];

                if(!int.TryParse(tokens[1], out port))
                {
                    return false;
                }
            }

            return !string.IsNullOrEmpty(host) && port > 0;
        }
    }
}