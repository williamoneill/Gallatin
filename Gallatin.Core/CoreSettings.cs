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
            ICoreSettings settings;

            XmlSerializer serializer = new XmlSerializer(typeof(CoreSettings));

            if (File.Exists(SettingsFileName))
            {
                using (FileStream stream = new FileStream(SettingsFileName, FileMode.Open))
                {
                    settings = serializer.Deserialize(stream) as ICoreSettings;
                }
            }
            else
            {
                settings = new CoreSettings();                
            }
            
            
            // Set up defaults, also provide defaults for new values that were added since the 
            // last serialization.
            settings.ServerPort = SetDefaultValue(settings.ServerPort, 0, 8080);
            settings.MaxNumberClients = SetDefaultValue(settings.MaxNumberClients, 0, 200 );
            settings.ReceiveBufferSize = SetDefaultValue(settings.ReceiveBufferSize,0, 8192);
            
            // Extra save...just in case we created a new instance
            Save(settings);

            return settings;
        }

        private static T SetDefaultValue<T>( T originalValue, T initialValue, T defaultValue )
        {
            if(originalValue.Equals(initialValue))
            {
                return defaultValue;
            }

            return originalValue;
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

        public int MaxNumberClients
        {
            get; set;
        }

        public int ReceiveBufferSize
        {
            get; set;
        }
    }
}
