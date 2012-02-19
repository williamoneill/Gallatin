using System;

namespace Gallatin.Core.Net
{
    /// <summary>
    /// Interface for client session loggers, which write to the logs using the client session ID
    /// </summary>
    public interface ISessionLogger
    {
        /// <summary>
        /// Logs an exception
        /// </summary>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        void Exception( string message, Exception ex );
        
        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message"></param>
        void Error( string message );
        
        /// <summary>
        /// Writes an informational message
        /// </summary>
        /// <param name="message"></param>
        void Info( string message );
        
        /// <summary>
        /// Writes a verbose message
        /// </summary>
        /// <param name="message"></param>
        void Verbose( string message );

        /// <summary>
        /// Writes a verbose message using delayed evaluation, only executing the delegate if the logger
        /// is writing at the verbose level.
        /// </summary>
        /// <param name="message"></param>
        void Verbose(Func<string> message);
    }
}