using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Xml.Linq;

namespace Gallatin.Filter.Util
{
    [Export(typeof(ISettingsFileLoader))]
    internal class SettingsFileLoader : ISettingsFileLoader
    {
        public XDocument LoadFile( SettingsFileType fileType )
        {
            string filename = null;

            switch ( fileType )
            {
                case SettingsFileType.Blacklist:
                    filename = "Blacklist.xml";
                    break;
                case SettingsFileType.ExtensionFilter:
                    filename = "FilterSettings.xml";
                    break;
                case SettingsFileType.HtmlBodyFilter:
                    filename = "FilterSettings.xml";
                    break;
                case SettingsFileType.MimeTypeFilter:
                    filename = "FilterSettings.xml";
                    break;
                case SettingsFileType.Whitelist:
                    filename = "WhiteList.xml";
                    break;
                default:
                    throw new ArgumentException("Unrecognized settings file type");
            }

            const string DefaultSettingsFileDirectory = ".\\addins\\settings";
            const string UserSettingsFileDirectory =  DefaultSettingsFileDirectory + "\\userprovided";

            // If the user override file is available than use it
            if (File.Exists(Path.Combine(UserSettingsFileDirectory, filename)))
            {
                return XDocument.Load( Path.Combine( UserSettingsFileDirectory, filename ) );
            }

            return XDocument.Load(Path.Combine(DefaultSettingsFileDirectory, filename));
        }
    }
}