using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Gallatin.Contracts;

namespace Gallatin.Filter
{
    /// <summary>
    /// Default whitelist instance
    /// </summary>
    [Export(typeof(IWhitelistEvaluator))]
    public class WhiteList : IWhitelistEvaluator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        public bool IsWhitlisted( IHttpRequest request, string connectionId )
        {
            //var tokens = connectionId.Split( ':' );

            //if (tokens.Length == 2)
            //{
            //    //return Util.IpAddressParser.IsMatch( "127.0.0.1", tokens[0] );
            //}

            return false;
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
