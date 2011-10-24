using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;

namespace Gallatin.Core.Web
{
    internal class ReadChunkedTrailerState : IHttpStreamParserState
    {
        private IHttpStreamParserContext _context;

        private int _consecutiveCrCount;
        private int _consecutiveLfCount;
        private MemoryStream _trailerData = new MemoryStream();

        public ReadChunkedTrailerState( IHttpStreamParserContext context )
        {
            Contract.Requires(context != null);

            WebLog.Logger.Verbose("Transitioning to read chunked trailer state");

            _context = context;
        }

        public void AcceptData( byte[] data )
        {
            // For now, consume and ignore all trailer data
            foreach(byte b in data)
            {
                if( b == '\r' )
                {
                    _consecutiveCrCount++;
                }
                else if(b == '\n')
                {
                    _consecutiveLfCount++;
                }
                else
                {
                    _consecutiveCrCount = 0;
                    _consecutiveLfCount = 0;
                }

                _trailerData.WriteByte(b);

                // If we hit a CRLF without any tailing data then we have reached the end of the chunked data.
                // If there is any trailing data then we need to look for CRLF+CRLF
                if( (_trailerData.Length == 2 && _consecutiveCrCount == 1 && _consecutiveLfCount == 1)
                    || (_trailerData.Length > 2 && _consecutiveCrCount == 2 && _consecutiveLfCount == 2) )
                {
                    _context.OnPartialDataAvailable(_trailerData.ToArray());
                    _context.OnMessageReadComplete();
                    _context.OnBodyAvailable();

                    _context.State = new ReadHeaderState(_context);

                    if(data.Length > _trailerData.Length)
                    {
                        _context.OnPartialDataAvailable(data.Skip((int)_trailerData.Length).Take(data.Length - (int)_trailerData.Length).ToArray());
                    }

                    break;
                }
            }
        }
    }
}