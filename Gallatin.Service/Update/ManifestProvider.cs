using System;
using System.IO;
using System.Net;

namespace Gallatin.Service.Update
{
    /// <summary>
    /// Retrieves the current update manifest from the Gallatin Proxy web server
    /// </summary>
    internal class ManifestProvider : IManifestProvider
    {
        #region IManifestProvider Members

        public string ManifestContent
        {
            get
            {
                using ( WebClient client = new WebClient() )
                {
                    return client.DownloadString( "http://www.gallatinproxy.com/releases/UpdateManifest.xml" );
                }
            }
        }

        public void DownloadUpdateArchive( Uri source, FileInfo destination )
        {
            using ( WebClient client = new WebClient() )
            {
                if(destination.Exists)
                    destination.Delete();

                client.DownloadFile( source, destination.FullName );
            }
        }

        #endregion
    }
}