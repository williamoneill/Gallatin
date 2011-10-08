using System;
using System.Diagnostics;

namespace Gallatin.Core.Util
{
    public static class Log
    {
        static Log()
        {
            _source = new TraceSource("proxyLog");
        }

        private static TraceSource _source;

        public static TraceSource Source
        {
            get
            {
                return _source;
            }
        }

        public static SourceLevels TraceLevel
        {
            get
            {
                return _source.Switch.Level;
            }

            set
            {
                _source.Switch.Level = value;
            }
        }


        public static void Error(string format, params object[] args )
        {
            Error(string.Format(format, args));
        }

        public static void Error(string message)
        {
            if(_source.Switch.ShouldTrace(TraceEventType.Error))
            {
                _source.TraceEvent(TraceEventType.Error, 1, message);
            }
        }

        public static void Warning(string format, params object[] args)
        {
            Warning(string.Format(format, args));
        }

        public static void Warning(string message)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Warning))
            {
                _source.TraceEvent(TraceEventType.Warning, 1, message);
            }
        }

        public static void Info( Func<string> logMessageCreationDelegate )
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Information))
            {
                Info(logMessageCreationDelegate());
            }
        }

        public static void Info(string format, params object[] args)
        {
            Info(string.Format(format, args));
        }

        public static void Info(string message)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Information))
            {
                _source.TraceEvent(TraceEventType.Information, 1, message);
            }
        }

        public static void Verbose(Func<string> logMessageCreationDelegate)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Verbose))
            {
                Verbose(logMessageCreationDelegate());
            }
        }

        public static void Verbose(string format, params object[] args)
        {
            Verbose(string.Format(format, args));
        }

        public static void Verbose(string message)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Verbose))
            {
                _source.TraceEvent(TraceEventType.Verbose, 1, message);
            }
        }

        public static void Exception(string message, Exception exception)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Error))
            {
                Exception ex = exception;

                Error("***EXCEPTION*** {0}", message);

                while (ex != null)
                {
                    Error(ex.Message);
                    Error(ex.StackTrace);
                    ex = ex.InnerException;
                }
            }
        }
    }
}
