using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Xml.Linq;
using Gallatin.Contracts;
using Gallatin.Filter.Util;

namespace Gallatin.Filter
{
    /// <summary>
    /// Filters responses based on MIME types in HTTP header
    /// </summary>
    [Export( typeof (IResponseFilter) )]
    public class MimeTypeFilter : IResponseFilter
    {
        private readonly Dictionary<string, string> _mimeTypes = new Dictionary<string, string>();

        /// <summary>
        /// Creates the default instance of the class
        /// </summary>
        /// <param name="loader">Reference to the settings loader</param>
        [ImportingConstructor]
        public MimeTypeFilter( ISettingsFileLoader loader )
        {
            XDocument doc = loader.LoadFile( SettingsFileType.MimeTypeFilter );

            foreach ( XElement mimeTypes in doc.Descendants( "BannedMimeTypes" ).Descendants( "MimeType" ) )
            {
                string mimeType = mimeTypes.Attribute( "name" ).Value;
                if ( !_mimeTypes.ContainsKey( mimeType ) )
                {
                    _mimeTypes.Add( mimeType, null );
                }
            }
        }

        #region IResponseFilter Members

        /// <summary>
        /// Evaluates the HTTP header to determine if the response contains a banned MIME type
        /// </summary>
        /// <param name="response">HTTP response</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <param name="bodyAvailableCallback">Always <c>null</c></param>
        /// <returns><c>null</c> if the reponse does not contain an invalid MIME type or HTML text describing the filter failure</returns>
        public string EvaluateFilter( IHttpResponse response,
                                      string connectionId,
                                      out Func<IHttpResponse, string, byte[], byte[]> bodyAvailableCallback )
        {
            // Don't need the body
            bodyAvailableCallback = null;

            string contentType = response.Headers["content-type"];

            if ( !string.IsNullOrEmpty( contentType ) )
            {
                if ( _mimeTypes.ContainsKey( contentType ) )
                {
                    return "Banned MIME type";
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the filter spped
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
}