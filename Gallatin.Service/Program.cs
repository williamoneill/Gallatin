using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;

namespace Gallatin.Service
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            // Change to the install directory so we don't make a mess of files in the system area
            FileInfo di = new FileInfo(Assembly.GetExecutingAssembly().Location);
            Directory.SetCurrentDirectory(di.Directory.FullName);

            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[] 
			{ 
				new GallatinProxy() 
			};
            ServiceBase.Run(ServicesToRun);
        }
    }
}
