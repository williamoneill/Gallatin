using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Gallatin.Core.Service;
using Gallatin.Core.Util;
using Gallatin.Service.Update;

namespace Gallatin.Service
{
    public partial class GallatinProxy : ServiceBase
    {
        public GallatinProxy()
        {
            InitializeComponent();
        }

        private AppDomain _domain;
        private IProxyService _service;

        private void CheckForUpdates(object state)
        {
            try
            {
                if (AutoUpdater.CheckForUpdates(new ManifestProvider()))
                {
                    GallatinEventLog.WriteEntry(
                        "Updates found. Downloading updates and restarting proxy server.", 
                        EventLogEntryType.Information);

                    StopFromDomain();
                    StartInDomain();
                }
            }
            catch( Exception ex )
            {
                GallatinEventLog.WriteEntry( 
                    "An error was encountered when trying to auto-update assemblies. " + ex.Message, 
                    EventLogEntryType.Error );
            }
        }

        private void StartInDomain()
        {
            GallatinEventLog.WriteEntry("Starting Gallatin Proxy", EventLogEntryType.Information);

            AppDomainSetup appDomainSetup = new AppDomainSetup();
            appDomainSetup.ApplicationName = "GallatinProxyAppDomain";
            appDomainSetup.ShadowCopyFiles = "true";

            _domain = AppDomain.CreateDomain("GallatinDomain", AppDomain.CurrentDomain.Evidence, appDomainSetup);
            _domain.UnhandledException += HandleDomainUnhandledException;
            _domain.InitializeLifetimeService();
            _service = (IProxyService)_domain.CreateInstanceAndUnwrap("Gallatin.Core", "Gallatin.Core.Service.CrossDomainProxyService");
            _service.Start();
        }

        void HandleDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            GallatinEventLog.WriteEntry("Unhandled exception in AppDomain. " + ex.Message, EventLogEntryType.Error );
        }

        private void StopFromDomain()
        {
            if (_service != null)
            {
                GallatinEventLog.WriteEntry("Stopping Gallatin Proxy", EventLogEntryType.Information);

                _service.Stop();
                _domain.UnhandledException -= HandleDomainUnhandledException;
                AppDomain.Unload(_domain);
            }
        }

        private Timer _updateTimer;

        protected override void OnStart(string[] args)
        {
            CheckForUpdates(null);

            StartInDomain();

            const int OneMinute = 60000;
            const int DueTime = 60 * OneMinute;
            const int IntervalTime = 240 * OneMinute;

            _updateTimer = new Timer( CheckForUpdates, null, DueTime, IntervalTime );
        }

        protected override void OnStop()
        {
            StopFromDomain();
            _updateTimer.Dispose();
        }
    }
}
