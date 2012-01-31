using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;


namespace Gallatin.Service
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }

        protected override void OnAfterInstall(IDictionary savedState)
        {
            base.OnAfterInstall(savedState);
            using (var serviceController = new ServiceController(this.GallatinProxy.ServiceName, Environment.MachineName))
                serviceController.Start();
        }

        public override void Install(IDictionary stateSaver)
        {
            // See http://www.codeproject.com/KB/dotnet/dotnetscmdescription.aspx

            Microsoft.Win32.RegistryKey system,
                //HKEY_LOCAL_MACHINE\Services\CurrentControlSet

              currentControlSet,
                //...\Services

              services,
                //...\<Service Name>

              service,
                //...\Parameters - this is where you can put service-specific configuration

              config;

            try
            {
                //Let the project installer do its job

                base.Install(stateSaver);

                //Open the HKEY_LOCAL_MACHINE\SYSTEM key

                system = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("System");
                //Open CurrentControlSet

                currentControlSet = system.OpenSubKey("CurrentControlSet");
                //Go to the services key

                services = currentControlSet.OpenSubKey("Services");
                //Open the key for your service, and allow writing

                service = services.OpenSubKey(this.GallatinProxy.ServiceName, true);
                //Add your service's description as a REG_SZ value named "Description"

                service.SetValue("Description", "Web content filter");
                //(Optional) Add some custom information your service will use...

                config = service.CreateSubKey("Parameters");
            }
            catch (Exception e)
            {
                Console.WriteLine("An exception was thrown during service installation:\n" + e.ToString());
            }
        }

        public override void Uninstall(IDictionary savedState)
        {
            Microsoft.Win32.RegistryKey system,
              currentControlSet,
              services,
              service;

            try
            {
                //Drill down to the service key and open it with write permission

                system = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("System");
                currentControlSet = system.OpenSubKey("CurrentControlSet");
                services = currentControlSet.OpenSubKey("Services");
                service = services.OpenSubKey(this.GallatinProxy.ServiceName, true);
                //Delete any keys you created during installation (or that your service created)

                service.DeleteSubKeyTree("Parameters");
                //...

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception encountered while uninstalling service:\n" + e.ToString());
            }
            finally
            {
                //Let the project installer do its job

                base.Uninstall(savedState);
            }
        }
    }
}
