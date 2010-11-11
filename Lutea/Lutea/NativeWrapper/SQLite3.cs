using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Gageas.Wrapper.SQLite3
{
    public static class SQLite3{
        public const int SQLITE3_OK = 0; // TODO: あとでenumにするかも？
        public const int SQLITE3_ROW = 100;
        public const int SQLITE_DONE = 101;
        public const UInt32 SQLITE_TRANSIENT = 0xffffffff;
        public enum TextEncoding{
            SQLITE_UTF8         = 1,
            SQLITE_UTF16LE      = 2,
            SQLITE_UTF16BE      = 3,
            SQLITE_UTF16        = 4,   /* Use native byte order */
            SQLITE_ANY          = 5,  /* sqlite3_create_function only */
            SQLITE_UTF16_ALIGNED= 8,   /* sqlite3_create_collation only */
        }
    }

    // 所々lock()があるが、SQLite3のDB自体複数スレッドから叩くとよくないらしい（個別にOpenすればよい）のであんまり意味ないか
    // interruptはその都合上別スレッドから叩いてもいいんじゃないかな
    public class SQLite3DB : IDisposable
    {
        private IntPtr dbPtr = (IntPtr)0;
        private IntPtr dbPtrForLock = (IntPtr)0;
        bool locked = false;


        // Exception
        public class SQLite3Exception : System.ApplicationException
        {
            public SQLite3Exception(string message)
                : base(message)
            {
            }
        }

        #region Constructor
        public SQLite3DB(string filename)
            : this(filename, false)
        {
        }

        public SQLite3DB(string filename, bool lockable)
        {
            int ret;
            IntPtr _filename = Marshal.StringToHGlobalUni(filename);
            try
            {
                ret = sqlite3_open16(_filename, out dbPtr); //UTF16toUTF8( 
                if (lockable)
                {
                    ret = sqlite3_open16(_filename, out dbPtrForLock);
                }

            }
            catch (Exception)
            {
                throw (new SQLite3Exception("db file open error."));
            }
            if (ret != SQLite3.SQLITE3_OK)
            {
                Marshal.FreeHGlobal(_filename);
                throw (new SQLite3Exception("db file open error."));
            }
        }
        #endregion

        public void Dispose()
        {
            if (dbPtr != (IntPtr)0)
            {
                sqlite3_close(dbPtr);
                dbPtr = (IntPtr)0;
            }
            GC.SuppressFinalize(this);
        }

        ~SQLite3DB()
        {
            Dispose();
        }

        public bool EnableLoadExtension
        {
            set
            {
                sqlite3_enable_load_extension(dbPtr, value ? 1 : 0);
            }
        }

        public void interrupt()
        {
            sqlite3_interrupt(dbPtr);
        }

        #region Lock
        /*
         * DB.GetLockによってLockオブジェクトを取得することにより、DBをロック（書き込みを拒否）することができる。
         * LockオブジェクトをDisposeすることによりロックが開放される。
         */
        public Lock GetLock(string tableName)
        {
            lock (this)
            {
                if (locked) return null;
                if (dbPtrForLock != IntPtr.Zero)
                {
                    IntPtr _sql = Marshal.StringToHGlobalAnsi("BEGIN;SELECT rowid FROM " + tableName + " LIMIT 1;");
                    int ret = -1;
                    try
                    {
                        ret = sqlite3_exec(dbPtrForLock, _sql, null, IntPtr.Zero, IntPtr.Zero);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(_sql);
                    }
                    if (ret != SQLite3.SQLITE3_OK)
                    {
                        return null;
                    }
                    locked = true;
                    return new _Lock(this);
                }
                return null;
            }
        }

        // Lockを外に公開するインターフェース
        public interface Lock : IDisposable { }
        
        // Lockの実体
        private class _Lock : Lock
        {
            SQLite3DB db;
            public _Lock(SQLite3DB db)
            {
                this.db = db;
            }
            public void Dispose()
            {
                db.UnLock();
                GC.SuppressFinalize(this);
            }
            ~_Lock(){
                this.Dispose();
            }
        }

        private void UnLock()
        {
            lock (this)
            {
                if (!locked) return;
                if (dbPtrForLock != IntPtr.Zero)
                {
                    IntPtr _sql = Marshal.StringToHGlobalAnsi("ROLLBACK;");
                    try
                    {
                        sqlite3_exec(dbPtrForLock, _sql, null, IntPtr.Zero, IntPtr.Zero);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(_sql);
                    }
                    locked = false;
                }
            }
        }
        #endregion

        public object[] FetchRow(string table, int rowid) // FIXME: NOT SECURE?
        {
            object[] ret;
            try
            {
                using (var stmt = this.Prepare("SELECT * FROM " + table + " WHERE ROWID=?;"))
                {
                    stmt.Bind(1, rowid.ToString());
                    ret = stmt.EvaluateFirstROW();
                }
            }
            catch (SQLite3Exception)
            {
                ret = new object[]{};
            }
            return ret;
        }

        public void FetchRowRange(string table, int rowid_start, int count, object[][] collection)
        {
            int start = Math.Min(rowid_start, collection.Length);
            int end = Math.Min(rowid_start + count, collection.Length);
            for (int i = start; i < end; i++)
            {
                object[] ret;
                try
                {
                    using (var stmt = this.Prepare("SELECT * FROM " + table + " WHERE ROWID=?;"))
                    {
                        stmt.Bind(1, (i+1).ToString());
                        ret = stmt.EvaluateFirstROW();
                    }
                }
                catch (SQLite3Exception)
                {
                    ret = new object[] { };
                }
                collection[i] = ret;
            }
        }

        public void Exec(string sql)
        {
            Exec(sql, null);
        }
        public delegate int Exec_callback(IntPtr p, int argc, string[] argv, string[] columnNames);
        public void Exec(string sql, Exec_callback callback)
        {
            lock (this)
            {
                IntPtr _sql = Marshal.StringToHGlobalAnsi(sql);
                int ret = -1;
                try
                {
                    ret = sqlite3_exec(dbPtr, _sql, callback, (IntPtr)0, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(_sql);
                }
                if (ret != SQLite3.SQLITE3_OK)
                {
                    throw (new SQLite3Exception("exec error."));
                }
            }
        }

        public STMT Prepare(string sql)
        {
            return new SQLite3DB._STMT(this, sql);
        }


        /** STMT 外からSTMTのインスタンスを生成できないように
         */
        public abstract class STMT : IDisposable
        {
            public delegate void dCallback(Object[] o);
            public abstract void Evaluate(dCallback cb);
            public abstract object[][] EvaluateAll();
            public abstract object[] EvaluateFirstROW();
            public abstract void Dispose();
            public abstract void Reset();
            public abstract void Bind(int index,string value);
        }

        private class _STMT : STMT
        {
            SQLite3DB db;
            IntPtr stmt;
            public _STMT(SQLite3DB db, String sql)
            {
                this.db = db;
                int ret = -1;
                IntPtr _sql = Marshal.StringToHGlobalUni(sql);
                try
                {
                    ret = sqlite3_prepare16(db.dbPtr, _sql, sql.Length * 2, out stmt, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(_sql);
                }
                if (ret != SQLite3.SQLITE3_OK)
                {
                    throw new SQLite3Exception("prepare error\n" + Marshal.PtrToStringUni(sqlite3_errmsg16(db.dbPtr)));
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
                while (SQLite3.SQLITE3_ROW == (r = sqlite3_step(this.stmt)))
                {
                    int N = sqlite3_column_count(stmt);
                    object[] o = new string[N];
                    IntPtr wstr;
                    for (int i = 0; i < N; i++)
                    {
                        wstr = sqlite3_column_text16(stmt, i);
                        o[i] = (wstr == IntPtr.Zero ? null : Marshal.PtrToStringUni(wstr));
                    }
                    cb(o);
                }
                if (r != SQLite3.SQLITE_DONE)
                {
                    throw new SQLite3Exception(Marshal.PtrToStringUni(sqlite3_errmsg16(db.dbPtr)));
                }
                Reset();
            }
            public override object[][] EvaluateAll()
            {
                List<object[]> data = new List<object[]>();
                int r;
                int N = sqlite3_column_count(stmt);
                while (SQLite3.SQLITE3_ROW == (r = sqlite3_step(this.stmt)))
                {
                    object[] o = new string[N];
                    IntPtr wstr;
                    for (int i = 0; i < N; i++)
                    {
                        wstr = sqlite3_column_text16(stmt, i);
                        o[i] = (wstr == IntPtr.Zero ? null : Marshal.PtrToStringUni(wstr));
                    }
                    data.Add(o);
                }
                if (r != SQLite3.SQLITE_DONE)
                {
                    throw new SQLite3Exception(Marshal.PtrToStringUni(sqlite3_errmsg16(db.dbPtr)));
                }
                Reset();
                return data.ToArray();
            }
            public override object[] EvaluateFirstROW()
            {
                int r;
                int N = sqlite3_column_count(stmt);
                r = sqlite3_step(this.stmt);
                object[] o = new string[N];
                if (r == SQLite3.SQLITE3_ROW)
                {
                    IntPtr wstr;
                    for (int i = 0; i < N; i++)
                    {
                        wstr = sqlite3_column_text16(stmt, i);
                        o[i] = (wstr == IntPtr.Zero ? null : Marshal.PtrToStringUni(wstr));
                    }
                }
                r = sqlite3_step(this.stmt);
                if (r != SQLite3.SQLITE_DONE)
                {
                    throw new SQLite3Exception(Marshal.PtrToStringUni(sqlite3_errmsg16(db.dbPtr)));
                }
                Reset();
                return o;
            }
        }
        #region ユーザ定義関数(create_function)関係
        /*
         * 
         * sqlite側に直接登録するのはproxy関数である。
         * proxy関数はuser_dataをもとにユーザ定義関数の実体を呼び出す。
         * ユーザ定義関数の引数はstringの配列
         * 戻り値は任意のオブジェクトである。
         * 戻り値は型によって適当なデータ型としてSQLite側に返される。
         * 
         */
        // Userが追加した関数を表す構造体
        struct SQLite3UserDefineFunctionObject
        {
            public SQLite3UserDefineFunction func;
            public _XFunc proxy;
            public string name;
        }
        // Userが追加した関数を保持(GC対策)
        private List<SQLite3UserDefineFunctionObject> userDefinedFunctions = new List<SQLite3UserDefineFunctionObject>();

        // ネイティブライブラリからのコールバック関数のためのデリゲート(内部用)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _XFunc(IntPtr sqlite3_context, int n, IntPtr sqlite3_value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _XStep(IntPtr sqlite3_context, int n, IntPtr sqlite3_value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void _XFinal(IntPtr sqlite3_context);

        // ユーザ定義関数のデリゲート
        public delegate object SQLite3UserDefineFunction(string[] arg);

        public int createFunction(string name, int args, SQLite3.TextEncoding encoding, SQLite3UserDefineFunction xFunc)
        {
            _XFunc proxy = new _XFunc(xFuncProxy);
            SQLite3UserDefineFunctionObject strt = new SQLite3UserDefineFunctionObject { func = xFunc, proxy = proxy, name = name };
            userDefinedFunctions.Add(strt);
            return _sqlite3_create_function16(dbPtr, name, args, (int)encoding, (IntPtr)userDefinedFunctions.IndexOf(strt), proxy, null, null);
        }

        // ユーザ定義関数の実行を中継する
        void xFuncProxy(IntPtr sqlite3_context, int n, IntPtr sqlite3_value)
        {
            // 引数を取得
            string[] arg = new string[n];
            for (int i = 0; i < n; i++)
            {
                IntPtr valuestr = sqlite3_value_text16(Marshal.ReadIntPtr(sqlite3_value, i * IntPtr.Size));
                String str = Marshal.PtrToStringUni(valuestr);
                arg[i] = str;
            }

            // ユーザ定義関数の実体を取得し、実行
            int id = sqlite3_user_data(sqlite3_context).ToInt32();
            object ret = userDefinedFunctions[id].func(arg);
            if (ret == null)
            {
                sqlite3_result_null(sqlite3_context);
            }
            else if ((ret is double) || (ret is float))
            {
                sqlite3_result_double(sqlite3_context, (double)ret);
            }
            else if (ret is Int64)
            {
                sqlite3_result_int64(sqlite3_context, (Int64)ret);
            }
            else if (ret is Int32)
            {
                sqlite3_result_int(sqlite3_context, (int)ret);
            }
            else if (ret is string)
            {
                sqlite3_result_text16(sqlite3_context, (string)ret, -1, SQLite3.SQLITE_TRANSIENT);
            }
        }
        #endregion

        #region DLL Import
        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_open16", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open16(IntPtr filename, out IntPtr db);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_enable_load_extension", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_enable_load_extension(IntPtr db, int onoff);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_interrupt", CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_interrupt(IntPtr db);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_exec", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db,IntPtr sql,Exec_callback callback,IntPtr p2, IntPtr err);

        //        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_prepare",CharSet=CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
//        private static extern int sqlite3_prepare(IntPtr db, byte[] sql, int sqllen, out IntPtr stmt, IntPtr err);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_prepare16", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare16(IntPtr db, IntPtr sql, int sqllen, out IntPtr stmt, IntPtr tail);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_reset", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_reset(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_step", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_finalize", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_count", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_count(IntPtr stmt);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_int", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_int(IntPtr stmt, int columnId);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_text", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr stmt, int columnId);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_column_text16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text16(IntPtr stmt, int columnId);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_bind_text", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text(IntPtr stmt,int id,IntPtr str,int len,IntPtr dispose);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_bind_text16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_text16(IntPtr stmt,int id,string str,int len,UInt32 dispose);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_result_null", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_result_null(IntPtr sqlite3_context);

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_result_text16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern void sqlite3_result_text16(IntPtr sqlite3_context,string str,int length,UInt32 dispose);

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

        [DllImport("sqlite3.dll", EntryPoint = "sqlite3_create_function16", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int _sqlite3_create_function16(IntPtr db, string zFunctionName, int nArg, int eTextRep, IntPtr pApp, _XFunc xfunc, _XStep xStep, _XFinal xFinal);

        #endregion
    }
}
