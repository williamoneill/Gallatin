using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Contracts;
using Gallatin.Core.Net;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// 
    /// </summary>
    public interface IRequestContext
    {
        /// <summary>
        /// 
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// 
        /// </summary>
        IHttpRequest OriginatingRequest { get; }

        /// <summary>
        /// Gets a reference to the client session logger
        /// </summary>
        ISessionLogger Logger { get; }
    }
}
