using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ionic.Zip;

namespace Gallatin.Service.Update
{
    /// <summary>
    /// This class is responsible for checking for updates compared against installed
    /// assemblies and downloading new content
    /// </summary>
    public class AutoUpdater
    {
        public const string BackupExtension = ".backup.";

        private const string GallatinCoreName = "Gallatin.Core.dll";
        private const string GallatinServiceName = "Gallatin.Service.exe";
        private const string GallatinCoreBackup = GallatinCoreName + BackupExtension;
        private const string GallatinServiceBackup = GallatinServiceName + BackupExtension;

        private static Guid _guid;

        private static void RenameExecutingExecutables()
        {
            _guid = Guid.NewGuid();

            // Rename files in use that cannot be shadow copied

            FileInfo coreSource = new FileInfo( GallatinCoreName );
            FileInfo coreTarget = new FileInfo( GallatinCoreBackup + _guid );

            if ( coreTarget.Exists )
            {
                coreTarget.Delete();
            }

            coreSource.MoveTo( coreTarget.FullName );

            FileInfo serviceSource = new FileInfo( GallatinServiceName );
            FileInfo serviceTarget = new FileInfo( GallatinServiceBackup + _guid);

            if ( serviceTarget.Exists )
            {
                serviceTarget.Delete();
            }

            serviceSource.MoveTo( serviceTarget.FullName );
        }

        private static bool RevertExecutableNamesIfNotUpdated()
        {
            bool wereFilesUpdated = false;

            FileInfo coreSource = new FileInfo( GallatinCoreBackup + _guid);
            FileInfo coreTarget = new FileInfo( GallatinCoreName );

            FileInfo serviceSource = new FileInfo( GallatinServiceBackup + _guid);
            FileInfo serviceTarget = new FileInfo( GallatinServiceName );

            if ( coreTarget.Exists )
            {
                wereFilesUpdated = true;
            }
            else
            {
                coreSource.MoveTo( coreTarget.FullName );
            }

            if ( serviceTarget.Exists )
            {
                wereFilesUpdated = true;
            }
            else
            {
                serviceSource.MoveTo( serviceTarget.FullName );
            }

            return wereFilesUpdated;
        }

        /// <summary>
        /// Compares the new manifest against the installed files to determine if updates are available
        /// </summary>
        /// <param name="manifestProvider">Reference to the manifest provider</param>
        /// <returns><c>True</c> if files were updated</returns>
        public static bool CheckForUpdates( IManifestProvider manifestProvider )
        {
            bool wereFilesUpdated = false;

            const string FileName = "updatemanifest.xml";
            const int NeverUpdate = -1;

            List<Version> installedVersions = new List<Version>();
            List<Version> availableVersions = new List<Version>();

            XDocument oldDoc = XDocument.Load( FileName );
            ParseXmlToList( installedVersions, oldDoc );

            // Check if updates are disabled. Return false if they are.
            if ( installedVersions.FirstOrDefault( s => s.ManifestVersion == NeverUpdate ) != null )
            {
                return false;
            }

            try
            {
                // Move the current executables out of the way so they can be updated
                RenameExecutingExecutables();

                // Check what's available on the server
                string manifestContent = manifestProvider.ManifestContent;
                XDocument newDoc = XDocument.Parse( manifestContent );
                ParseXmlToList( availableVersions, newDoc );

                // Apply the updates in order
                IEnumerable<Version> sortedList =
                    availableVersions.OrderBy( s => s.ManifestVersion ).Where(
                        s => s.ManifestVersion > installedVersions.Max( r => r.ManifestVersion ) );

                foreach ( Version version in sortedList )
                {
                    // Let the exception go if the path is malformed
                    int index = version.PayloadUrl.LastIndexOf( '/' );
                    FileInfo installFile = new FileInfo( version.PayloadUrl.Substring( index + 1 ) );

                    manifestProvider.DownloadUpdateArchive( new Uri( version.PayloadUrl ), installFile );

                    using ( ZipFile zipFile = new ZipFile( installFile.FullName ) )
                    {
                        zipFile.ExtractAll( ".", ExtractExistingFileAction.OverwriteSilently );
                    }

                    installFile.Delete();

                    wereFilesUpdated = true;
                }

                // Save the manifest for the next version check
                File.WriteAllText( FileName, manifestContent );
            }
            finally
            {
                // Always revert if no updates; otherwise, the service will not restart
                if ( RevertExecutableNamesIfNotUpdated() )
                {
                    wereFilesUpdated = true;
                }
            }

            return wereFilesUpdated;
        }

        private static void ParseXmlToList( List<Version> availableVersions, XDocument manifest )
        {
            foreach ( XElement version in manifest.Descendants( "Version" ) )
            {
                availableVersions.Add( new Version
                                       {
                                           PayloadUrl = version.Attribute( "url" ).Value,
                                           ManifestVersion = int.Parse( version.Attribute( "value" ).Value )
                                       } );
            }
        }

        #region Nested type: Version

        private class Version
        {
            public string PayloadUrl { get; set; }
            public int ManifestVersion { get; set; }
        }

        #endregion
    }
}