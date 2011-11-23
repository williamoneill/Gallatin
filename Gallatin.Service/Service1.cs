using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using Gallatin.Core.Service;
using Gallatin.Service.Update;

namespace Gallatin.Service
{
    public partial class GallatinProxy : ServiceBase
    {
        private AppDomain _domain;
        private IProxyService _service;
        private Timer _updateTimer;

        public GallatinProxy()
        {
            InitializeComponent();
        }

        private void CheckForUpdates( object state )
        {
            try
            {
                if ( AutoUpdater.CheckForUpdates( new ManifestProvider() ) )
                {
                    GallatinEventLog.WriteEntry(
                        "Updates found. Downloading updates and restarting proxy server.",
                        EventLogEntryType.Information );

                    StopFromDomain();
                    StartInDomain();
                }
            }
            catch ( Exception ex )
            {
                GallatinEventLog.WriteEntry(
                    "An error was encountered when trying to auto-update assemblies. " + ex.Message,
                    EventLogEntryType.Error );
            }
        }

        private void StartInDomain()
        {
            GallatinEventLog.WriteEntry( "Starting Gallatin Proxy", EventLogEntryType.Information );

            AppDomainSetup appDomainSetup = new AppDomainSetup();
            appDomainSetup.ApplicationName = "GallatinProxyAppDomain";
            appDomainSetup.ShadowCopyFiles = "true";

            _domain = AppDomain.CreateDomain( "GallatinDomain", AppDomain.CurrentDomain.Evidence, appDomainSetup );
            _domain.UnhandledException += HandleDomainUnhandledException;
            _domain.InitializeLifetimeService();
            _service = (IProxyService) _domain.CreateInstanceAndUnwrap( "Gallatin.Core", "Gallatin.Core.Service.CrossDomainProxyService" );
            _service.Start();
        }

        private void HandleDomainUnhandledException( object sender, UnhandledExceptionEventArgs e )
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                GallatinEventLog.WriteEntry("Unhandled exception in AppDomain. " + ex.Message, EventLogEntryType.Error);
            }

            StopFromDomain();
        }

        private void StopFromDomain()
        {
            if ( _service != null )
            {
                GallatinEventLog.WriteEntry( "Stopping Gallatin Proxy", EventLogEntryType.Information );

                _service.Stop();
                _domain.UnhandledException -= HandleDomainUnhandledException;
                AppDomain.Unload( _domain );
            }
        }

        protected override void OnStart( string[] args )
        {
            // I considered checking for updates here but it slows down the service start-up time.

            StartInDomain();

            const int OneMinute = 60000;
            const int DueTime = 120 * OneMinute;
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