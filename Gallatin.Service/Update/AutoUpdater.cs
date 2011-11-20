using System;
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
            const string PayloadFileName = "payload.zip";
            const string FileName = "updatemanifest.xml";
            const int NeverUpdate = -1;

            XDocument oldDoc = XDocument.Load(FileName);
            var currentValue = int.Parse( oldDoc.Descendants( "CurrentVersion" ).First().Attribute( "value" ).Value );
            if (currentValue == NeverUpdate)
            {
                return false;
            }

            string manifestContent = manifestProvider.ManifestContent;

            XDocument newDoc = XDocument.Parse( manifestContent );
            var newValue = int.Parse(newDoc.Descendants("CurrentVersion").First().Attribute("value").Value);
            var updateUrl = newDoc.Descendants( "Payload" ).First().Attribute( "url" ).Value;

            if (newValue > currentValue)
            {
                FileInfo installFile = new FileInfo(PayloadFileName);

                manifestProvider.DownloadUpdateArchive( new Uri(updateUrl), installFile );

                using (ZipFile zipFile = new ZipFile(installFile.FullName))
                {
                    zipFile.ExtractAll(".");
                }

                // Save the manifest for the next version check
                File.WriteAllText( FileName, manifestContent );

                return true;
            }

            return false;
        }


    }
}
