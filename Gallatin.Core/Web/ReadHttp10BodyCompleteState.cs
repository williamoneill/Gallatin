using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Web
{
    internal class ReadHttp10BodyCompleteState : IHttpStreamParserState
    {
        public ReadHttp10BodyCompleteState()
        {
            WebLog.Logger.Info("Transitioning to read HTTP 1.0 body complete state");
        }

        public void AcceptData( byte[] data )
        {
            throw new InvalidOperationException("Cannot accept data when an HTTP 1.0 connection was closed");
        }
    }
}
