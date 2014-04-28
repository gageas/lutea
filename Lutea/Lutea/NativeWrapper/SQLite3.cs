using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Gageas.Wrapper.SQLite3
{
    /// <summary>
    /// SQLite3のラッパ
    /// </summary>
    public static class SQLite3{
        /// <summary>
        /// SQLITE_OKの定数
        /// </summary>
        public const int SQLITE_OK = 0;

        /// <summary>
        /// SQLITE_ROWの定数
        /// </summary>
        public const int SQLITE_ROW = 100;

        /// <summary>
        /// SQLITE_DONEの定数
        /// </summary>
        public const int SQLITE_DONE = 101;

        /// <summary>
        /// SQLITE_TRANSIENTの定数
        /// </summary>
        public const UInt32 SQLITE_TRANSIENT = 0xffffffff;

        /// <summary>
        /// SQLiteの文字列エンコーディング列挙体
        /// </summary>
        public enum TextEncoding{
            /// <summary>
            /// UTF-8
            /// </summary>
            SQLITE_UTF8         = 1,

            /// <summary>
            /// UTF-16(LE)
            /// </summary>
            SQLITE_UTF16LE      = 2,

            /// <summary>
            /// UTF-16(BE)
            /// </summary>
            SQLITE_UTF16BE      = 3,
            
            /// <summary>
            /// UTF-16(native byte order)
            /// </summary>
            SQLITE_UTF16        = 4,

            /// <summary>
            /// ANY(sqlite3_create_function only)
            /// </summary>
            SQLITE_ANY          = 5,

            /// <summary>
            /// UTF16 Aligned(sqlite3_create_collation only
            /// </summary>
            SQLITE_UTF16_ALIGNED= 8,
        }
    }

    /// <summary>
    /// データベースオブジェクト
    /// </summary>
    /// <remarks>原則開いたスレッドと同じスレッドから操作する。interruptは特例？</remarks>
    public class SQLite3DB : IDisposable
    {
        private IntPtr dbPtr = (IntPtr)0;

        internal IntPtr GetHandle()
        {
            return dbPtr;
        }

        private static string ReadStringFromLPWSTR(IntPtr lpwstr)
        {
            return (lpwstr == IntPtr.Zero ? null : Marshal.PtrToStringUni(lpwstr));
        }

        /// <summary>
        /// SQLite3の例外クラス
        /// </summary>
        public class SQLite3Exception : System.ApplicationException
        {
            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="message">メッセージ</param>
            public SQLite3Exception(string message)
                : base(message)
            {
            }
        }

        #region Constructor
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="filename">データベースファイル名</param>
        public SQLite3DB(string filename)
        {
            int ret = sqlite3_open16(filename, out dbPtr);
            if (ret != SQLite3.SQLITE_OK)
            {
                throw (new SQLite3Exception("db file open error."));
            }
        }
        #endregion

        /// <summary>
        /// IDisposableの実装
        /// </summary>
        public void Dispose()
        {
            if (dbPtr != (IntPtr)0)
            {
                sqlite3_close(dbPtr);
                dbPtr = (IntPtr)0;
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~SQLite3DB()
        {
            Dispose();
        }

        /// <summary>
        /// Extensionの読み込みを有効にする
        /// </summary>
        public bool EnableLoadExtension
        {
            set
            {
                sqlite3_enable_load_extension(dbPtr, value ? 1 : 0);
            }
        }

        /// <summary>
        /// 実行中のクエリを中断する
        /// </summary>
        public void interrupt()
        {
            sqlite3_interrupt(dbPtr);
        }

        /// <summary>
        /// 行を取得
        /// </summary>
        /// <param name="table">テーブル名</param>
        /// <param name="rowid">行</param>
        /// <returns>行のデータ</returns>
        public object[] FetchRow(string table, int rowid)
        {
            object[] ret;
            try
            {
                using (var stmt = this.Prepare("SELECT * FROM " + table + " WHERE ROWID=?;"))
                {
                    if (!stmt.IsReadOnly()) throw new InvalidOperationException();
                    stmt.Bind(1, rowid.ToString());
                    ret = stmt.EvaluateFirstROW();
                }
            }
            catch (SQLite3Exception)
            {
                ret = new object[] { };
            }
            return ret;
        }

        /// <summary>
        /// 行を取得(範囲)
        /// </summary>
        /// <param name="table">テーブル名</param>
        /// <param name="rowid_start">先頭の行ID</param>
        /// <param name="count">行数</param>
        /// <param name="collection"></param>
        public void FetchRowRange(string table, int rowid_start, int count, object[][] collection)
        {
            int start = Math.Min(rowid_start, collection.Length);
            int end = Math.Min(rowid_start + count, collection.Length);
            using (var stmt = this.Prepare("SELECT * FROM " + table + " WHERE ROWID=?;"))
            {
                if (!stmt.IsReadOnly()) throw new InvalidOperationException();
                for (int i = start; i < end; i++)
                {
                    object[] ret;
                    try
                    {
                        stmt.Reset();
                        stmt.Bind(1, (i + 1).ToString());
                        ret = stmt.EvaluateFirstROW();
                    }
                    catch (SQLite3Exception)
                    {
                        ret = new object[] { };
                    }
                    collection[i] = ret;
                }
            }
        }

        /// <summary>
        /// Execのコールバック関数のデリゲート
        /// </summary>
        /// <param name="p"></param>
        /// <param name="argc"></param>
        /// <param name="argv"></param>
        /// <param name="columnNames"></param>
        /// <returns></returns>
        public delegate int Exec_callback(IntPtr p, int argc, string[] argv, string[] columnNames);

        /// <summary>
        /// SQLを実行
        /// </summary>
        /// <param name="sql">SQL</param>
        /// <param name="callback">コールバック関数</param>
        public void Exec(string sql, Exec_callback callback = null)
        {
            lock (this)
            {
                int ret = -1;
                try
                {
                    ret = sqlite3_exec(dbPtr, sql, callback, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                }
                if (ret != SQLite3.SQLITE_OK)
                {
                    throw new SQLite3Exception("exec error.");
                }
            }
        }

        /// <summary>
        /// プリペアドステートメントを生成
        /// </summary>
        /// <param name="sql">SQL文</param>
        /// <returns>プリペアドステートメントオブジェクト</returns>
        public STMT Prepare(string sql)
        {
            return new SQLite3DB._STMT(this, sql);
        }

        /// <summary>
        /// プリペアドステートメントオブジェクト(インタフェース)
        /// </summary>
        public abstract class STMT : IDisposable
        {
            /// <summary>
            /// コールバック関数のデリゲート
            /// </summary>
            /// <param name="o"></param>
            public delegate void dCallback(Object[] o);

            /// <summary>
            /// ステートメントを評価する
            /// </summary>
            /// <param name="cb"></param>
            public abstract void Evaluate(dCallback cb);

            /// <summary>
            /// ステートメントを評価し，結果を全て取得
            /// </summary>
            /// <returns></returns>
            public abstract object[][] EvaluateAll();

            /// <summary>
            /// ステートメントを評価し，先頭の行を取得
            /// </summary>
            /// <returns></returns>
            public abstract object[] EvaluateFirstROW();

            /// <summary>
            /// IDisposableの実装
            /// </summary>
            public abstract void Dispose();

            /// <summary>
            /// リセット
            /// </summary>
            public abstract void Reset();

            /// <summary>
            /// プレースホルダに値をバインド
            /// </summary>
            /// <param name="index">プレースホルダのインデックス</param>
            /// <param name="value">値</param>
            public abstract void Bind(int index,string value);

            /// <summary>
            /// ステートメントがReadOnlyかどうか
            /// </summary>
            /// <returns></returns>
            public abstract bool IsReadOnly();
        }

        private class _STMT : STMT
        {
            SQLite3DB db;
            IntPtr stmt;
            int columnCount = -1;
            public _STMT(SQLite3DB db, String sql)
            {
                this.db = db;
                int ret = -1;
                try
                {
                    ret = sqlite3_prepare16(db.dbPtr, sql, sql.Length * 2, out stmt, IntPtr.Zero);
                }
                finally
                {
                }
                if (ret != SQLite3.SQLITE_OK)
                {
                    throw new SQLite3Exception("prepare error\n" + ReadStringFromLPWSTR(sqlite3_errmsg16(db.dbPtr)));
                }
            }
            public override void Dispose()
            {
                if (stmt != IntPtr.Zero)
                {
                    sqlite3_finalize(stmt);
                }
                stmt = IntPtr.Zero;
                GC.SuppressFinalize(this);
            }

            ~_STMT()
            {
                this.Dispose();
            }

            public override void Reset()
            {
                sqlite3_reset(this.stmt);
            }

            public override void Bind(int index, string value) {
                if(value.IndexOf('\0')>=0){
                    value = value.Normalize().Replace('\0', '\n');
                }
                sqlite3_bind_text16(stmt, index, value, value.Length * 2, SQLite3.SQLITE_TRANSIENT);
            }
            public override void Evaluate(dCallback cb)
            {
                int r;
                if (columnCount == -1) columnCount = sqlite3_column_count(stmt);
                while (SQLite3.SQLITE_ROW == (r = sqlite3_step(this.stmt)))
                {
                    object[] o = new string[columnCount];
                    for (int i = 0; i < columnCount; i++)
                    {
                        o[i] = ReadStringFromLPWSTR(sqlite3_column_text16(stmt, i));
                    }
                    cb(o);
                }
                if (r != SQLite3.SQLITE_DONE)
                {
                    throw new SQLite3Exception(ReadStringFromLPWSTR(sqlite3_errmsg16(db.dbPtr)));
                }
                Reset();
            }
            public override object[][] EvaluateAll()
            {
                List<object[]> data = new List<object[]>();
                int r;
                if (columnCount == -1) columnCount = sqlite3_column_count(stmt);
                while (SQLite3.SQLITE_ROW == (r = sqlite3_step(this.stmt)))
                {
                    object[] o = new string[columnCount];
                    for (int i = 0; i < columnCount; i++)
                    {
                        o[i] = ReadStringFromLPWSTR(sqlite3_column_text16(stmt, i));
                    }
                    data.Add(o);
                }
                if (r != SQLite3.SQLITE_DONE)
                {
                    throw new SQLite3Exception(ReadStringFromLPWSTR(sqlite3_errmsg16(db.dbPtr)));
                }
                Reset();
                return data.ToArray();
            }
            public override object[] EvaluateFirstROW()
            {
                int r;
                if (columnCount == -1) columnCount = sqlite3_column_count(stmt);
                r = sqlite3_step(this.stmt);
                object[] o = new string[columnCount];
                if (r == SQLite3.SQLITE_ROW)
                {
                    for (int i = 0; i < columnCount; i++)
                    {
                        o[i] = ReadStringFromLPWSTR(sqlite3_column_text16(stmt, i));
                    }
                }
                r = sqlite3_step(this.stmt);
                if (r != SQLite3.SQLITE_DONE)
                {
                    throw new SQLite3Exception(ReadStringFromLPWSTR(sqlite3_errmsg16(db.dbPtr)));
                }
                Reset();
                return o;
            }
            public override bool IsReadOnly()
            {
                return sqlite3_stmt_readonly(this.stmt);
            }
        }

        #region DLL Import
        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_open16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open16([MarshalAs(UnmanagedType.LPWStr)]string filename, out IntPtr db);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_enable_load_extension", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_enable_load_extension(IntPtr db, int onoff);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_interrupt", CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_interrupt(IntPtr db);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_exec", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db,[MarshalAs(UnmanagedType.LPStr)]string sql,Exec_callback callback,IntPtr p2, IntPtr err);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_prepare16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare16(IntPtr db,[MarshalAs(UnmanagedType.LPWStr)]string sql, int sqllen, out IntPtr stmt, IntPtr tail);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_reset", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_reset(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_step", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_finalize", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_stmt_readonly", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool sqlite3_stmt_readonly(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_count", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_count(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_int", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_int(IntPtr stmt, int columnId);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_text", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr stmt, int columnId);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_text16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text16(IntPtr stmt, int columnId);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_bind_text", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text(IntPtr stmt,int id,[MarshalAs(UnmanagedType.LPStr)]string str,int len,IntPtr dispose);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_bind_text16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text16(IntPtr stmt, int id, [MarshalAs(UnmanagedType.LPWStr)]string str, int len, UInt32 dispose);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_result_null", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_result_null(IntPtr sqlite3_context);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_result_text16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_result_text16(IntPtr sqlite3_context, [MarshalAs(UnmanagedType.LPWStr)]string str, int length, UInt32 dispose);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_result_int", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_result_int(IntPtr sqlite3_context, Int32 val);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_result_int64", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_result_int64(IntPtr sqlite3_context, Int64 val);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_result_double", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_result_double(IntPtr sqlite3_context, double val);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_value_text16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_value_text16(IntPtr sqlite3_value);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_user_data", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_user_data(IntPtr sqlite3_context);
        
        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_errmsg16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg16(IntPtr db);
        #endregion
    }
}
