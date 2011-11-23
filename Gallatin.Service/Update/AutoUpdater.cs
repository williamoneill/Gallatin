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
        /// <summary>
        /// Compares the new manifest against the installed files to determine if updates are available
        /// </summary>
        /// <param name="manifestProvider"></param>
        /// <returns></returns>
        public static bool CheckForUpdates( IManifestProvider manifestProvider )
        {
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

            // Check what's available on the server
            string manifestContent = manifestProvider.ManifestContent;
            XDocument newDoc = XDocument.Parse( manifestContent );
            ParseXmlToList( availableVersions, newDoc );

            // Apply the updates in order
            int highestInstalledVersion = installedVersions.Max( s => s.ManifestVersion );

            IEnumerable<Version> sortedList =
                availableVersions.OrderBy( s => s.ManifestVersion ).Where( s => s.ManifestVersion > highestInstalledVersion );

            if ( sortedList.Count() > 0 )
            {
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
                }

                // Save the manifest for the next version check
                File.WriteAllText( FileName, manifestContent );

                return true;
            }

            return false;
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