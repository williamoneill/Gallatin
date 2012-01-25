using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Gallatin.Contracts;

namespace Gallatin.Core.Service
{
    /// <summary>
    /// 
    /// </summary>
    public interface IAccessLog
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="logDirectory"></param>
        void Start(DirectoryInfo logDirectory);
        
        /// <summary>
        /// 
        /// </summary>
        void Stop();
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="host"></param>
        /// <param name="filterInfo"></param>
        void Write(string connectionId, IHttpRequest host, string filterInfo );
    }

    [Export(typeof(IAccessLog))]
    internal class AccessLog : IAccessLog
    {
        private class LogEntry
        {
            public DateTime LogDate;
            public string ConnecitonId;
            public IHttpRequest Request;
            public string FilterInfo;
        }

        private List<LogEntry> _logEntries;

        private Timer _timer;

        private object _mutex = new object();

        private void TimerCallback(object state)
        {
            List<LogEntry> entries;

            DateTime now = DateTime.Now;

            FileInfo logFile = new FileInfo(Path.Combine( _directoryInfo.FullName, string.Format("{0}-{1:00}-{2:00}.html", now.Year, now.Month, now.Day) ));

            using (FileStream fileStream = new FileStream(logFile.FullName, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                using (StreamWriter writer = new StreamWriter(fileStream))
                {
                    lock (_mutex)
                    {
                        entries = _logEntries;
                        _logEntries = new List<LogEntry>();
                    }

                    try
                    {
                        if (!logFile.Exists)
                        {
                            writer.WriteLine("<h3>Gallatin Proxy Log File</h3>");
                        }

                        foreach (var logEntry in entries)
                        {
                            writer.WriteLine(string.Format("<br>[{0}] -- [{2}] -- [{3}] -- <a href='{1}'>{1}</a>", logEntry.ConnecitonId, logEntry.Request.Path, logEntry.FilterInfo, logEntry.LogDate));
                        }

                    }
                    catch ( Exception ex)
                    {
                        ServiceLog.Logger.Exception("Unhandled exception writing to access log",ex);
                    }
                }
            }

        }

        private DirectoryInfo _directoryInfo;

        public void Start(DirectoryInfo logDirectory)
        {
            Contract.Requires(logDirectory != null);

            if (!logDirectory.Exists)
            {
                logDirectory.Create();
            }

            _directoryInfo = logDirectory;

            _timer = new Timer(TimerCallback, null, 10000, 2000);
            _logEntries = new List<LogEntry>();
        }

        public void Stop()
        {
            _timer.Dispose();
            _timer = null;
            TimerCallback(null);
        }

        public void Write(string connectionId, IHttpRequest request, string filterInfo )
        {
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires(request!=null);
            Contract.Requires(!string.IsNullOrEmpty(filterInfo));

            if (_timer != null)
            {
                var entry = new LogEntry()
                {
                    LogDate = DateTime.Now,
                    ConnecitonId = connectionId,
                    Request = request,
                    FilterInfo = filterInfo
                };

                lock (_mutex)
                {
                    _logEntries.Add(entry);
                }
                
            }

        }
    }
}
