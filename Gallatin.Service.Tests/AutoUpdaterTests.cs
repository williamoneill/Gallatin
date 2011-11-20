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
                "<Manifest><CurrentVersion value='-1'><Payload url='http://www.gallatinproxy.com/releases/payload.zip'/>"+
                "</CurrentVersion></Manifest>";

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
                "<Manifest><CurrentVersion value='1'><Payload url='http://www.gallatinproxy.com/releases/payload.zip'/>" +
                "</CurrentVersion></Manifest>";

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
                "<Manifest><CurrentVersion value='1'><Payload url='http://www.gallatinproxy.com/releases/payload.zip'/>" +
                "</CurrentVersion></Manifest>";

            string version2Xml =
                "<?xml version='1.0' encoding='utf-8' ?>" +
                "<Manifest><CurrentVersion value='2'><Payload url='http://www.gallatinproxy.com/releases/payload.zip'/>" +
                "</CurrentVersion></Manifest>";

            File.WriteAllText("updatemanifest.xml", version1Xml);

            if (File.Exists("payload.zip"))
                File.Delete("payload.zip");
            if (File.Exists("foo.txt"))
                File.Delete("foo.txt");
            if (File.Exists("bar.txt"))
                File.Delete("bar.txt");

            // Copy the zip file to the expected location. This is what the actual class will do.
            File.Copy("testfiles\\mytest.zip", "payload.zip");

            Mock<IManifestProvider> manifestProvider = new Mock<IManifestProvider>();
            manifestProvider.SetupGet(m => m.ManifestContent).Returns(version2Xml);

            var updated = AutoUpdater.CheckForUpdates(manifestProvider.Object);

            Assert.That(updated, Is.True);
            Assert.That(File.Exists("foo.txt"));
            Assert.That(File.ReadAllText("foo.txt"), Is.EqualTo("foo"));
            Assert.That(File.Exists("bar.txt"));
            Assert.That(File.ReadAllText("updatemanifest.xml"), Is.EqualTo(version2Xml));

            manifestProvider.VerifyGet(m=>m.ManifestContent, Times.Once());
            manifestProvider.Verify(m=>m.DownloadUpdateArchive(It.IsAny<Uri>(), It.IsAny<FileInfo>() ), Times.Once());
        }
    }
}
