using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Gallatin.Core
{
    public class HttpStreamEvaluator
    {
        public enum HttpType
        {
            Response,
            Request
        }

        private Stream _stream;

        private long? _headerIndex = null;
        private bool _hasParsedHeader = false;
        public string Url { get; private set; }
        public string Version { get; private set; }
        public HttpType MessageType { get; private set; }
        public int? StatusCode { get; private set; }
        public string StatusMessage { get; private set; }
        public string RequestMethod { get; private set; }
        public string RequestUri { get; private set; }

        public HttpStreamEvaluator( Stream stream )
        {
            _stream = stream;
        }

        public bool IsComplete
        {
            get; private set;
        }

        public bool ShouldCloseConnectionWithServer { get; private set; }

        public List<KeyValuePair<string, string>> Headers { get; private set; }

        private void FindIndexOfHeaderTerminator()
        {
            if (!_headerIndex.HasValue)
            {
                _stream.Position = 0;

                int data = 0;

                int crCount = 0;
                int lfCount = 0;

                do
                {
                    data = _stream.ReadByte();

                    if (data == '\r')
                    {
                        crCount++;
                    }
                    else if (data == '\n' && crCount - lfCount == 1)
                    {
                        lfCount++;
                    }
                    else
                    {
                        crCount = lfCount = 0;
                    }
                }
                while (data != -1 && crCount != 2 && lfCount != 2);

                if (data != -1)
                    _headerIndex = _stream.Position;
            }
        }

        private static readonly Regex _splitReponseHeader =
            new Regex(@"HTTP/(?<version>\d.\d)\s*(?<code>\d*)\s(?<status>.*)$");

        private static readonly Regex _splitRequestHeader =
            new Regex(@"(?<verb>\w*)\s*(?<destination>\S*)\sHTTP/(?<version>.*)$");


        private void ParseHeader()
        {
            if (!_hasParsedHeader)
            {
                _stream.Position = 0;

                StreamReader reader = new StreamReader(_stream);

                string line;

                bool readFirstLine = false;

                this.Headers = new List<KeyValuePair<string, string>>();

                while( !string.IsNullOrEmpty((line = reader.ReadLine())) )
                {
                    if( !readFirstLine )
                    {
                        readFirstLine = true;

                        Match httpResponseMatch;
                        Match httpRequestMatch;

                        if( ( httpResponseMatch = _splitReponseHeader.Match(line)).Success )
                        {
                            this.MessageType = HttpType.Response;
                            Version = httpResponseMatch.Groups["version"].ToString();
                            this.StatusCode = int.Parse( httpResponseMatch.Groups["code"].ToString() );
                            this.StatusMessage = httpResponseMatch.Groups["status"].ToString();

                        }
                        else if( (httpRequestMatch = _splitRequestHeader.Match( line ) ).Success )
                        {
                            this.MessageType = HttpType.Request;
                            Version = httpRequestMatch.Groups["version"].ToString();
                            this.RequestMethod = httpRequestMatch.Groups["verb"].ToString();
                            RequestUri = httpRequestMatch.Groups["destination"].ToString();

                        }
                        else
                        {
                            throw new ArgumentException(
                                "The first line of the HTTP header was invalid" );
                        }
                    }
                    else
                    {
                        string[] tokens = line.Split( ':' );

                        if(tokens.Count() != 2)
                            throw new ArgumentException("HTTP header was invalid");

                        this.Headers.Add( new KeyValuePair<string, string>(tokens[0].Trim(), tokens[1].Trim()) );
                    }
                }
            }
        }

        private void DetermineIfSocketShouldClose()
        {
            KeyValuePair<string, string> contentLength =
                 this.Headers.SingleOrDefault(
                    s =>
                    s.Key.Equals( "Content-Length", StringComparison.InvariantCultureIgnoreCase ) ) ;

            if( !contentLength.Equals( default( KeyValuePair<string,string>) ) )
            {
                if ( int.Parse( contentLength.Value ) == _stream.Length - this._headerIndex.Value )
                {
                    this.ShouldCloseConnectionWithServer = true;
                    this.IsComplete = true;
                }
            }
            else
            {
                // Evaluate chunked data
                var transferEncoding =
                    this.Headers.SingleOrDefault(
                        s =>
                        s.Key.Equals( "Transfer-Encoding",
                                      StringComparison.InvariantCultureIgnoreCase )
                        && s.Value.Equals( "chunked", StringComparison.InvariantCultureIgnoreCase ) );

                if( !transferEncoding.Equals(default( KeyValuePair<string,string>)) )
                {
                    
                }
            }



            // TODO: evaluate chunked data
        }

        public void Evaluate()
        {
            long startingPostion = _stream.Position;

            try
            {
                FindIndexOfHeaderTerminator();

                if( _headerIndex.HasValue )
                {
                    ParseHeader();

                    
                }
            }
            finally
            {
                _stream.Position = startingPostion;
            }
        }
    }
}
