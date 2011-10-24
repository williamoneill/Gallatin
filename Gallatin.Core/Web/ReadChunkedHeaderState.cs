using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Gallatin.Core.Web
{
    internal class ReadChunkedHeaderState : IHttpStreamParserState
    {
        private IHttpStreamParserContext _context;

        private List<byte> _chunkHeader = new List<byte>();

        public ReadChunkedHeaderState(IHttpStreamParserContext context)
        {
            Contract.Requires(context != null);

            WebLog.Logger.Verbose("Transitioning to read chunked header state");

            _context = context;
        }

        public void AcceptData(byte[] buffer)
        {
            const int LastChunkSize = 0;
            const int Undetermined = -1;

            // RFC2616 3.6.1
            //Chunked-Body   = *chunk
            //                last-chunk
            //                trailer
            //                CRLF
            //chunk          = chunk-size [ chunk-extension ] CRLF
            //                chunk-data CRLF
            //chunk-size     = 1*HEX
            //last-chunk     = 1*("0") [ chunk-extension ] CRLF
            //chunk-extension= *( ";" chunk-ext-name [ "=" chunk-ext-val ] )
            //chunk-ext-name = token
            //chunk-ext-val  = token | quoted-string
            //chunk-data     = chunk-size(OCTET)
            //trailer        = *(entity-header CRLF)

            
            int nextChunkSize = Undetermined;

            for(int i = 0; i < buffer.Length && nextChunkSize == -1; i++)
            {
                _chunkHeader.Add(buffer[i]);

                if(_chunkHeader[i] == '\n' && _chunkHeader.Count > 1 && _chunkHeader[i-1] == '\r' )
                {
                    _context.OnPartialDataAvailable(_chunkHeader.ToArray());

                    var rawData = Encoding.UTF8.GetString( _chunkHeader.ToArray() );

                    var tokens = rawData.Split(';');

                    nextChunkSize = int.Parse(tokens[0], NumberStyles.HexNumber);

                    MemoryStream extraData = null;

                    if(buffer.Length > i + 1)
                    {
                        // Pass the remainder of the buffer to the next state
                        extraData = new MemoryStream(buffer, i + 1, buffer.Length - i - 1);
                    }

                    if(nextChunkSize == LastChunkSize)
                    {
                        _context.State = new ReadChunkedTrailerState(_context);
                    }
                    else
                    {
                        _context.State = new ReadChunkedBodyState( _context, nextChunkSize );
                    }

                    if(extraData != null)
                    {
                        _context.State.AcceptData(extraData.ToArray());
                    }
                    else
                    {
                        _context.OnAdditionalDataRequested();
                    }
                }

            }

            if (nextChunkSize == Undetermined)
            {
                _context.OnAdditionalDataRequested();
            }
        }
        
    }
}