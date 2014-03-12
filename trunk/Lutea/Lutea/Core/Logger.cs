using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea
{
    public static class Logger
    {
        public enum Level { Warn, Debug, Log, Error };

        public struct LogMessage
        {
            public readonly Level Level;
            public readonly String Message;
            public readonly DateTime Timestamp;
            public readonly String StackTrace;
            public LogMessage(String message, Level level)
            {
                this.Message = message;
                this.Timestamp = DateTime.Now;
                this.Level = level;
                this.StackTrace = Environment.StackTrace;
            }
            public override string ToString()
            {
                return String.Format("[{0}.{1:000}][{2}] {3}", Timestamp.ToString(), Timestamp.Millisecond, Level.ToString(), Message);
            }
        }

        public delegate void LogEventHandler(LogMessage e);

        public static event LogEventHandler LogClient;

        private static void raise(LogMessage e)
        {
            if (LogClient != null)
            {
                LogClient.Invoke(e);
            }
        }

        #region 各レベルごとのログ出力メソッド
        public static void Log(object s)
        {
            raise(new LogMessage(s.ToString(), Level.Log));
        }

        public static void Debug(object s)
        {
#if DEBUG
            raise(new LogMessage(s.ToString(), Level.Debug));
#endif
        }

        public static void Error(object s)
        {
            raise(new LogMessage(s.ToString(), Level.Error));
        }

        public static void Warn(object s)
        {
            raise(new LogMessage(s.ToString(), Level.Warn));
        }
        #endregion
    }
}
