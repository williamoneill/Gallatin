using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Gallatin.Core
{
    public static class Log
    {
        public static void Error(string format, params object[] args )
        {
            Error(string.Format(format, args));
        }

        public static void Error(string message)
        {
            Trace.TraceError( message );
        }

        public static void Warning(string format, params object[] args)
        {
            Warning(string.Format(format, args));
        }

        public static void Warning(string message)
        {
            Trace.TraceWarning(message);
        }

        public static void Info( Func<string> logMessageCreationDelegate )
        {
            // TODO: don't evaluate this unless the switch is enabled
            Log.Info(logMessageCreationDelegate());
        }

        public static void Info(string format, params object[] args)
        {
            Info(string.Format(format, args));
        }

        public static void Info(string message)
        {
            Trace.TraceInformation(message);
        }

        public static void Exception(string message, Exception exception)
        {
            Exception ex = exception;

            Error( "Tracing exception -- {0}", message );

            while ( ex != null )
            {
                Error( ex.Message );
                Error(ex.StackTrace);
                ex = ex.InnerException;
            }
            
        }
    }
}
