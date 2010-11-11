using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea
{
    public class Logger
    {
        public delegate void LogEventHandler(LogEvent e);
        private Logger()
        {
        }
        public static event LogEventHandler LogClient;
        private static void raise(LogEvent e){
            if (LogClient != null)
            {
                LogClient.Invoke(e);
            }
        }

        public static void Log(object s)
        {
            raise(new LogEvent(s.ToString(),Level.Log));
        }

        public static void Debug(object s)
        {
#if DEBUG
            raise(new LogEvent(s.ToString(), Level.Debug));
#endif
        }

        public static void Error(object s)
        {
            raise(new LogEvent(s.ToString(), Level.Error));
        }

        public static void Warn(object s)
        {
            raise(new LogEvent(s.ToString(), Level.Warn));
        }

        public enum Level { Warn, Debug, Log, Error };
        public class LogEvent
        {
            public readonly Level Level;
            public readonly String msg;
            public readonly DateTime datetime;
            public LogEvent(String msg, Level level)
            {
                this.msg = msg;
                this.datetime = DateTime.Now;
                this.Level = level;
            }
            public override string ToString()
            {
                return String.Format("[{0}.{1:000}][{2}] {3}", datetime.ToString(), datetime.Millisecond, Level.ToString(), msg);
            }
        }
    }
}
