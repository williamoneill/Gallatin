using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using Gallatin.Contracts;
using Gallatin.Filter.Util;

namespace Gallatin.Filter
{
    /// <summary>
    /// Default whitelist instance
    /// </summary>
    [Export(typeof(IWhitelistEvaluator))]
    public class WhiteListEvaluator : IWhitelistEvaluator
    {
        private readonly List<string> _whiteListIpAddresses = new List<string>();
        private readonly List<string> _whiteListDomains = new List<string>(); 

        /// <summary>
        /// Constructs the default instance of the class
        /// </summary>
        [ImportingConstructor]
        public WhiteListEvaluator( ISettingsFileLoader loader )
        {
            var doc = loader.LoadFile( SettingsFileType.Whitelist );

            foreach (var whiteList in doc.Descendants("Hosts").Descendants("IP"))
            {
                _whiteListIpAddresses.Add(whiteList.Attribute("address").Value);
            }

            foreach (var whiteListUrl in doc.Descendants("Urls").Descendants("Url"))
            {
                _whiteListDomains.Add(whiteListUrl.Attribute("name").Value);
            }
        }

        /// <summary>
        /// Determines if the client address or host is in the whitelist
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <returns><c>true</c> if the connection is whitelisted</returns>
        public bool IsWhitlisted( IHttpRequest request, string connectionId )
        {
            var tokens = connectionId.Split(':');

            if (tokens.Length == 2)
            {
                if ( _whiteListIpAddresses.Any( address => IpAddressParser.IsMatch(address, tokens[0]) ) )
                {
                    return true;
                }
            }

            string host = request.Headers["host"];

            if(!string.IsNullOrEmpty(host))
            {
                return _whiteListDomains.Any( domain => host.EndsWith( domain, StringComparison.InvariantCultureIgnoreCase ) );
            }

            return false;
        }

        /// <summary>
        /// Gets the filter speed
        /// </summary>
        public FilterSpeedType FilterSpeedType
        {
            get
            {
                return FilterSpeedType.LocalAndFast;
            }
        }
    }
}
