using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Gallatin.Core.Web
{
    internal class ReadHeaderState : IHttpStreamParserState
    {
        private MemoryStream _headerData = new MemoryStream();
        private MemoryStream _bodyData = new MemoryStream();
        private int _consecutiveCrCount;
        private int _consecutiveLfCount;
        private bool _foundHeaderTerminator;

        private static Regex _crlfRegex = new Regex("\r\n");
        private static readonly Regex _splitReponseHeader =
            new Regex(@"HTTP/(?<version>\d.\d)\s*(?<statuscode>\d*)\s(?<status>.*)$");

        private static readonly Regex _splitRequestHeader =
            new Regex(@"(?<method>\w*)\s*(?<destination>\S*)\sHTTP/(?<version>.*)$");

        private void SearchForHeaderTerminator(byte[] data)
        {
            for (int i = 0; i < data.Length && !_foundHeaderTerminator; i++)
            {
                if (data[i] == '\r')
                {
                    _consecutiveCrCount++;
                }
                else if (data[i] == '\n')
                {
                    _consecutiveLfCount++;
                }
                else
                {
                    _consecutiveCrCount = 0;
                    _consecutiveLfCount = 0;
                }

                _headerData.WriteByte(data[i]);

                if (_consecutiveCrCount == 2
                    && _consecutiveLfCount == 2)
                {
                    _foundHeaderTerminator = true;

                    // Write any remaining data to the body memory buffer
                    if (data.Length > i + 1)
                    {
                        _bodyData = new MemoryStream( data.Length - i - 1 );
                        _bodyData.Write(data, i + 1, data.Length - i - 1);
                    }
                }
            }

            // TODO: research why this fires the event too many times
            //if (!_foundHeaderTerminator)
            //{
            //    _context.OnAdditionalDataRequested();
            //}
        }

        private void InterpretHttpHeaders(HttpHeaders headers)
        {
            int length = 0;
            string contentLength = headers["content-length"];
            if (contentLength != null)
            {
                length = int.Parse(contentLength);
            }

            string transferEncoding = headers["transfer-encoding"];
            if (transferEncoding != null && transferEncoding.ToLower().Contains("chunked"))
            {
                _context.State = new ReadChunkedHeaderState(_context);
                
                if( _bodyData.Length > 0 )
                {
                    _context.State.AcceptData( _bodyData.ToArray() );
                }
                else
                {
                    _context.OnAdditionalDataRequested();
                }
            }
            else if(length > 0)
            {
                _context.State = new ReadNormalBodyState(_context, length);
                
                if( _bodyData.Length > 0 )
                {
                    _context.State.AcceptData( _bodyData.ToArray() );
                }
                else
                {
                    _context.OnAdditionalDataRequested();
                }
            }
            else
            {
                _context.OnMessageReadComplete();

                // 0-byte message. Start reading the next header.
                _context.State = new ReadHeaderState(_context);
            }

        }

        private void ParseHeader()
        {
            _headerData.Position = 0;


            string header = Encoding.UTF8.GetString( _headerData.ToArray() );

            WebLog.Logger.Verbose("HTTP header = " + header);

            string[] headerLines = _crlfRegex.Split( header );
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>(headerLines.Length - 1);

            for (int i = 1; i < headerLines.Length; i++)
            {
                if (headerLines[i].Trim().Length > 0)
                {
                    int index = headerLines[i].IndexOf(':');
                    if (index == -1)
                    {
                        throw new ArgumentException("HTTP header line invalid or malformed: " + headerLines[i]);
                    }

                    pairs.Add(new KeyValuePair<string, string>(
                                    headerLines[i].Substring(0, index),
                                    headerLines[i].Substring(index + 1).Trim()));
                }
            }

            HttpHeaders headers = new HttpHeaders(pairs);

            Match httpResponseMatch = _splitReponseHeader.Match(headerLines[0]);

            if (httpResponseMatch.Success)
            {
                _context.OnReadResponseHeaderComplete( 
                    httpResponseMatch.Groups["version"].ToString(),
                    headers,
                    int.Parse(
                        httpResponseMatch.Groups["statuscode"].
                            ToString()),
                    httpResponseMatch.Groups["status"].ToString());
            }
            else
            {
                Match httpRequestMatch = _splitRequestHeader.Match(headerLines[0]);

                if (!httpRequestMatch.Success)
                {
                    throw new ArgumentException(
                        "The HTTP data could not be identified as a request or response.");
                }

                _context.OnReadRequestHeaderComplete(
                    httpRequestMatch.Groups["version"].ToString(),
                    headers,
                    httpRequestMatch.Groups["method"].ToString(),
                    httpRequestMatch.Groups["destination"].ToString());
            }

            InterpretHttpHeaders(headers);
            
        }



        private IHttpStreamParserContext _context;

        public ReadHeaderState(IHttpStreamParserContext context)
        {
            Contract.Requires(context!=null);

            WebLog.Logger.Verbose("Transitioning to read header state");

            _context = context;
        }

        public void AcceptData( byte[] data )
        {
            if (!_foundHeaderTerminator)
            {
                SearchForHeaderTerminator(data);
            }

            if (_foundHeaderTerminator)
            {
                ParseHeader();
            }
            else
            {
                _context.OnAdditionalDataRequested();
            }
        }
    }
}