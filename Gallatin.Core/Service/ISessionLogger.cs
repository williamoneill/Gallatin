using System;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// 
    /// </summary>
    public interface ISessionLogger
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        void Exception( string message, Exception ex );
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        void Error( string message );
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        void Info( string message );
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        void Verbose( string message );

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        void Verbose(Func<string> message);
    }
}