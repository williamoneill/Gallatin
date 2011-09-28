using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Gallatin.Core
{
    public class HttpContentParser
    {
        private const int NaN = -1;
        private const char UNIVERSAL_NEW_LINE = '\n';
        private const char NIX_NEW_LINE = '\r';

        private static readonly Regex _splitReponseHeader =
            new Regex( @"HTTP/(?<version>\d.\d)\s*(?<code>\d*)\s(?<status>.*)$" );

        private static readonly Regex _splitRequestHeader =
            new Regex( @"(?<verb>\w*)\s*(?<destination>\S*)\sHTTP/(?<version>.*)$" );

        /// <summary>
        /// 	Parses the stream containing raw HTTP data and creates an instance of the <see cref = "HttpData" />
        /// 	class based on if the data is a HTTP request or response.
        /// </summary>
        /// <param name = "httpContent"></param>
        /// <param name = "data"></param>
        /// <returns></returns>
        public static bool TryParse( Stream httpContent, out HttpMessageOld message )
        {
            long streamEnd = httpContent.Position;
            httpContent.Position = 0;
            message = null;

            try
            {
                int headerTerminatorIndex = LocateHeaderTerminatorIndex( httpContent );

                if ( headerTerminatorIndex != NaN )
                {
                    string headerText = GetHeaderText( httpContent, headerTerminatorIndex );

                    string[] headerLines = SplitHeaderLines( headerText );

                    message = CreateMessage( headerLines[0] );

                    message.OriginalStream = httpContent;

                    PopulateHeaderPairs( message, headerLines );

                    long contentLength = CalculateContentLength( message );

                    if(contentLength > 0 )
                    {
                        bool hasReceivedEntireMessage = SetMessageBody( httpContent, message, headerTerminatorIndex, contentLength );

                        if (!hasReceivedEntireMessage)
                            message = null;
                    }


                }
            }
            catch ( Exception ex )
            {
                Trace.TraceError( "An exception was encountered while parsing HTTP content. "
                                  + ex.Message );
                message = null;
            }
            finally
            {
                httpContent.Position = streamEnd;
            }

            return message != null;
        }


        private static bool SetMessageBody( Stream httpContent,
                                            HttpMessageOld message,
                                            int headerTerminatorIndex,
                                            long contentLength )
        {
            if ( httpContent.Length
                 >= headerTerminatorIndex + contentLength )
            {
                if ( contentLength > 0 )
                {
                    httpContent.Position = headerTerminatorIndex + 1;

                    message.Body = new byte[contentLength];

                    // TODO: fix this to work with longs
                    if ( contentLength > int.MaxValue )
                    {
                        throw new InternalBufferOverflowException(
                            "Exceeded available HTTP body buffer" );
                    }

                    httpContent.Read( message.Body, 0, (int) contentLength );
                }
                return true;
            }

            return false;
        }

        private static long CalculateContentLength( HttpMessageOld message )
        {
            const string HTTP_CONTENT_LENGTH_KEY = "Content-Length";

            long contentLength = 0;

            //TODO: use first or default here
            IEnumerable<KeyValuePair<string, string>> contentLengthPair =
                ( from s in message.HeaderPairs
                  where s.Key == HTTP_CONTENT_LENGTH_KEY
                  select s );

            if ( contentLengthPair.Count() > 0 )
            {
                if ( contentLengthPair.Count() > 1 )
                {
                    throw new ArgumentException( "Ambiguous content length in HTTP header" );
                }

                contentLength = long.Parse( contentLengthPair.First().Value );
            }
            return contentLength;
        }

        private static void PopulateHeaderPairs( HttpMessageOld message, string[] headerLines )
        {
            message.HeaderPairs = new List<KeyValuePair<string, string>>(headerLines.Count());

            foreach ( string line in headerLines.Skip( 1 ) )
            {
                if ( line.Trim().Length > 0 )
                {
                    int seperatorIndex = line.IndexOf( ':' );

                    if ( seperatorIndex == NaN )
                    {
                        throw new InvalidDataException( "HTTP header/pair corrupt" );
                    }

                    message.HeaderPairs.Add(
                        new KeyValuePair<string, string>( line.Substring( 0, seperatorIndex ),
                                                          line.Substring( seperatorIndex + 1 ).Trim() ) );
                }
            }
        }

        private static HttpMessageOld CreateMessage( string firstHeaderLine )
        {
            HttpMessageOld message = null;


            // TODO: revisit and use windows code page
            string utf7 = Encoding.ASCII.GetString( Encoding.UTF8.GetBytes( firstHeaderLine ) );

            Match httpResponseMatch = _splitReponseHeader.Match(utf7);

            if ( httpResponseMatch.Success )
            {
                HttpResponse response = new HttpResponse();

                response.ResponseCode = int.Parse( httpResponseMatch.Groups["code"].ToString() );
                response.Status = httpResponseMatch.Groups["status"].ToString();
                response.Version = httpResponseMatch.Groups["version"].ToString();

                message = response;
            }
            else
            {
                Match httpRequestMatch = _splitRequestHeader.Match(utf7);

                if (!httpRequestMatch.Success)
                {
                    throw new ArgumentException(
                        "The HTTP data could not be identified as a request or response." );
                }

                HttpRequest request = new HttpRequest();

                request.DestinationAddress = httpRequestMatch.Groups["destination"].ToString();
                request.Version = httpRequestMatch.Groups["version"].ToString();
                request.RequestType = (HttpActionType) Enum.Parse( typeof (HttpActionType),
                                                                   httpRequestMatch.Groups["verb"].
                                                                       ToString(),
                                                                   true );

                message = request;
            }

            return message;
        }

        private static string[] SplitHeaderLines( string headerText )
        {
            const int MIN_NUM_HEADER_LINES = 2;

            string[] headerLines = headerText.Split(
                new string[]
                {
                    NIX_NEW_LINE.ToString() + UNIVERSAL_NEW_LINE.ToString(), 
                    UNIVERSAL_NEW_LINE.ToString()
                },
                StringSplitOptions.RemoveEmptyEntries );

            if ( headerLines.Count() < MIN_NUM_HEADER_LINES )
            {
                throw new ArgumentException( "Invalid HTTP header. Does not contain expected data." );
            }

            return headerLines;
        }

        private static string GetHeaderText( Stream httpContent, int headerTerminatorIndex )
        {
            byte[] rawHeader = new byte[headerTerminatorIndex];

            httpContent.Position = 0;

            httpContent.Read( rawHeader, 0, headerTerminatorIndex );

            return Encoding.UTF8.GetString( rawHeader );
        }

        private static int LocateHeaderTerminatorIndex( Stream httpContent )
        {
            httpContent.Position = 0;
            bool readFirstNewLine = false;
            bool readSecondNewLine = false;
            int headerLength = NaN;

            int readByte = 0;

            while ( readByte != NaN
                    && !readSecondNewLine )
            {
                readByte = httpContent.ReadByte();

                headerLength++;

                if ( readByte != NaN )
                {
                    if ( readByte == UNIVERSAL_NEW_LINE )
                    {
                        if ( readFirstNewLine )
                        {
                            readSecondNewLine = true;
                        }
                        else
                        {
                            readFirstNewLine = true;
                        }
                    }
                    else if ( readByte != NIX_NEW_LINE )
                    {
                        readFirstNewLine = false;
                    }
                }
            }

            return readSecondNewLine ? headerLength : NaN;
        }
    }
}