using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;

namespace Gallatin.Core.Web
{
    internal class ReadChunkedBodyState : IHttpStreamParserState
    {
        const int LengthOfCrLf = 2;

        private IHttpStreamParserContext _context;
        private MemoryStream _body;
        private int _remainingDataNeeded;
        private int _chunkSize;

        public ReadChunkedBodyState(IHttpStreamParserContext context, int chunkSize)
        {
            Contract.Requires(context != null);
            Contract.Requires(chunkSize>0);

            WebLog.Logger.Verbose("Transitioning to read chunked body state. Chunk size = " + chunkSize);

            _remainingDataNeeded = chunkSize + LengthOfCrLf;
            _context = context;
            _body = new MemoryStream(chunkSize);
            _chunkSize = chunkSize;
        }

        public void AcceptData(byte[] buffer)
        {
            _body.Write(buffer,0,buffer.Length);
            _remainingDataNeeded -= buffer.Length;

            if (_remainingDataNeeded <= 0)
            {
                // The chunk will have a terminating CRLF that is not part of the actual body. Trim this
                // before appending to the body. The raw data must keep this intact.

                _context.AppendBodyData( _body.ToArray().Take(_chunkSize).ToArray() );
                _context.OnPartialDataAvailable( _body.ToArray().Take(_chunkSize+LengthOfCrLf).ToArray() );

                _context.State = new ReadChunkedHeaderState(_context);

                if (_remainingDataNeeded < 0)
                {
                    _context.State.AcceptData( _body.ToArray().Skip(_chunkSize+LengthOfCrLf).Take(_remainingDataNeeded * -1).ToArray() );
                }
                else
                {
                    _context.OnAdditionalDataRequested();
                }
            }
            else
            {
                _context.OnAdditionalDataRequested();
            }
        }
    }
}