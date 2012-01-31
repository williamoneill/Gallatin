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
    public enum AccessLogType
    {
        /// <summary>
        /// 
        /// </summary>
        AccessGranted,

        /// <summary>
        /// 
        /// </summary>
        AccessBlocked,

        /// <summary>
        /// 
        /// </summary>
        HttpsTunnel
    }

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
        /// <param name="logType"></param>
        void Write(string connectionId, IHttpRequest host, AccessLogType logType );
    }

    [Export(typeof(IAccessLog))]
    internal class AccessLog : IAccessLog
    {
        private class LogEntry
        {
            public string ConnecitonId;
            public IHttpRequest Request;
            public AccessLogType LogType;
        }

        private List<LogEntry> _logEntries;

        private Timer _timer;

        private object _mutex = new object();

        private void TimerCallback(object state)
        {
            List<LogEntry> entries;

            DateTime now = DateTime.Now;

            FileInfo logFile = new FileInfo(Path.Combine( _directoryInfo.FullName, string.Format("{0}-{1:00}-{2:00}.html", now.Year, now.Month, now.Day) ));

            bool logFileCreated = !logFile.Exists;

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
                        if (logFileCreated)
                        {
                            writer.WriteLine("<head><style type='text/css'>td{{margin-bottom:6px; text-align:center; width:170px;}} td.ok{{background-color:lightgreen;}} td.https{{background-color:yellow;}} td.blocked{{background-color:red; foreground-color:white;}} td.url{{width:300px;text-align:left; }} p{{word-wrap: break-word;}} </style></head>" +
                                "<h3>Gallatin Proxy Access Log - Created {0}</h3><table>", DateTime.Now);
                        }

                        foreach (var logEntry in entries)
                        {
                            string filterInfo = string.Empty;
                            switch ( logEntry.LogType )
                            {
                                case AccessLogType.AccessGranted:
                                    filterInfo = "<td class='ok'>Access Granted</td>";
                                    break;

                                case AccessLogType.AccessBlocked:
                                    filterInfo = "<td class='blocked'>Access Denied</td>";
                                    break;

                                case AccessLogType.HttpsTunnel:
                                    filterInfo = "<td class='https'>HTTPS - Proxy Blind Tunnel</td>";
                                    break;
                            }

                            writer.WriteLine(string.Format("<tr><td>{0}</td>{1}<td>{2}</td><td class='url'><a href='{3}'>{4}</a></td></tr>", 
                                logEntry.ConnecitonId, filterInfo, now, logEntry.Request.Path, Break(logEntry.Request.Path)  ) );
                        }

                    }
                    catch ( Exception )
                    {
                        ServiceLog.Logger.Warning("Unhandled exception writing to access log. Will retry.");
                    }
                }
            }
        }

        private static string Break(string source )
        {
            const int MaxLen = 100;

            int i = 0;

            StringBuilder builder = new StringBuilder(source.Length);

            while (i < source.Length)
            {
                builder.Append( source.Substring( i, Math.Min( MaxLen, source.Length - i )) );
                builder.Append( "<br>" );
                i += MaxLen;
            }

            return builder.ToString();
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


        public void Write(string connectionId, IHttpRequest request, AccessLogType logType )
        {
            Contract.Requires(!string.IsNullOrEmpty(connectionId));
            Contract.Requires(request!=null);

            if (_timer != null)
            {
                var entry = new LogEntry()
                {
                    ConnecitonId = connectionId,
                    Request = request,
                    LogType = logType
                };

                lock (_mutex)
                {
                    _logEntries.Add(entry);
                }
                
            }

        }
    }
}
