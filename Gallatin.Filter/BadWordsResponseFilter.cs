using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Gallatin.Contracts;

namespace Gallatin.Filter
{
    /// <summary>
    /// 
    /// </summary>
    //[Export(typeof(IResponseFilter))]
    public class NoApplicationsFilter : IResponseFilter
    {
        #region IResponseFilter Members

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        /// <param name="connectionId"></param>
        /// <param name="bodyAvailableCallback"></param>
        /// <returns></returns>
        public string EvaluateFilter( IHttpResponse response,
                                      string connectionId,
                                      out Func<IHttpResponse, string, byte[], byte[]> bodyAvailableCallback )
        {
            string mimeType = response.Headers["content-type"];

            bodyAvailableCallback = null;

            if ( mimeType != null
                 && mimeType.ToLower().Contains( "application" ) )
            {
                return "Applications not allowed";
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        public FilterSpeedType FilterSpeedType
        {
            get
            {
                return FilterSpeedType.LocalAndFast;
            }
        }

        #endregion
    }

    /// <summary>
    /// Filteres the HTML content for profanity and other keywords that indicate the page is undesirable
    /// </summary>
    [Export( typeof (IResponseFilter) )]
    public class BadWordsResponseFilter : IResponseFilter
    {

        #region IResponseFilter Members

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        /// <param name="connectionId"></param>
        /// <param name="bodyAvailableCallback"></param>
        /// <returns></returns>
        public string EvaluateFilter( IHttpResponse response,
                                      string connectionId,
                                      out Func<IHttpResponse, string, byte[], byte[]> bodyAvailableCallback )
        {
            string mimeType = response.Headers["content-type"];

            bodyAvailableCallback = null;

            if ( mimeType != null
                 && mimeType.ToLower().Contains( "text/html" ) )
            {
                bodyAvailableCallback = ParseHtmlContent;
            }

            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        public FilterSpeedType FilterSpeedType
        {
            get
            {
                return FilterSpeedType.LocalAndSlow;
            }
        }

        #endregion


        private static Dictionary<string, int> _wordList = new Dictionary<string, int>( StringComparer.InvariantCultureIgnoreCase );

        static BadWordsResponseFilter()
        {
            _wordList.Add("proxy", 34);

            _wordList.Add( "cunt", 100 );
            _wordList.Add("fuck", 100);
            _wordList.Add("fucker", 100);
            _wordList.Add("fuckin", 100);
            _wordList.Add("fucking", 100);
            
            _wordList.Add("shit", 80);
            _wordList.Add("shitty", 80);
            _wordList.Add("whore", 80);
            _wordList.Add("whores", 80);
            _wordList.Add("slut", 80);
            _wordList.Add("sluts", 80);
            _wordList.Add("porn", 30);
            _wordList.Add("p0rn", 30);

            _wordList.Add("anal", 50);
            _wordList.Add("butt", 30);

            _wordList.Add("tit", 50);
            _wordList.Add("tits", 50);
            _wordList.Add("fetish", 50);
            _wordList.Add("fetishes", 50);

            _wordList.Add("ass", 30);
            _wordList.Add("asses", 30);
            _wordList.Add("bastard", 30);
            _wordList.Add("bastards", 30);
            _wordList.Add("bitch", 30);
            _wordList.Add("bitches", 30);

            _wordList.Add("breasts", 30);
            _wordList.Add("breast", 20);
            _wordList.Add("cancer", -20);

            _wordList.Add("nude", 15);
            _wordList.Add("naked", 15);
            _wordList.Add("sex", 30);
            _wordList.Add("sensual", 30);
            _wordList.Add("sexy", 30);

            _wordList.Add("adult", 15);
            _wordList.Add("explict", 15);
            _wordList.Add("sexually-explicit", 45);
            _wordList.Add("sexually", 30);

            _wordList.Add("skin", 5);
            _wordList.Add("care", -5);
            _wordList.Add("makeup", -5);
            _wordList.Add("acne", -5);
            _wordList.Add("health", -10);
            _wordList.Add("flesh", 5);

        }

        private static string MakeWordLessOffensive( string word )
        {

            if (word.Length < 4)
            {
                return "***";
            }

            char[] array = word.ToCharArray();
            for (int i = 2; i < array.Length; i += 3)
            {
                array[i] = '*';
            }

            return new string(array);
        }

        private byte[] ParseHtmlContent( IHttpResponse response, string connectionId, byte[] htmlContent )
        {
            const int MaxWeight = 100;

            int rating = 0;

            var rawText = FindRawHtmlText( htmlContent );

            StringBuilder bannedWords = new StringBuilder();

            foreach (string word in rawText.Split( new[]{ ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int val;
                if (_wordList.TryGetValue(word, out val))
                {
                    bannedWords.AppendFormat( "[{0}] ", MakeWordLessOffensive( word ) );

                    rating += val;
                }
            }

            if (rating >= MaxWeight)
            {
                return Encoding.UTF8.GetBytes( 
                    string.Format(
                     "<h3>Gallatin Proxy</h3>This page has been banned because it contained inappropriate content. Page rating of {1} exceeds limit of {2}.<p>Language: {0}",
                     bannedWords, rating, MaxWeight) );
            }

            return null;
        }

        private static string FindRawHtmlText( byte[] htmlContent )
        {
            bool isInElement = false;

            MemoryStream memoryStream = new MemoryStream( htmlContent.Length );

            foreach ( byte c in htmlContent )
            {
                if ( c == '<' )
                {
                    isInElement = true;
                }
                else if ( c == '>' )
                {
                    isInElement = false;
                    memoryStream.WriteByte( (byte) ' ' );
                }

                else if ( !isInElement )
                {
                    memoryStream.WriteByte( c );
                }
            }

            string rawText = Encoding.UTF8.GetString( memoryStream.ToArray() );
            return rawText;
        }
    }
}