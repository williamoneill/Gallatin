using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gallatin.Contracts
{
    /// <summary>
    /// Interface for the internal logger to be used by filters
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Writes an informational message
        /// </summary>
        /// <param name="info">
        /// Message to write
        /// </param>
        void WriteInfo( string info );

        /// <summary>
        /// Writes an error message
        /// </summary>
        /// <param name="error">
        /// Error message
        /// </param>
        void WriteError( string error );

        /// <summary>
        /// Writes a warning message
        /// </summary>
        /// <param name="warning">
        /// Warning message
        /// </param>
        void WriteWarning( string warning );
    }

}
