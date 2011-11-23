using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Gallatin.Filter.Util
{
    /// <summary>
    /// Interface for classes that load setting files
    /// </summary>
    public interface ISettingsFileLoader
    {
        /// <summary>
        /// Loads specified settings file
        /// </summary>
        /// <param name="fileType">
        /// Settings file type
        /// </param>
        /// <returns>
        /// Parsed XML content from the specified settings file
        /// </returns>
        XDocument LoadFile( SettingsFileType fileType );
    }
}
