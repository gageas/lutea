using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Gageas.Lutea.Util;
using Gageas.Lutea.Library;
using Gageas.Wrapper.SQLite3;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// プレイリストを管理するクラス
    /// </summary>
    class PlaylistManager
    {
        /// <summary>
        /// プレイリスト生成要求を表す構造体
        /// </summary>
        private struct CreatePlaylistRequest
        {
            /// <summary>
            /// クエリ文字列
            /// </summary>
            public readonly string QueryText;

            /// <summary>
            /// 生成後再生開始
            /// </summary>
            public readonly bool PlayOnCreate;

            /// <summary>
            /// 新規クエリではなく再実行
            /// </summary>
            public readonly bool IsReloadOnly;

            public CreatePlaylistRequest(string query = null, bool playOnCreate = false, bool isReloadOnly = false)
            {
                QueryText = query;
                PlayOnCreate = playOnCreate;
                IsReloadOnly = isReloadOnly;
            }
        }

        /// <summary>
        /// 現在のプレイリストにおけるtagAlbumの連続数カウントのキャッシュ
        /// </summary>
        internal int[] TagAlbumContinuousCount;

        /// <summary>
        /// 現在のプレイリストの行数
        /// </summary>
        internal int CurrentPlaylistRows;

        /// <summary>
        /// 最後に実行した生成SQL(クエリ文字列をSQLに展開した文字列)
        /// </summary>
        internal String LatestPlaylistQueryExpanded = "";

        /// <summary>
        /// 現在のプレイリストのテーブル名
        /// </summary>
        internal string PlaylistTableName
        {
            get
            {
                return AppCore.PlaylistSortColumn == null ? "unordered_playlist" : "playlist";
            }
        }

        /// <summary>
        /// データベース処理のリトライ回数
        /// </summary>
        private const int RETRY_COUNT = 10;

        /// <summary>
        /// データベース処理リトライ時の遅延時間
        /// </summary>
        private const int RETRY_DELAY = 50;

        /// <summary>
        /// Library.dbのカラムのうち，数値として扱うカラムのリスト
        /// </summary>
        private static readonly IEnumerable<LibraryColumnType> NumericColumnTypes = new List<LibraryColumnType>() {                 
            LibraryColumnType.Bitrate,
            LibraryColumnType.FileSize,
            LibraryColumnType.Integer,
            LibraryColumnType.Rating,
            LibraryColumnType.Timestamp64,
            LibraryColumnType.TrackNumber,
            LibraryColumnType.Time,
        };

        /// <summary>
        /// クエリ文字列をSQLに展開するデリゲートのリスト
        /// </summary>
        private IEnumerable<Func<string, string>> QueryTextExpanders;

        /// <summary>
        /// Library.dbへの接続を保持
        /// </summary>
        private SQLite3DB LibraryDB;

        /// <summary>
        /// プレイリストの行を取得するプリペアドに対するロックオブジェクト
        /// </summary>
        private readonly Object FetchRowStmtLock = new Object();

        /// <summary>
        /// プレイリストの行を取得するプリペアド
        /// </summary>
        private SQLite3DB.STMT FetchRowStmt;

        /// <summary>
        /// プレイリストを生成するスレッド
        /// </summary>
        private Thread PlaylistCreateThread;

        /// <summary>
        /// プレイリスト生成要求(PlaylistCreateThreadから参照)
        /// </summary>
        private CreatePlaylistRequest Request;

        /// <summary>
        /// プレイリスト行の内容のキャッシュ
        /// </summary>
        private object[][] PlaylistCache;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        internal PlaylistManager()
        {
            SetupConnection();
            QueryTextExpanders = new List<Func<string, string>>() {
                (q) => q == "" ? "SELECT * FROM list" : q,
                GetRegexpSQL,
                GetMigemoSQL,
                (q) => "SELECT * FROM allTags WHERE text like '%" + q.EscapeSingleQuotSQL() + "%';"
            };

            PlaylistCreateThread = new Thread(CreatePlaylistProc);
            PlaylistCreateThread.IsBackground = true;
            PlaylistCreateThread.Priority = ThreadPriority.BelowNormal;
            PlaylistCreateThread.Start();
        }

        private void SetupConnection()
        {
            LibraryDB = AppCore.Library.Connect();
            LibraryDB.Exec("CREATE TEMP VIEW allTags AS SELECT *, " + String.Join("||'\n'||", Controller.Columns.Where(_ => _.IsTextSearchTarget).Select(_ => _.Name)) + " AS text FROM list;");
        }

        /// <summary>
        /// 指定したファイル名のプレイリスト内でのIndexを取得
        /// </summary>
        /// <param name="file_name">ファイル名</param>
        /// <returns>0から始まる位置。存在しない場合は-1。</returns>
        internal int GetIndexInPlaylist(string file_name)
        {
            try
            {
                var stmt = LibraryDB.Prepare("SELECT ROWID FROM " + PlaylistTableName + " WHERE file_name = ?;");
                stmt.Bind(1, file_name);
                int ret = 0;
                stmt.Evaluate((o) => ret = int.Parse(o[0].ToString()));
                if (ret > 0) return ret - 1;
            }
            catch (SQLite3DB.SQLite3Exception ex)
            {
                Logger.Debug(ex.ToString());
            }
            return -1;
        }

        /// <summary>
        /// 直前に生成したプレイリストと同クエリでプレイリストを再生成
        /// </summary>
        internal void RefreshPlaylist()
        {
            Request = new CreatePlaylistRequest(query: LatestPlaylistQueryExpanded, isReloadOnly: true);
            LibraryDB.interrupt();
            PlaylistCreateThread.Interrupt();
        }

        /// <summary>
        /// プレイリストを生成
        /// </summary>
        /// <param name="query">クエリ</param>
        /// <param name="playOnCreate">生成後に再生開始</param>
        internal void CreatePlaylist(string query, bool playOnCreate = false)
        {
            Logger.Log("createPlaylist " + query);
            Request = new CreatePlaylistRequest(query: query, playOnCreate: playOnCreate);
            LibraryDB.interrupt();
            PlaylistCreateThread.Interrupt();
        }

        /// <summary>
        /// ソート条件付きのプレイリストを生成
        /// </summary>
        /// <param name="column">ソート基準とするカラム</param>
        /// <param name="sortOrder">ソート順</param>
        internal void CreateOrderedPlaylist(string column, Controller.SortOrders sortOrder)
        {
            PlaylistCache = new object[PlaylistCache.Length][];
            CreateOrderedPlaylistTableInDB();
            Controller._PlaylistUpdated(null);
        }

        /// <summary>
        /// クエリ文字列からMigemoを使用するSQLを生成
        /// </summary>
        /// <param name="queryText">クエリ文字列</param>
        /// <returns>SQL文字列</returns>
        private string GetMigemoSQL(string queryText)
        {
            var migemo = AppCore.MyMigemo;
            if (!AppCore.UseMigemo) throw new System.NotSupportedException("migemo is not enabled.");
            if (migemo == null) throw new System.NotSupportedException("migemo is not enabled.");
            return "SELECT * FROM allTags WHERE " + String.Join(" AND ", queryText
                .EscapeSingleQuotSQL()
                .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(_ =>
                    _.First() == '-'
                        ? (" NOT migemo( '" + _.Substring(1) + "' , text)")
                        : ("migemo( '" + _ + "' , text)")
                    )) + ";";
        }

        /// <summary>
        /// クエリ文字列から正規表現を使用するSQLを生成
        /// </summary>
        /// <param name="queryText">クエリ文字列</param>
        /// <returns>SQL文字列</returns>
        private string GetRegexpSQL(string queryText)
        {
            Match match = new Regex(@"^\/(.+)\/[a-z]*$").Match(queryText);
            if (match.Success)
            {
                return "SELECT * FROM allTags WHERE text regexp  '" + queryText.EscapeSingleQuotSQL() + "' ;";
            }
            else
            {
                throw new System.ArgumentException();
            }
        }

        /// <summary>
        /// サブクエリのSQLからプレイリストを生成するSQLのプリペアドステートメントを生成
        /// </summary>
        /// <param name="subquery">サブクエリ</param>
        /// <exception cref="SQLite3DB.SQLite3Exception">サブクエリが不正または'Readonly'ではないSQL</exception>
        /// <returns>プリペアドステートメント</returns>
        private SQLite3DB.STMT PrepareForCreatePlaylistView(string subquery)
        {
            using (var tmpstmt = LibraryDB.Prepare(subquery + " ;"))
            {
                if (!tmpstmt.IsReadOnly()) throw new SQLite3DB.SQLite3Exception("Query is not readonly");
                var stmt = LibraryDB.Prepare("CREATE TEMP TABLE __temp AS SELECT file_name FROM ( " + subquery.TrimEnd(new char[] { ' ', '\t', '\n', ';' }) + " );");
                // prepareが成功した場合のみ以下が実行される
                LatestPlaylistQueryExpanded = subquery;
                return stmt;
            }
        }

        /// <summary>
        /// プレイリスト内の行を取得するプリペアドステートメントを破棄
        /// </summary>
        private void DisposeFetchStmt()
        {
            lock (FetchRowStmtLock)
            {
                if (FetchRowStmt != null)
                {
                    FetchRowStmt.Dispose();
                    FetchRowStmt = null;
                }
            }
        }

        /// <summary>
        /// 現在の条件でプレイリストをソート
        /// </summary>
        private void CreateOrderedPlaylistTableInDB()
        {
            try
            {
                LibraryDB.Exec("DROP TABLE IF EXISTS playlist;");
            }
            catch (SQLite3DB.SQLite3Exception ee)
            {
                Logger.Log(ee);
            }

            if (AppCore.PlaylistSortColumn != null)
            {
                var orderPhrase = " ORDER BY "
                    + (NumericColumnTypes.Contains(AppCore.Library.Columns[Controller.GetColumnIndexByName(AppCore.PlaylistSortColumn)].Type)
                        ? ("list." + AppCore.PlaylistSortColumn + "-0")
                        : ("list." + AppCore.PlaylistSortColumn + "||'' COLLATE NOCASE "))
                    + (AppCore.PlaylistSortOrder == Controller.SortOrders.Asc
                        ? " ASC "
                        : " DESC ");

                for (int i = 0; i < RETRY_COUNT; i++)
                {
                    try
                    {
                        LibraryDB.Exec("CREATE TEMP TABLE playlist AS SELECT list.file_name, list.tagAlbum FROM list, unordered_playlist WHERE list.file_name == unordered_playlist.file_name " + orderPhrase + " ;");
                        break;
                    }
                    catch (SQLite3DB.SQLite3Exception) { }
                    Thread.Sleep(RETRY_DELAY);
                }
            }

            LuteaHelper.ClearRepeatCount(CurrentPlaylistRows);
            LibraryDB.Exec("SELECT __x_lutea_count_continuous(tagAlbum) FROM " + PlaylistTableName + " ;");
            TagAlbumContinuousCount = (int[])LuteaHelper.counter.Clone();
            DisposeFetchStmt();
        }

        /// <summary>
        /// プレイリストテーブルをDROP
        /// </summary>
        private void DropOldPlaylist()
        {
            // Luteaで生成するplaylistをdrop
            try
            {
                LibraryDB.Exec("DROP TABLE IF EXISTS playlist;");
                LibraryDB.Exec("DROP TABLE IF EXISTS unordered_playlist;");
                LibraryDB.Exec("DROP TABLE IF EXISTS __temp");
                Logger.Debug("playlist TABLE(Lutea type) DROPed");
            }
            catch (SQLite3DB.SQLite3Exception e)
            {
                // H2k6で生成するplaylistをdrop
                try
                {
                    LibraryDB.Exec("DROP VIEW IF EXISTS playlist;");
                    LibraryDB.Exec("DROP TABLE IF EXISTS unordered_playlist;");
                    LibraryDB.Exec("DROP TABLE IF EXISTS __temp");
                    Logger.Debug("playlist TABLE(H2k6 type) DROPed");
                }
                catch (SQLite3DB.SQLite3Exception ee)
                {
                    Logger.Error(e);
                    Logger.Log(ee);
                }
            }
        }

        /// <summary>
        /// クエリ文字列をSQL文に展開してPrepare
        /// </summary>
        /// <param name="queryText">クエリ文字列</param>
        /// <returns>プリペアドステートメントまたはNULL</returns>
        private SQLite3DB.STMT ExpandAndPrepareQueryText(string queryText)
        {
            SQLite3DB.STMT tmt = null;
            for (int i = 0; i < RETRY_COUNT; i++)
            {
                foreach (var dlg in QueryTextExpanders)
                {
                    try
                    {
                        tmt = PrepareForCreatePlaylistView(dlg(queryText));
                        break;
                    }
                    catch (NotSupportedException) { /* nothin to do */ }
                    catch (ArgumentException) { /* nothin to do */ }
                    catch (SQLite3DB.SQLite3Exception e) { Logger.Log(e); }
                };
                if (tmt != null) break;
                Thread.Sleep(RETRY_DELAY);
            }
            return tmt;
        }

        /// <summary>
        /// クエリ文字列からデータベース内にプレイリストを生成
        /// </summary>
        /// <param name="queryText">クエリ文字列</param>
        private void CreatePlaylistTableInDB(string queryText)
        {
            try
            {
                DropOldPlaylist();
                using (var tmt = ExpandAndPrepareQueryText(queryText))
                {
                    if (tmt == null)
                    {
                        Logger.Error("Can't Prepare Query: " + queryText);
                        CurrentPlaylistRows = 0;
                        PlaylistCache = new object[0][];
                        LibraryDB.Dispose();
                        SetupConnection();
                        return;
                    }
                    else
                    {
                        for (int i = 0; i < RETRY_COUNT; i++)
                        {
                            try
                            {
                                tmt.Evaluate(null);
                                break;
                            }
                            catch (SQLite3DB.SQLite3Exception) { }
                            Thread.Sleep(RETRY_DELAY);
                        }
                    }

                    //createPlaylistからinterruptが連続で発行されたとき、このsleep内で捕捉する
                    Thread.Sleep(10);

                    LibraryDB.Exec("CREATE TEMP TABLE unordered_playlist AS SELECT list.file_name, list.tagAlbum FROM __temp JOIN list ON list.file_name = __temp.file_name;");

                    using (var tmt2 = LibraryDB.Prepare("SELECT COUNT(*) FROM unordered_playlist ;"))
                    {
                        tmt2.Evaluate((o) => CurrentPlaylistRows = int.Parse(o[0].ToString()));
                        CreateOrderedPlaylistTableInDB();

                        // プレイリストキャッシュ用の配列を作成
                        PlaylistCache = new object[CurrentPlaylistRows][];
                    }
                }
            }
            catch (SQLite3DB.SQLite3Exception e)
            {
                Logger.Log(e.ToString());
            }
        }

        /// <summary>
        /// プレイリストを生成する(スレッド)
        /// </summary>
        private void CreatePlaylistProc()
        {
            while (true)
            {
                try
                {
                    CreatePlaylistRequest req;
                    if (Request.QueryText == null)
                    {
                        Thread.Sleep(System.Threading.Timeout.Infinite);
                    }

                    //createPlaylistからinterruptが連続で発行されたとき、このsleep内で捕捉する
                    Thread.Sleep(10);

                    req = Request;
                    Request = new CreatePlaylistRequest();

                    CreatePlaylistTableInDB(req.QueryText);
                    Controller._PlaylistUpdated(req.IsReloadOnly ? null : req.QueryText);
                    if (req.PlayOnCreate) AppCore.PlayPlaylistItem(0);
                }
                catch (ThreadInterruptedException) { }
            }
        }

        /// <summary>
        /// プレイリストキャッシュの指定された行を無効にする
        /// </summary>
        /// <param name="index">行番号</param>
        internal void InvalidatePlaylistRowCache(int index)
        {
            if (index < 0 || index >= CurrentPlaylistRows) return;
            PlaylistCache[index] = null;
        }

        /// <summary>
        /// プレイリストの行を取得する(キャッシュ付き)
        /// </summary>
        /// <param name="index">行番号</param>
        /// <returns>行の内容，またはNULL</returns>
        public object[] GetPlaylistRow(int index)
        {
            if (index < 0) return null;
            // このメソッドの呼び出し中にcacheの参照が変わる可能性があるので、最初に参照をコピーする
            // 一時的に古いcacheの内容を吐いても問題ないので、mutexで固めるほどではない
            var _cache = PlaylistCache;
            if (_cache == null) return null;
            if (_cache.Length <= index) return null;
            object[] value = null;
            for (int i = 0; i < RETRY_COUNT; i++)
            {
                if (_cache[index] == null)
                {
                    try
                    {
                        lock (FetchRowStmtLock)
                        {
                            if (FetchRowStmt == null)
                            {
                                FetchRowStmt = LibraryDB.Prepare("SELECT * FROM list WHERE file_name = (SELECT file_name FROM " + PlaylistTableName + " WHERE ROWID=?);");
                            }
                            FetchRowStmt.Bind(1, (index + 1).ToString());
                            _cache[index] = FetchRowStmt.EvaluateFirstROW();
                        }
                    }
                    catch (SQLite3DB.SQLite3Exception) { }
                }
                if ((_cache[index] == null) || (_cache[index].Length == 0) || (_cache[index][0] == null))
                {
                    _cache[index] = null;
                    Thread.Sleep(1);
                }
                else
                {
                    break;
                }
            }
            value = _cache[index];
            if (value == null || value.Length == 0) return null;
            return value;
        }
    }
}
