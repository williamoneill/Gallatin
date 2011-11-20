using System;
using System.IO;

namespace Gallatin.Service.Update
{
    /// <summary>
    /// Interface for classes that retrieve the update manifest information
    /// </summary>
    public interface IManifestProvider
    {
        /// <summary>
        /// Gets the manifest content
        /// </summary>
        string ManifestContent { get; }

        /// <summary>
        /// Downloads the most recent update archive to the specified location
        /// </summary>
        /// <param name="source">URI of the source archive</param>
        /// <param name="destination">
        /// Archive destination
        /// </param>
        void DownloadUpdateArchive( Uri source, FileInfo destination );
    }
}