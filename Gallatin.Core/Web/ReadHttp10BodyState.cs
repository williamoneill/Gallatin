using System;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Web
{
    internal class ReadHttp10BodyState : IHttpStreamParserState
    {
        private readonly IHttpStreamParserContext _context;

        public ReadHttp10BodyState( IHttpStreamParserContext context )
        {
            Contract.Requires( context != null );

            WebLog.Logger.Verbose( "Transitioning to read HTTP 1.0 body state" );

            _context = context;
        }

        #region IHttpStreamParserState Members

        public void AcceptData( byte[] data )
        {
            if (data.Length > 0)
            {
                _context.AppendBodyData(data);
                _context.OnPartialDataAvailable(data);
            }

            _context.OnAdditionalDataRequested();
        }

        #endregion

    }
}