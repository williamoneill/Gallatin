using System.IO;
using System.Xml.Serialization;

namespace Gallatin.Core
{
    public class SettingsMapper
    {
        public const string SettingsFileName = "settings.xml";

        private static T SetDefaultValue<T>( T originalValue, T initialValue, T defaultValue )
        {
            if ( originalValue.Equals( initialValue ) )
            {
                return defaultValue;
            }

            return originalValue;
        }

        public static void Save( ICoreSettings settings )
        {
            XmlSerializer serializer = new XmlSerializer( typeof (CoreSettings) );

            using ( FileStream stream = new FileStream( SettingsFileName, FileMode.Create ) )
            {
                serializer.Serialize( stream, settings );
            }
        }

        public static ICoreSettings Load()
        {
            ICoreSettings settings;

            XmlSerializer serializer = new XmlSerializer( typeof (CoreSettings) );

            if ( File.Exists( SettingsFileName ) )
            {
                using ( FileStream stream = new FileStream( SettingsFileName, FileMode.Open ) )
                {
                    settings = serializer.Deserialize( stream ) as ICoreSettings;
                }
            }
            else
            {
                settings = new CoreSettings();
            }

            // Set up defaults, also provide defaults for new values that were added since the 
            // last serialization.
            settings.ServerPort = SetDefaultValue( settings.ServerPort, 0, 8080 );
            settings.MaxNumberClients = SetDefaultValue( settings.MaxNumberClients, 0, 500 );
            settings.ReceiveBufferSize = SetDefaultValue( settings.ReceiveBufferSize, 0, 8192 );
            settings.WatchdogThreadSleepInterval = SetDefaultValue( settings.WatchdogThreadSleepInterval, 0, 10000 );
            settings.SessionInactivityTimeout = SetDefaultValue( settings.SessionInactivityTimeout, 0, 30000 );

            // Extra save...just in case we created a new instance in the above else block
            Save( settings );

            return settings;
        }
    }
}