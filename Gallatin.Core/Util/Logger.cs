using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Gallatin.Core.Util
{
    internal class Logger
    {
        public Logger(string traceSource)
        {
            Contract.Requires(!string.IsNullOrEmpty(traceSource)); 

            _source = new TraceSource(traceSource);
        }

        private TraceSource _source;

        public TraceSource Source
        {
            get
            {
                return _source;
            }
        }

        public SourceLevels TraceLevel
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


        public void Error(string format, params object[] args )
        {
            Error(string.Format(format, args));
        }

        public void Error(string message)
        {
            if(_source.Switch.ShouldTrace(TraceEventType.Error))
            {
                _source.TraceEvent(TraceEventType.Error, 1, message);
            }
        }

        public void Warning(string format, params object[] args)
        {
            Warning(string.Format(format, args));
        }

        public void Warning(string message)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Warning))
            {
                _source.TraceEvent(TraceEventType.Warning, 1, message);
            }
        }

        public void Info( Func<string> logMessageCreationDelegate )
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Information))
            {
                Info(logMessageCreationDelegate());
            }
        }

        public void Info(string format, params object[] args)
        {
            Info(string.Format(format, args));
        }

        public void Info(string message)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Information))
            {
                _source.TraceEvent(TraceEventType.Information, 1, message);
            }
        }

        public void Verbose(Func<string> logMessageCreationDelegate)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Verbose))
            {
                Verbose(logMessageCreationDelegate());
            }
        }

        public void Verbose(string format, params object[] args)
        {
            Verbose(string.Format(format, args));
        }

        public void Verbose(string message)
        {
            if (_source.Switch.ShouldTrace(TraceEventType.Verbose))
            {
                _source.TraceEvent(TraceEventType.Verbose, 1, message);
            }
        }

        public void Exception(string message, Exception exception)
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