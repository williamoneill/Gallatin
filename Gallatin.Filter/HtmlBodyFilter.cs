using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Gallatin.Contracts;
using Gallatin.Filter.Util;

namespace Gallatin.Filter
{
    /// <summary>
    /// Filteres the HTML content for profanity and other keywords that indicate the page is undesirable
    /// </summary>
    [Export( typeof (IResponseFilter) )]
    public class HtmlBodyFilter : IResponseFilter
    {
        private static readonly Regex _removeHtmlTags = new Regex( @"<(script|style)[\d\D]*?>[\d\D]*?</(script|style)>|(\<[^\>]*?\>)",
                                                                   RegexOptions.Singleline );

        private static readonly Regex _shortenWs = new Regex( @"\s{2,}", RegexOptions.Singleline );
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

        private static string FindRawHtmlText( byte[] htmlContent )
        {
            string html = Encoding.UTF8.GetString( htmlContent );

            string modifiedHtml = _removeHtmlTags.Replace( html, "" );

            return _shortenWs.Replace( modifiedHtml, "  " );
        }

        private byte[] ParseBody( IHttpResponse response, string connectionId, byte[] body )
        {
            const int MaxWeight = 100;

            byte[] returnValue = null;

            // ToLower 'cause none of the regex expressions are built to ignore case (performance)
            string htmlBody = FindRawHtmlText( body ).ToLower();
            if ( htmlBody != null )
            {
                int weight = 0;

                DateTime start = DateTime.Now;

                string message = null;

                foreach ( RegexFilter regex in _filters )
                {
                    Match match = regex.Regex.Match( htmlBody );

                    if ( match.Success )
                    {
                        message += string.Format( "<p>Filter <strong>{0}</strong> with weight of {1} had {2} matches",
                                                  regex.Name,
                                                  regex.Weight,
                                                  match.Groups[0].Length );

                        weight += match.Groups[0].Length * regex.Weight;
                    }

                    if ( weight >= MaxWeight )
                    {
                        returnValue = Encoding.UTF8.GetBytes( message );
                        break;
                    }
                }

                DateTime end = DateTime.Now;

                _logger.WriteInfo( string.Format( "HTML Body filtering took {0} ms", ( end - start ).TotalMilliseconds ) );
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