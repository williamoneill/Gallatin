using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Gallatin.Contracts;

namespace Gallatin.Filter
{
    /// <summary>
    /// 
    /// </summary>
    //[Export(typeof(IResponseFilter))]
    public class NoApplicationsFilter : IResponseFilter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        /// <param name="connectionId"></param>
        /// <param name="bodyAvailableCallback"></param>
        /// <returns></returns>
        public string EvaluateFilter( IHttpResponse response, string connectionId, out Func<IHttpResponse, string, byte[], byte[]> bodyAvailableCallback )
        {
            string mimeType = response.Headers["content-type"];

            bodyAvailableCallback = null;

            if (mimeType != null && mimeType.ToLower().Contains("application"))
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
    }

    /// <summary>
    /// Filteres the HTML content for profanity and other keywords that indicate the page is undesirable
    /// </summary>
    [Export(typeof(IResponseFilter))]
    public class BadWordsResponseFilter : IResponseFilter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        /// <param name="connectionId"></param>
        /// <param name="bodyAvailableCallback"></param>
        /// <returns></returns>
        public string EvaluateFilter( IHttpResponse response, string connectionId, out Func<IHttpResponse, string, byte[], byte[]> bodyAvailableCallback )
        {
            string mimeType = response.Headers["content-type"];

            bodyAvailableCallback = null;

            if (mimeType != null && mimeType.ToLower().Contains("html"))
            {
                bodyAvailableCallback = ParseHtmlContent;
            }

            return null;
        }

        private byte[] ParseHtmlContent(IHttpResponse response, string connectionId, byte[] htmlContent)
        {
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
    }
}
