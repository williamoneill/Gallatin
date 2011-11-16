using System.ComponentModel.Composition;
using Gallatin.Contracts;

namespace Gallatin.Filter
{
    /// <summary>
    /// Implements the default blacklist filter
    /// </summary>
    [Export( typeof (IConnectionFilter) )]
    public class BlacklistFilter : IConnectionFilter
    {
        #region IConnectionFilter Members

        /// <summary>
        /// Evaluates the HTTP request to determine if the host or path is in a blacklist
        /// </summary>
        /// <param name="request">HTTP request</param>
        /// <param name="connectionId">Client connection ID</param>
        /// <returns>HTML error text or <c>null</c> if no filter should be applied</returns>
        public string EvaluateFilter( IHttpRequest request, string connectionId )
        {
            string host = request.Headers["host"].ToLower();
            string contentType = request.Headers["content-type"];

            if ( !string.IsNullOrEmpty( host ) )
            {
                // Always turn on safe search for Google queries.
                if ( host == "www.google.com"
                     && request.Path.Contains( "&" ) )
                {
                    request.Path += "&safe=strict";
                }

                else if ( contentType != null && contentType.Equals( "text/html" )
                          && ( host.StartsWith( "ad." ) || host.StartsWith( "ads." ) ) )
                {
                    return
                        string.Format(
                            "<div style='background:white; padding:5; margin:5; font-size: 10pt; font-weight: bold; color: #000;'>Gallatin Proxy - Advertisement blocked to host: {0}</div>",
                            host );
                }

                //else
                //{

                //    string googlAdvisory =
                //        "<p>Advisory provided by Google http://code.google.com/apis/safebrowsing/safebrowsing_faq.html#whyAdvisory. Google works to provide the most accurate and up-to-date phishing and malware information. However, it cannot guarantee that its information is comprehensive and error-free: some risky sites may not be identified, and some safe sites may be identified in error.";

                //    if (rep == Reputation.MalwareBlackList)
                //    {
                //        return
                //            "Warning- Suspected phishing page. This page may be a forgery or imitation of another website, designed to trick users into sharing personal or financial information. Entering any personal information on this page may result in identity theft or other abuse. You can find out more about phishing from www.antiphishing.org." + googlAdvisory;
                //    }
                //    if (rep == Reputation.PhishBlackList)
                //    {
                //        return
                //            "Warning- Visiting this web site may harm your computer. This page appears to contain malicious code that could be downloaded to your computer without your consent. You can learn more about harmful web content including viruses and other malicious code and how to protect your computer at StopBadware.org." + googlAdvisory;
                //    }
                //}
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