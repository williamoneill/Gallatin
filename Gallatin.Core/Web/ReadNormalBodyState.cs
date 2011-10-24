using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;

namespace Gallatin.Core.Web
{
    internal class ReadNormalBodyState : IHttpStreamParserState
    {
        private int _contentLength;
        private IHttpStreamParserContext _context;
        private int _dataRemaining;

        public ReadNormalBodyState(IHttpStreamParserContext context, int contentLength )
        {
            Contract.Requires(context != null);
            Contract.Requires(contentLength > 0);

            WebLog.Logger.Verbose("Transitioning to read body state");

            _context = context;
            _dataRemaining = contentLength;
            _contentLength = contentLength;
        }

        public void AcceptData( byte[] data )
        {
            // Partial read or just enough
            if (_dataRemaining >= data.Length)
            {
                // Set this right away so this can be re-entrant
                _dataRemaining -= data.Length;

                _context.AppendBodyData(data);
                _context.OnPartialDataAvailable(data);

                if (_dataRemaining == 0)
                {
                    _context.OnBodyAvailable();
                    _context.OnMessageReadComplete();

                    _context.State = new ReadHeaderState(_context);
                }
                else
                {
                    _context.OnAdditionalDataRequested();
                }

            }

            else
            {
                // Too much data available
                var remainingDataBuffer = data.Take( _dataRemaining ).ToArray();

                // Set this right away so this can be re-entrant
                _dataRemaining = 0;

                _context.AppendBodyData(remainingDataBuffer);
                _context.OnPartialDataAvailable(remainingDataBuffer);
                _context.OnBodyAvailable();
                _context.OnMessageReadComplete();
                _context.State = new ReadHeaderState(_context);

                _context.State.AcceptData( data.Skip(remainingDataBuffer.Length).ToArray() );

            }

        }
    }
}