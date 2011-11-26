using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Gallatin.Contracts;
using Gallatin.Filter.Util;
using HtmlAgilityPack;

namespace Gallatin.Filter
{
    /// <summary>
    /// Filteres the HTML content for profanity and other keywords that indicate the page is undesirable
    /// </summary>
    [Export( typeof (IResponseFilter) )]
    public class HtmlBodyFilter : IResponseFilter
    {
        private readonly List<RegexFilter> _filters;
        private readonly ILogger _logger;

        /// <summary>
        /// Creates the default instance of the class
        /// </summary>
        /// <param name="loader">Reference to the settings file</param>
        /// <param name="logger">Reference to the filter logger</param>
        [ImportingConstructor]
        public HtmlBodyFilter( ISettingsFileLoader loader, ILogger logger )
        {
            _logger = logger;

            XDocument doc = loader.LoadFile( SettingsFileType.HtmlBodyFilter );

            List<RegexFilter> filters = new List<RegexFilter>();

            foreach ( XElement regex in doc.Descendants( "BannedWords" ).Descendants( "Regex" ) )
            {
                filters.Add( new RegexFilter(
                                 regex.Attribute( "name" ).Value,
                                 regex.Attribute( "value" ).Value,
                                 int.Parse( regex.Attribute( "weight" ).Value ) ) );
            }

            // Sort so that the "credit" filters are always hit first
            _filters = new List<RegexFilter>( filters.OrderBy( s => s.Weight ) );
        }

        #region IResponseFilter Members

        /// <summary>
        /// Evaluates the HTML body for undesireable keywords
        /// </summary>
        /// <param name="response">HTTP resonse</param>
        /// <param name="connectionId">Client connetion ID</param>
        /// <param name="bodyAvailableCallback">Callback to invoke when the HTML body is available, or <c>null</c> if the reponse is not HTML</param>
        /// <returns><c>null</c></returns>
        public string EvaluateFilter( IHttpResponse response,
                                      string connectionId,
                                      out Func<IHttpResponse, string, byte[], byte[]> bodyAvailableCallback )
        {
            string mimeType = response.Headers["content-type"];

            bodyAvailableCallback = null;

            if ( mimeType != null
                 && mimeType.ToLower().Contains( "text/html" ) )
            {
                // Get the body when it is available
                bodyAvailableCallback = ParseBody;
            }

            return null;
        }

        /// <summary>
        /// Gets the filter speed type. This is one of the slower filters due to the heavy use of regular expressions
        /// </summary>
        public FilterSpeedType FilterSpeedType
        {
            get
            {
                return FilterSpeedType.LocalAndSlow;
            }
        }

        #endregion

        private static byte[] CheckMetaContentRatings( HtmlDocument doc )
        {
            HtmlNodeCollection metaTags = doc.DocumentNode.SelectNodes( "//meta[@name='rating']" );

            if ( metaTags != null )
            {
                foreach ( HtmlNode tag in metaTags )
                {
                    foreach ( HtmlAttribute attrib in tag.Attributes )
                    {
                        if ( attrib.Name == "content"
                             && ( attrib.Value == "mature" || attrib.Value == "adult" || attrib.Value == "rta-5042-1996-1400-1577-rta" ) )
                        {
                            return Encoding.UTF8.GetBytes( "Page contains adult content" );
                        }
                    }
                }
            }

            return null;
        }

        private static string CensorWord( string word )
        {
            if (word.Length < 4)
            {
                return "***";
            }

            char[] chars = word.ToCharArray();

            for ( int i = 2; i < word.Length; i += 3 )
            {
                chars[i] = '*';
            }

            return new string(chars);
        }

        private static string FindRawHtmlText( HtmlDocument doc )
        {
            StringBuilder builder = new StringBuilder();

            foreach (
                HtmlNode text in
                    doc.DocumentNode.Descendants().Where(
                        n =>
                        n.NodeType == HtmlNodeType.Text
                        && ( n.ParentNode.Name != "script" && n.ParentNode.Name != "comment" && n.ParentNode.Name != "style" ) ) )
            {
                builder.AppendFormat( " {0} ", text.InnerHtml );
            }

            return builder.ToString();
        }

        private byte[] ParseBody( IHttpResponse response, string connectionId, byte[] body )
        {
            DateTime start = DateTime.Now;

            HtmlDocument doc = new HtmlDocument();

            // This ToLower is important for a variety of reasons. Do not remove.
            doc.LoadHtml( Encoding.UTF8.GetString( body ).ToLower() );

            byte[] returnValue = CheckMetaContentRatings( doc );

            if ( returnValue == null )
            {
                returnValue = ApplyRegexFiltering( doc );
            }

            DateTime end = DateTime.Now;

            _logger.WriteInfo( string.Format( "HTML Body filtering took {0} ms", ( end - start ).TotalMilliseconds ) );

            return returnValue;
        }

        private byte[] ApplyRegexFiltering( HtmlDocument doc )
        {
            const int MaxWeight = 100;

            byte[] returnValue = null;
            string htmlBody = FindRawHtmlText( doc );
            if ( htmlBody != null )
            {
                int weight = 0;

                string message = null;

                foreach ( RegexFilter regex in _filters )
                {
                    MatchCollection matches = regex.Regex.Matches( htmlBody );

                    foreach ( Match match in matches )
                    {
                        if ( match.Success )
                        {
                            message += string.Format( "<p>{0} {1} {2}",
                                                      regex.Name,
                                                      regex.Weight,
                                                      CensorWord(  match.Groups[0].Value ) );

                            weight += regex.Weight;
                        }
                    }


                    if ( weight >= MaxWeight )
                    {
                        returnValue = Encoding.UTF8.GetBytes( message );
                        break;
                    }
                }
            }
            return returnValue;
        }

        #region Nested type: RegexFilter

        private class RegexFilter
        {
            public RegexFilter( string name, string expr, int weight )
            {
                Name = name;
                ExpressionText = expr;
                Weight = weight;
                Regex = new Regex( expr, RegexOptions.Compiled );
            }

            public string Name { get; private set; }
            public string ExpressionText { get; private set; }
            public int Weight { get; private set; }
            public Regex Regex { get; private set; }
        }

        #endregion
    }
}