using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Xml.Linq;
using Gallatin.Contracts;
using Gallatin.Filter.Util;

namespace Gallatin.Filter
{
    /// <summary>
    /// Connection filter based on target extension
    /// </summary>
    [Export( typeof (IConnectionFilter) )]
    public class ExtensionFilter : IConnectionFilter
    {
        private readonly List<string> _extensions = new List<string>();

        /// <summary>
        /// Creates the default instance for the class
        /// </summary>
        [ImportingConstructor]
        public ExtensionFilter( ISettingsFileLoader loader )
        {
            XDocument doc = loader.LoadFile( SettingsFileType.ExtensionFilter );

            foreach ( XElement extension in doc.Descendants( "BannedFileExtensions" ).Descendants( "Extensions" ) )
            {
                _extensions.Add( extension.Attribute( "value" ).Value );
            }
        }

        #region IConnectionFilter Members

        /// <summary>
        /// Evaluates the HTTP request against the set extension filters
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <returns><c>null</c> if no filter was applied or HTML describing the failure</returns>
        public string EvaluateFilter( IHttpRequest request, string connectionId )
        {
            string path = request.Path.ToLower();

            if ( _extensions.Any( path.EndsWith ) )
            {
                return "Banned extension";
            }

            return null;
        }

        /// <summary>
        /// Gets the filter speed type
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