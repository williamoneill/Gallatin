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
                    StopFromDomain();
                    Thread.Sleep(5000);
                    StartInDomain();
                }
            }
            catch
            {
            }
        }

        private void StartInDomain()
        {
            AppDomainSetup appDomainSetup = new AppDomainSetup();
            appDomainSetup.ApplicationName = "GallatinProxyAppDomain";
            appDomainSetup.ShadowCopyFiles = "true";

            _domain = AppDomain.CreateDomain("GallatinDomain", AppDomain.CurrentDomain.Evidence, appDomainSetup);
            _domain.InitializeLifetimeService();
            _service = (IProxyService)_domain.CreateInstanceAndUnwrap("Gallatin.Core", "Gallatin.Core.Service.CrossDomainProxyService");
            _service.Start();
        }

        private void StopFromDomain()
        {
            if (_service != null)
            {
                _service.Stop();
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
