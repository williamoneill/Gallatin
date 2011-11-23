using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Gallatin.Service.Update;
using Moq;
using NUnit.Framework;

namespace Gallatin.Service.Tests
{
    [TestFixture]
    public class AutoUpdaterTests
    {
        [Test]
        public void NoUpdateTest()
        {
            // Notice the -1 for current version
            string noUpdateXml = 
                "<?xml version='1.0' encoding='utf-8' ?>"+
                "<Manifest>" +
                "<Version value='-1' url='http://www.gallatinproxy.com/releases/payload.zip'/>" +
                "</Manifest>";

            File.WriteAllText( "updatemanifest.xml", noUpdateXml);

            Mock<IManifestProvider> manifestProvider = new Mock<IManifestProvider>();

            var updated = AutoUpdater.CheckForUpdates( manifestProvider.Object );

            Assert.That(updated, Is.False);

            manifestProvider.VerifyGet(m=> m.ManifestContent, Times.Never());
        }

        [Test]
        public void NoDownloadIfVersionsMatch()
        {
            string version1Xml =
                "<?xml version='1.0' encoding='utf-8' ?>" +
                "<Manifest>" +
                "<Version value='1' url='http://www.gallatinproxy.com/releases/payload.zip'/>" +
                "</Manifest>";

            File.WriteAllText("updatemanifest.xml", version1Xml);

            Mock<IManifestProvider> manifestProvider = new Mock<IManifestProvider>();
            manifestProvider.SetupGet( m => m.ManifestContent ).Returns( version1Xml );

            var updated = AutoUpdater.CheckForUpdates(manifestProvider.Object);

            Assert.That(updated, Is.False);

            manifestProvider.Verify( m => m.DownloadUpdateArchive( It.IsAny<Uri>(), It.IsAny<FileInfo>() ), Times.Never() );
        }

        [Test]
        public void VerifyDownloadAndUnzip()
        {
            string version1Xml =
                "<?xml version='1.0' encoding='utf-8' ?>" +
                "<Manifest>" +
                "<Version value='1' url='http://www.gallatinproxy.com/releases/payload.zip'/>" +
                "</Manifest>";

            // Notice that version 3 appears before version 2. This is to verify that the list is sorted.
            string version3Xml =
                "<?xml version='1.0' encoding='utf-8' ?>" +
                "<Manifest>" +
                "<Version value='1' url='http://www.gallatinproxy.com/releases/payload.zip'/>" +
                "<Version value='3' url='http://www.gallatinproxy.com/releases/payload.3.zip'/>" +
                "<Version value='2' url='http://www.gallatinproxy.com/releases/payload.2.zip'/>" +
                "</Manifest>";

            // Write the file so we are at version 1
            File.WriteAllText("updatemanifest.xml", version1Xml);

            // Delete existing files from previous test runs
            if (File.Exists("payload.zip"))
                File.Delete("payload.zip");
            if (File.Exists("payload.2.zip"))
                File.Delete("payload.2.zip");
            if (File.Exists("payload.3.zip"))
                File.Delete("payload.3.zip");
            if (File.Exists("foo.txt"))
                File.Delete("foo.txt");
            if (File.Exists("bar.txt"))
                File.Delete("bar.txt");

            // Copy the zip file to the expected location. 
            File.Copy("testfiles\\mytest.zip", "payload.2.zip");
            File.Copy("testfiles\\mytest2.zip", "payload.3.zip");

            // Mock that the server has version 3 of the software
            Mock<IManifestProvider> manifestProvider = new Mock<IManifestProvider>();
            manifestProvider.SetupGet(m => m.ManifestContent).Returns(version3Xml);

            var updated = AutoUpdater.CheckForUpdates(manifestProvider.Object);

            // Verify that the correct files were written.
            // Note that the content of the two zip files is different. We should only see
            // the content from the second file.
            Assert.That(updated, Is.True);
            Assert.That(File.Exists("foo.txt"));
            Assert.That(File.ReadAllText("foo.txt"), Is.EqualTo("foo2"));
            Assert.That(File.Exists("bar.txt"));
            Assert.That(File.ReadAllText("bar.txt"), Is.EqualTo("bar2"));
            Assert.That(File.ReadAllText("updatemanifest.xml"), Is.EqualTo(version3Xml));

            manifestProvider.VerifyGet(m=>m.ManifestContent, Times.Once());
            manifestProvider.Verify(m=>m.DownloadUpdateArchive(It.IsAny<Uri>(), It.IsAny<FileInfo>() ), Times.Exactly(2));
        }
    }
}
