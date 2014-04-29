using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea
{
    /// <summary>
    /// ロガークラス
    /// </summary>
    public static class Logger
    {
        /// <summary>
        /// ログレベル
        /// </summary>
        public enum Level { 
            /// <summary>
            /// 警告
            /// </summary>
            Warn, 

            /// <summary>
            /// デバッグ情報
            /// </summary>
            Debug, 

            /// <summary>
            /// 通常ログ
            /// </summary>
            Log, 

            /// <summary>
            /// エラー
            /// </summary>
            Error 
        };

        /// <summary>
        /// ログメッセージ構造体
        /// </summary>
        public struct LogMessage
        {
            /// <summary>
            /// ログレベル
            /// </summary>
            public readonly Level Level;

            /// <summary>
            /// ログメッセージ
            /// </summary>
            public readonly String Message;

            /// <summary>
            /// タイムスタンプ
            /// </summary>
            public readonly DateTime Timestamp;

            /// <summary>
            /// スタックトレース
            /// </summary>
            public readonly String StackTrace;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="message"></param>
            /// <param name="level"></param>
            public LogMessage(String message, Level level)
            {
                this.Message = message;
                this.Timestamp = DateTime.Now;
                this.Level = level;
                this.StackTrace = Environment.StackTrace;
            }

            /// <summary>
            /// ToString
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return String.Format("[{0}.{1:000}][{2}] {3}", Timestamp.ToString(), Timestamp.Millisecond, Level.ToString(), Message);
            }
        }

        /// <summary>
        /// ログ出力を受けるハンドラのデリゲート
        /// </summary>
        /// <param name="e">ログ構造体</param>
        public delegate void LogEventHandler(LogMessage e);

        /// <summary>
        /// ログ出力イベント
        /// </summary>
        public static event LogEventHandler LogClient;

        /// <summary>
        /// ログの出力を実行
        /// </summary>
        /// <param name="e"></param>
        private static void raise(LogMessage e)
        {
            if (LogClient != null)
            {
                LogClient.Invoke(e);
            }
        }

        #region 各レベルごとのログ出力メソッド
        /// <summary>
        /// 通常ログレベルのログを出力
        /// </summary>
        /// <param name="s"></param>
        public static void Log(object s)
        {
            raise(new LogMessage(s.ToString(), Level.Log));
        }

        /// <summary>
        /// デバッグレベルのログを出力
        /// </summary>
        /// <param name="s"></param>
        public static void Debug(object s)
        {
#if DEBUG
            raise(new LogMessage(s.ToString(), Level.Debug));
#endif
        }

        /// <summary>
        /// エラーレベルのログを出力
        /// </summary>
        /// <param name="s"></param>
        public static void Error(object s)
        {
            raise(new LogMessage(s.ToString(), Level.Error));
        }

        /// <summary>
        /// 警告レベルのログを出力
        /// </summary>
        /// <param name="s"></param>
        public static void Warn(object s)
        {
            raise(new LogMessage(s.ToString(), Level.Warn));
        }
        #endregion
    }
}
