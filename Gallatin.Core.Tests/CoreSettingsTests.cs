using System.IO;
using NUnit.Framework;

namespace Gallatin.Core.Tests
{
    [TestFixture]
    public class CoreSettingsTests
    {
        #region Setup/Teardown

        [SetUp]
        public void TestSetup()
        {
            if ( File.Exists( SettingsMapper.SettingsFileName ) )
            {
                File.Delete( SettingsMapper.SettingsFileName );
            }
        }

        #endregion

        [Test]
        public void VerifyDefaults()
        {
            Assert.That(File.Exists(SettingsMapper.SettingsFileName), Is.False);

            ICoreSettings settings = SettingsMapper.Load();

            Assert.That(settings.MaxNumberClients, Is.EqualTo(100));
            Assert.That(settings.ReceiveBufferSize, Is.EqualTo(8192));

            Assert.That( File.Exists( SettingsMapper.SettingsFileName ), Is.True );
        }

        [Test]
        public void VerifySaveSettings()
        {
            ICoreSettings settings = SettingsMapper.Load();

            settings.MaxNumberClients = 400;

            SettingsMapper.Save(settings);

            ICoreSettings settings2 = SettingsMapper.Load();

            Assert.That( settings2.MaxNumberClients, Is.EqualTo(400) );
        }
    }
}