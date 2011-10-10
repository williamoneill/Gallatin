using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Gallatin.Core
{
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
            else
            {
                return new CoreSettings();
            }

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
    }
}
