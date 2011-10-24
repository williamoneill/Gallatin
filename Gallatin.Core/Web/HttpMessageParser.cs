
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gallatin.Core.Util;

namespace Gallatin.Core.Web
{
    /// <summary>
    /// 	This is a stateful class that evaluates the raw network stream to create an <see cref = "IHttpMessage" />.
    /// </summary>
    public class HttpMessageParser : IHttpMessageParser
    {
        private const int LengthOfCrlf = 2;

        private static readonly Regex _splitReponseHeader =
            new Regex( @"HTTP/(?<version>\d.\d)\s*(?<statuscode>\d*)\s(?<status>.*)$" );

        private static readonly Regex _splitRequestHeader =
            new Regex( @"(?<method>\w*)\s*(?<destination>\S*)\sHTTP/(?<version>.*)$" );

        private List<byte> _rawData = new List<byte>(60000);
        private int? _index;
        private byte[] _body;
        private List<byte> _combinedChunkedData;
        private IHttpMessage _completeMessage;
        private bool? _hasBody;

        private List<List<byte>> _headerLines;
        private List<KeyValuePair<string, string>> _headerPairs;

        public HttpMessageParser()
        {
        }

        #region IHttpMessageParser Members

        /// <summary>
        /// Returns the message header only. The body may be invalid at this point.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool TryGetHeader( out IHttpMessage message )
        {
            lock(_mutex)
            {
                message = null;

                // TODO: make the header a subclass of HttpMessage. This will save multiple parsings...
                if (_index.HasValue)
                {
                    message = CreateMessage();
                }

                return message != null;
            }

        }

        /// <summary>
        /// Gets an array of the raw data that has been appended to the internal buffer
        /// </summary>
        public byte[] RawData
        {
            get
            {
                return _rawData.ToArray();
            }
        }

        /// <summary>
        /// 	The intent of this method is to attempt to create an HTTP message
        /// 	from the raw data received over the network.
        /// </summary>
        /// <param name = "rawNetworkContent"></param>
        /// <returns></returns>
        public IHttpMessage AppendData( IEnumerable<byte> rawNetworkContent )
        {
            lock(_mutex)
            {
                _completeMessage = null;

                _rawData.AddRange(rawNetworkContent);

                if (FindHeaderLines())
                {
                    PopulateHeaderPairs();

                    if (IsReceiveComplete())
                    {
                        _completeMessage = CreateMessage();
                    }
                }

                return _completeMessage;
                
            }

        }

        #endregion

        private object _mutex = new object();

        public void Reset()
        {
            lock(_mutex)
            {
                Log.Logger.Verbose("Resetting HttpMessageParser");

                _rawData.Clear();
                
                _index = null;
                
                _body = null;

                if (_combinedChunkedData != null)
                    _combinedChunkedData.Clear();

                _completeMessage = null;
                _hasBody = null;
                _headerLines = null;
                _headerPairs = null;

                if (_headerLines != null)
                    _headerLines.Clear();

                if (_headerPairs != null)
                    _headerPairs.Clear();
            }
        }

        public bool TryGetCompleteMessage( out IHttpMessage message )
        {
            lock(_mutex)
            {
                message = _completeMessage;

                return _completeMessage != null;
                
            }
        }

        // TODO: update unit tests
        public bool TryGetCompleteResponseMessage(out IHttpResponseMessage message)
        {
            lock(_mutex)
            {
                if(_completeMessage != null && _completeMessage is IHttpResponseMessage)
                {
                    message = _completeMessage as IHttpResponseMessage;
                    return true;
                }

                message = null;
                return false;
            }
        }

        public bool TryGetCompleteRequestMessage(out IHttpRequestMessage message)
        {
            lock (_mutex)
            {
                if (_completeMessage != null && _completeMessage is IHttpRequestMessage)
                {
                    message = _completeMessage as IHttpRequestMessage;
                    return true;
                }

                message = null;
                return false;
            }
        }

        public byte[] AllData
        {
            get
            {
                return _rawData.ToArray();
            }
        }

