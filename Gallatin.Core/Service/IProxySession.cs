using System;
using System.Diagnostics.Contracts;
using Gallatin.Core.Net;
using Gallatin.Core.Util;

namespace Gallatin.Core.Service
{

    internal interface ISession : IPooledObject
    {
        /// <summary>
        /// Gets the session ID
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Raised when the client session has ended
        /// </summary>
        event EventHandler SessionEnded;

        /// <summary>
        /// Starts the client session
        /// </summary>
        /// <param name="connection">Reference to the client network connection</param>
        void Start(INetworkConnection connection);
    }
    
    



}