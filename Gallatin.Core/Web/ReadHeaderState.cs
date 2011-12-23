using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Gallatin.Core.Web
{
    internal class ReadHeaderState : IHttpStreamParserState
    {
        private readonly IHttpStreamParserContext _context;

        private readonly MemoryStream _headerData = new MemoryStream();
        private MemoryStream _bodyData = new MemoryStream();
        private int _consecutiveCrCount;
        private int _consecutiveLfCount;
        private bool _foundHeaderTerminator;

        public ReadHeaderState( IHttpStreamParserContext context )
        {
            Contract.Requires( context != null );

            WebLog.Logger.Verbose( "Transitioning to read header state" );

            _context = context;
        }

        #region IHttpStreamParserState Members

        public void AcceptData( byte[] data )
        {
            if ( !_foundHeaderTerminator )
            {
                SearchForHeaderTerminator( data );
            }

            if ( _foundHeaderTerminator )
            {
                ParseHeader();
            }
            else
            {
                _context.OnAdditionalDataRequested();
            }
        }

        #endregion

        private void SearchForHeaderTerminator( byte[] data )
        {
            for ( int i = 0; i < data.Length && !_foundHeaderTerminator; i++ )
            {
                if ( data[i] == '\r' )
                {
                    _consecutiveCrCount++;
                }
                else if ( data[i] == '\n' )
                {
                    _consecutiveLfCount++;
                }
                else
                {
                    _consecutiveCrCount = 0;
                    _consecutiveLfCount = 0;
                }

                if ( _consecutiveCrCount == 2
                     && _consecutiveLfCount == 2 )
                {
                    _foundHeaderTerminator = true;

                    _headerData.Write( data, 0, i + 1 );

                    // Write any remaining data to the body memory buffer
                    if ( data.Length
                         > i + 1 )
                    {
                        _bodyData = new MemoryStream( data.Length - i - 1 );
                        _bodyData.Write( data, i + 1, data.Length - i - 1 );
                    }
                }
            }

            if ( !_foundHeaderTerminator )
            {
                _headerData.Write( data, 0, data.Length );
            }

            // TODO: research why this fires the event too many times
            //if (!_foundHeaderTerminator)
            //{
            //    _context.OnAdditionalDataRequested();
            //}
        }

        private void InterpretHttpHeaders( HttpHeaders headers, string httpVersion )
        {
            Contract.Requires(!string.IsNullOrEmpty(httpVersion));

            int length = 0;
            string contentLength = headers["content-length"];
            if ( contentLength != null )
            {
                length = int.Parse( contentLength );
            }

            string transferEncoding = headers["transfer-encoding"];
            if ( transferEncoding != null
                 && transferEncoding.ToLower().Contains( "chunked" ) )
            {
                _context.State = new ReadChunkedHeaderState( _context );

                if ( _bodyData.Length > 0 )
                {
                    _context.State.AcceptData( _bodyData.ToArray() );
                }
                else
                {
                    _context.OnAdditionalDataRequested();
                }
            }
            else if ( length > 0 )
            {
                _context.State = new ReadNormalBodyState( _context, length );

                if ( _bodyData.Length > 0 )
                {
                    _context.State.AcceptData( _bodyData.ToArray() );
                }
                else
                {
                    _context.OnAdditionalDataRequested();
                }
            }
                // HTTP 1.0 assumes non-persistent connection (connection=close unnecessary)
                // HTTP 1.1 assumes persistent connection. Unless the connection is explictly closed, assume no body 
                // if content-length is not specified.
            else if ( contentLength == null && (httpVersion == "1.0" || headers["connection"] == "close" ))
            {
                _context.State = new ReadHttp10BodyState(_context);
                _context.State.AcceptData(_bodyData.ToArray());
            }
            else
            {
                _context.OnMessageReadComplete();

                // 0-byte message. Start reading the next header.
                _context.State = new ReadHeaderState(_context);
                _context.OnAdditionalDataRequested();
            }
        }

        private void ParseHeader()
        {
            _headerData.Position = 0;

            string header = Encoding.UTF8.GetString( _headerData.ToArray() );

            WebLog.Logger.Verbose( "Original HTTP header\r\n " + header );

            string[] headerLines = header.Split( new[]
                                                 {
                                                     "\r\n"
                                                 },
                                                 StringSplitOptions.RemoveEmptyEntries );

            var headers = CreateHeaders( headerLines );

            bool parseOk = false;
            string httpVersion = null;

            const int TokensInHttpVersionString = 2;

            // Response?
            if (headerLines[0].StartsWith("HTTP"))
            {
                // Typical line will look like "HTTP/1.1 200 OK somthing else"
                string[] tokens = headerLines[0].Split( ' ' );

                const int MinTokensInResponse = 2;
                if (tokens.Length >= MinTokensInResponse)
                {
                    int statusCode;
                    if (int.TryParse(tokens[1], out statusCode))
                    {
                        string[] versionTokens = tokens[0].Split('/');
                        if (versionTokens.Length == TokensInHttpVersionString)
                        {
                            parseOk = true;

                            string statusString = "";
                            if(tokens.Length > MinTokensInResponse )
                            {
                                for(int i = MinTokensInResponse; i< tokens.Length; i++)
                                {
                                    statusString += tokens[i] + " ";
                                }
                            }

                            httpVersion = versionTokens[1];

                            _context.OnReadResponseHeaderComplete(
                                httpVersion,
                                headers,
                                statusCode,
                                statusString.Trim() );
                        }
                        
                    }


                    
                }
            }
            else
            {
                // Typcial line will look like "GET / HTTP/1.1"
                string[] tokens = headerLines[0].Split( ' ' );
                const int TokensInValidHttpRequestLine = 3;
                if (tokens.Length == TokensInValidHttpRequestLine)
                {
                    string[] versionTokens = tokens[2].Split( '/' );

                    if(versionTokens.Length == TokensInHttpVersionString)
                    {
                        parseOk = true;

                        httpVersion = versionTokens[1];

                        _context.OnReadRequestHeaderComplete(
                            httpVersion,
                            headers,
                            tokens[0],
                            tokens[1]);
                    }
                }
            }

            if(!parseOk)
            {
                    throw new ArgumentException(
                        "The HTTP data could not be identified as a request or response.");
                
            }

            InterpretHttpHeaders( headers, httpVersion );
        }

        private static HttpHeaders CreateHeaders( string[] headerLines )
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>( headerLines.Length - 1 );

            for ( int i = 1; i < headerLines.Length; i++ )
            {
                if ( headerLines[i].Trim().Length > 0 )
                {
                    int index = headerLines[i].IndexOf( ':' );
                    if ( index == -1 )
                    {
                        throw new ArgumentException( "HTTP header line invalid or malformed: " + headerLines[i] );
                    }

                    pairs.Add( new KeyValuePair<string, string>(
                                   headerLines[i].Substring( 0, index ),
                                   headerLines[i].Substring( index + 1 ).Trim() ) );
                }
            }

            HttpHeaders headers = new HttpHeaders( pairs );
            return headers;
        }
    }
}