        private int FindLengthOfLine( int startingIndex )
        {
            const int NotFound = -1;

            int index = startingIndex;
            int length = NotFound;

            while ( _rawData.Count - LengthOfCrlf >= index
                    && length == NotFound )
            {
                if ( _rawData[index] == '\r'
                     && _rawData[index + 1] == '\n' )
                {
                    length = index - startingIndex;
                }

                index++;
            }

            return length;
        }

        private bool FindHeaderLines()
        {
            // Return if we already read the complete header
            if ( _index.HasValue )
            {
                return true;
            }

            List<List<byte>> lines = new List<List<byte>>();

            int startIndex = 0;
            bool headerIncomplete = false;

            while ( !_index.HasValue
                    && !headerIncomplete )
            {
                int length = FindLengthOfLine( startIndex );

                if ( length == 0 )
                {
                    _index = startIndex + LengthOfCrlf;
                }
                else if ( length != -1 )
                {
                    lines.Add( _rawData.GetRange( startIndex, length ) );

                    startIndex += length + LengthOfCrlf;
                }
                else
                {
                    headerIncomplete = true;
                }
            }

            if ( _index.HasValue )
            {
                _headerLines = lines;
            }

            return _index.HasValue;
        }

        private IHttpMessage CreateMessage()
        {
            IHttpMessage message;

            // Determine what type of message we are creating based on the first header line
            string headerLine = Encoding.UTF8.GetString( _headerLines[0].ToArray() );

            Match httpResponseMatch = _splitReponseHeader.Match( headerLine );

            if ( httpResponseMatch.Success )
            {
                message = new HttpResponseMessage( _body,
                                                   httpResponseMatch.Groups["version"].ToString(),
                                                   _headerPairs,
                                                   int.Parse(
                                                       httpResponseMatch.Groups["statuscode"].
                                                           ToString() ),
                                                   httpResponseMatch.Groups["status"].ToString() );
            }
            else
            {
                Match httpRequestMatch = _splitRequestHeader.Match( headerLine );

                if ( !httpRequestMatch.Success )
                {
                    throw new ArgumentException(
                        "The HTTP data could not be identified as a request or response." );
                }

                Uri baseUri =
                    new Uri( "http://"
                             +
                             _headerPairs.Find(
                                 s =>
                                 s.Key.Equals( "host", StringComparison.InvariantCultureIgnoreCase ) )
                                 .Value );
                Uri uri = new Uri( baseUri, httpRequestMatch.Groups["destination"].ToString() );

                message = new HttpRequestMessage( _body,
                                                  httpRequestMatch.Groups["version"].ToString(),
                                                  _headerPairs,
                                                  httpRequestMatch.Groups["method"].ToString(),
                                                  uri );
            }

            return message;
        }

        private void PopulateHeaderPairs()
        {
            // Return if we already parsed the pairs
            if ( _headerPairs == null )
            {
                Trace.Assert( _headerLines != null );

                _headerPairs = new List<KeyValuePair<string, string>>();

                // Skip the first header line. It is not a key/value pair.
                foreach ( List<byte> line in _headerLines.Skip( 1 ) )
                {
                    string decodedLine = Encoding.UTF8.GetString( line.ToArray() );

                    int indexOfFirstColon = decodedLine.IndexOf( ':' );

                    if ( indexOfFirstColon == -1 )
                    {
                        throw new ArgumentException( "Invalid HTTP header declaration" );
                    }

                    _headerPairs.Add(
                        new KeyValuePair<string, string>(
                            decodedLine.Substring( 0, indexOfFirstColon ).Trim(),
                            decodedLine.Substring( indexOfFirstColon + 1 ).Trim() ) );
                }
            }
        }

