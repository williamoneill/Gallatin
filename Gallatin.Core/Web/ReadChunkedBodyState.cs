using System;
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

        public void AcceptData(byte[] data)
        {
            _body.Write(data,0,data.Length);
            _remainingDataNeeded -= data.Length;

            if (_remainingDataNeeded <= 0)
            {
                // The chunk will have a terminating CRLF that is not part of the actual body. Trim this
                // before appending to the body. The raw data must keep this intact.

                var sourceArray = _body.ToArray();

                byte[] buffer = new byte[_chunkSize];
                Array.Copy( sourceArray, buffer, _chunkSize );
                _context.AppendBodyData( buffer );

                buffer = new byte[_chunkSize + LengthOfCrLf];
                Array.Copy(sourceArray, buffer, _chunkSize + LengthOfCrLf);
                _context.OnPartialDataAvailable( buffer );

                _context.State = new ReadChunkedHeaderState(_context);

                if (_remainingDataNeeded < 0)
                {
                    buffer = new byte[_remainingDataNeeded * -1];
                    Array.Copy(sourceArray, _chunkSize + LengthOfCrLf, buffer, 0, buffer.Length);
                    _context.State.AcceptData( buffer );
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