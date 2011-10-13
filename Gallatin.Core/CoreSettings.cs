using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Gallatin.Core
{
    [Export(typeof(ICoreSettings))]
    public class CoreSettings : ICoreSettings
    {
        private const string SettingsFileName = "settings.xml";

        public static ICoreSettings Load()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(CoreSettings));

            if (File.Exists(SettingsFileName))
            {
                using (FileStream stream = new FileStream(SettingsFileName, FileMode.Open))
                {
                    return serializer.Deserialize(stream) as ICoreSettings;
                }
            }

            CoreSettings settings = new CoreSettings();
            
            // Set up defaults
            settings.ServerPort = 8080;
            
            Save(settings);
            return settings;

        }

        public static void Save(ICoreSettings settings)
        {
            XmlSerializer serializer = new XmlSerializer(typeof (CoreSettings));

            using (FileStream stream = new FileStream(SettingsFileName, FileMode.Create))
            {
                serializer.Serialize(stream, settings);
            }
            
        }

        public int NetworkAddressBindingOrdinal
        {
            get; set;
        }

        public int ServerPort { get; set; }
    }
}