        private bool IsReceiveComplete()
        {
            bool hasReceivedCompleteMessage = false;

            // Short-circuit evaluation. We've already determined there is no body.
            if ( _hasBody.HasValue
                 && !_hasBody.Value )
            {
                hasReceivedCompleteMessage = true;
            }
            else
            {
                // TODO: research pre/post asserts in .NET 4
                Trace.Assert( _headerPairs != null );
                Trace.Assert( _index.HasValue );

                KeyValuePair<string, string> transferEncoding =
                    _headerPairs.Where(
                        s =>
                        s.Key.Equals( "transfer-encoding",
                                      StringComparison.InvariantCultureIgnoreCase ) ).
                        SingleOrDefault();

                KeyValuePair<string, string> contentLength =
                    _headerPairs.Where(
                        s =>
                        s.Key.Equals( "content-length", StringComparison.InvariantCultureIgnoreCase )
                        && !s.Value.Equals( "0" ) )
                        .SingleOrDefault();

                // RFC 2616 4.4.3 - use transfer-encoding over content-length if both
                // are supplied
                if ( transferEncoding.Equals( default( KeyValuePair<string, string> ) )
                     || !transferEncoding.Value.ToLowerInvariant().Contains( "chunked" ) )
                {
                    // Contains neither chunked data or content-length. Don't call Grissom, there is no body.
                    if ( contentLength.Equals( default( KeyValuePair<string, string> ) ) )
                    {
                        _hasBody = false;
                        hasReceivedCompleteMessage = true;
                    }
                    else
                    {
                        _hasBody = true;

                        int contentLengthValue = int.Parse( contentLength.Value );

                        if ( _rawData.Count
                             == _index + contentLengthValue )
                        {
                            hasReceivedCompleteMessage = true;
                            _body =
                                _rawData.GetRange( _index.Value, contentLengthValue )
                                    .ToArray();
                        }
                    }
                }
                else
                {
                    _hasBody = true;

                    hasReceivedCompleteMessage = CheckChunkedMessage();

                    if ( hasReceivedCompleteMessage )
                    {
                        // RFC 2616 14.41 - proxy must removed transfer-encoding
                        _headerPairs.Remove( transferEncoding );

                        // The content-length header may exist with the transfer-encoding. If so, remove it and add
                        // a new header that reflects the length of the un-chunked data.
                        if ( !contentLength.Equals( default( KeyValuePair<string, string> ) ) )
                        {
                            _headerPairs.Remove( contentLength );
                        }

                        _headerPairs.Add( new KeyValuePair<string, string>( "Content-Length",
                                                                            _combinedChunkedData.
                                                                                Count.
                                                                                ToString() ) );

                        _body = _combinedChunkedData.ToArray();
                    }
                }
            }

            return hasReceivedCompleteMessage;
        }

        private bool CheckChunkedMessage()
        {
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

            const int LastChunk = 0;

            bool hasReceivedCompleteChunkedMessage = false;

            int chunkSize = -1;

            _combinedChunkedData = new List<byte>();

            // Start of message body
            int startingIndex = _index.Value;

            // Find first chunk header
            int length = FindLengthOfLine( startingIndex );

            while ( length != -1
                    && chunkSize != LastChunk )
            {
                string chunkDataHeader =
                    Encoding.UTF8.GetString( _rawData.Skip( startingIndex ).Take( length ).ToArray() );

                // Ignore possible chunk-extension starting at ';'
                string[] tokens = chunkDataHeader.Split( ';' );

                // Strange that the content-length is in base 10, but chunked data length is in hex.
                chunkSize = int.Parse( tokens[0], NumberStyles.HexNumber );

                if ( chunkSize != LastChunk )
                {
                    // Do we have enough data for the next chunk?
                    if ( _rawData.Count
                         >= startingIndex + length + LengthOfCrlf + chunkSize )
                    {
                        startingIndex += length + LengthOfCrlf;

                        _combinedChunkedData.AddRange(
                            _rawData.Skip( startingIndex ).Take( chunkSize ) );

                        startingIndex += chunkSize + LengthOfCrlf;
                        length = FindLengthOfLine( startingIndex );
                    }
                    else
                    {
                        // Not enough data yet. Break out of the loop.
                        length = -1;
                    }
                }

                hasReceivedCompleteChunkedMessage = chunkSize == LastChunk;
            }

            return hasReceivedCompleteChunkedMessage;
        }
    }
}