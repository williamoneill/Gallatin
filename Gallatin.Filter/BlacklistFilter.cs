using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Xml.Linq;
using Gallatin.Contracts;
using Gallatin.Filter.Util;

namespace Gallatin.Filter
{
    /// <summary>
    /// Implements the default blacklist filter
    /// </summary>
    [Export( typeof (IConnectionFilter) )]
    public class BlacklistFilter : IConnectionFilter
    {
        private readonly List<string> _blackListDomains = new List<string>();
        private readonly List<string> _blackListIpAddresses = new List<string>();

        /// <summary>
        /// Constructs the default instance of the class
        /// </summary>
        [ImportingConstructor]
        public BlacklistFilter( ISettingsFileLoader loader )
        {
            XDocument doc = loader.LoadFile( SettingsFileType.Blacklist );

            foreach ( XElement blackList in doc.Descendants( "Hosts" ).Descendants( "IP" ) )
            {
                _blackListIpAddresses.Add( blackList.Attribute( "address" ).Value );
            }

            foreach ( XElement blackListUrl in doc.Descendants( "Urls" ).Descendants( "Url" ) )
            {
                _blackListDomains.Add( blackListUrl.Attribute( "name" ).Value );
            }
        }

        #region IConnectionFilter Members

        /// <summary>
        /// Evaluates the HTTP request to determine if the host or path is in a blacklist
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <returns>HTML error text or <c>null</c> if no filter should be applied</returns>
        public string EvaluateFilter( IHttpRequest request, string connectionId )
        {
            string[] tokens = connectionId.Split( ':' );

            if ( tokens.Length == 2 )
            {
                if ( _blackListIpAddresses.Any( address => IpAddressParser.IsMatch( address, tokens[0] ) ) )
                {
                    return "Banned IP address";
                }
            }

            string host = request.Headers["host"];
            if ( !string.IsNullOrEmpty( host ) )
            {
                if ( _blackListDomains.Any( domain => host.EndsWith( domain, StringComparison.InvariantCultureIgnoreCase ) ) )
                {
                    return "Banned domain";
                }

                // Always turn on safe search for Google queries.
                if ( host == "www.google.com"
                     && request.Path.Contains( "&" ) )
                {
                    request.Path += "&safe=strict";
                }
            }

            return null;
        }


        /// <summary>
        /// Gets the filter speed type which is local and slow
        /// </summary>
        public FilterSpeedType FilterSpeedType
        {
            get
            {
                return FilterSpeedType.LocalAndSlow;
            }
        }

        #endregion
    }
}