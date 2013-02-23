using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Gageas.Wrapper.BASS;
using Gageas.Wrapper.SQLite3;
using Gageas.Lutea;
using Gageas.Lutea.Core;
using Gageas.Lutea.Util;
using Gageas.Lutea.Library;
using Gageas.Lutea.Tags;
using KaoriYa.Migemo;

using System.Runtime.InteropServices; // dllimport

namespace Gageas.Lutea.Core
{

    /// <summary>
    /// Streamを保持。内部的に使用する
    /// </summary>
    class StreamObject
    {
        public StreamObject(BASS.Stream stream, string file_name, ulong cueOffset = 0, ulong cueLength = 0){
            this.stream = stream;
            this.file_name = file_name;  // DBのfile_nameをそのまま入れる。.cueの場合あり
            this.cueOffset = cueOffset;
            this.cueLength = cueLength;
        }
        public BASS.Stream stream;
        public string file_name;
        public string cueStreamFileName = null;
        public ulong cueOffset = 0;
        public ulong cueLength = 0;
        public bool invalidateCueLengthOnSeek = false;
        public object[] meta;
        public double? gain;
        public bool ready = false;
        public bool playbackCounterUpdated = false;
    }

    class AppCore
    {
        private const string settingFileName = "settings.dat";

        internal static List<Lutea.Core.LuteaComponentInterface> plugins = new List<Core.LuteaComponentInterface>();


        /// <summary>
        /// アプリケーションのコアスレッド。
        /// WASAPIの初期化・解放などを担当する
        /// </summary>
        #region Core Thread
        private static WorkerThread coreWorker = new WorkerThread();
        public static void CoreEnqueue(Controller.VOIDVOID d)
        {
            coreWorker.AddTask(d);
        }
        #endregion

        #region Settigs
        internal static bool EnableReplayGain = true;
        internal static double ReplaygainGainBoost = 5.0;
        internal static double NoReplaygainGainBoost = 0.0;
        internal static bool enableWASAPIExclusive = true;
        internal static bool fadeInOutOnSkip = false;
        internal static bool UseMigemo = true;
        internal static string preferredDeviceName = "";
        internal static Migemo migemo = null;
        #endregion

        internal static string PlaylistSortColumn = null;
        internal static Controller.SortOrders PlaylistSortOrder = Controller.SortOrders.Asc;

        #region set/get Volume
        private static float _volume = 1.0F;
        internal static float volume
        {
            get
            {
                return _volume;
//                return outputManager.volume;
            }
            set
            {
                _volume = value;
                if (!mute)
                {
                    outputManager.Volume = value;
                }
            }

        }
        #endregion
        
        #region set/get Mute
        private static bool _mute = false;
        internal static bool mute
        {
            get
            {
                return _mute;
            }
            set
            {
                if (mute == value) return;
                if (outputManager.Available)
                {
                    if (value)
                    {
                        outputManager.SetVolume(0, 0);
                    }
                    else
                    {
                        outputManager.SetVolume(_volume, 0);
                    }
                }
                _mute = value;
            }

        }
        #endregion

        #region set/get Pause
        internal static bool pause
        {
            get
            {
                return outputManager.Pause;
            }
            set
            {
                outputManager.Pause = value;
            }
        }
        #endregion

        private static Boolean floatingPointOutput = false;
        internal static StreamObject currentStream; // 再生中のストリーム
        internal static StreamObject preparedStream;

        internal static object[][] playlistCache;

        private static bool initialized = false;

        private static Object dblock = new Object();
        internal static Boolean isPlaying;

        private static OutputManager outputManager = new OutputManager(StreamProc);
        internal static bool outputChannelIsReady = false;
        
        #region set/get Playback Order
        private static Controller.PlaybackOrder _playbackOrder;
        internal static Controller.PlaybackOrder playbackOrder
        {
            get
            {
                return _playbackOrder;
            }
            set
            {
                _playbackOrder = value;
            }
        }
        #endregion
        
        #region get Output Mode
        internal static Controller.OutputModeEnum OutputMode
        {
            get
            {
                return outputManager.OutputMode;
            }
        }
        internal static Controller.Resolutions OutputResolution
        {
            get
            {
                return outputManager.OutputResolution;
            }
        }
        #endregion

        internal static UserDirectory userDirectory;
        private static MusicLibrary library;
        internal static MusicLibrary Library
        {
            get
            {
                return library;
            }
        }
        
        internal static SQLite3DB h2k6db;
        internal static int currentPlaylistRows;

        internal static uint FFTData(float[] buffer, Wrapper.BASS.BASS.IPlayable.FFT fftopt)
        {
            if (outputManager.Available)
            {
                return outputManager.GetDataFFT(buffer, fftopt);
            }
            else
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = 0.0F;
                }
            }
            return 0;
        }

        internal static void SetPosition(double value)
        {
            var _current = currentStream;
            if (_current == null) return;
            outputManager.Pause = true;
            if (_current.invalidateCueLengthOnSeek)
            {
                _current.cueLength = 0;
            }
            _current.stream.position = _current.stream.Seconds2Bytes(value) + _current.cueOffset;
            outputManager.Start();

        }

        #region ストリームプロシージャ
        private static uint readStreamGained(IntPtr buffer, uint length, BASS.Stream stream, double gaindB)
        {
            uint read = stream.GetData(buffer, length);
            if (read == 0xffffffff) return read;
            uint read_size = read & 0x7fffffff;
            if (EnableReplayGain)
            {
                LuteaHelper.ApplyGain(buffer, read_size, gaindB, OutputMode == Controller.OutputModeEnum.WASAPI || OutputMode == Controller.OutputModeEnum.WASAPIEx ? volume : 1.0);
            }
            return read;
        }

        /// <summary>
        /// 超重要
        /// 出力側からの要求に対して音声データを渡すコールバック関数です
        /// ここでは"必ず"要求された量のデータを埋めて返します。
        /// 0. バッファ全体をゼロフィル
        /// 1. currentStreamから読み込めるだけ読む
        /// 2. 1.でバッファが埋まらなかった場合、preparedStreamから読み込めるだけ読む
        /// 3. 2.でバッファが埋まらなくても、バッファが最後まで埋まったとして返す
        ///    （音声データが書き込まれなかったブロックは0. により無音となる）
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private static uint StreamProc(IntPtr buffer, uint length)
        {
            // 参照コピー
            var _current = currentStream;
            var _prepare = preparedStream;
            uint read1 = 0xffffffff;
            uint read2 = 0xffffffff;

            // prepareからも読み込めなかった時、バッファをゼロフィルして返す（無音）
            ZeroMemory(buffer, length);
            // BASSのデフォルトで0.01秒毎ぐらいにコールされるっぽい。(WASAPI時?)
            // try-catchのコストが気になるのでリリースビルドでは除去する。通常例外おきないはず。
#if DEBUG
            try
            {
#endif
            if (outputChannelIsReady && outputManager.Available)
            {
                // currentStreamから読み出し
                if (_current != null && _current.stream != null && _current.ready)
                {
                    if ((_current.cueLength > 0) && length + _current.stream.position > _current.cueLength + _current.cueOffset)
                    {
                        uint toread = (uint)(_current.cueLength + _current.cueOffset - _current.stream.position);
                        read1 = readStreamGained(buffer, toread, _current.stream, _current.gain == null ? NoReplaygainGainBoost : (ReplaygainGainBoost + _current.gain ?? 0));
                    }
                    else
                    {
                        read1 = readStreamGained(buffer, length, _current.stream, _current.gain == null ? NoReplaygainGainBoost : (ReplaygainGainBoost + _current.gain ?? 0));
                    }
                }
                if (read1 != 0xffffffff && ((read1 &= 0x7fffffff) == length)) return length;

                // バッファの最後まで読み込めなかった時、prepareStreamからの読み込みを試す
                if (read1 == 0xffffffff) read1 = 0;
                if (_prepare != null && _prepare.stream != null && _prepare.ready)
                {
                    read2 = readStreamGained((IntPtr)((ulong)buffer + read1), length - read1, _prepare.stream, _prepare.gain == null ? NoReplaygainGainBoost : (ReplaygainGainBoost + _prepare.gain ?? 0));
                }
                onFinish(BASS.SYNC_TYPE.END, _current);
            }
#if DEBUG
            }
            catch (Exception e) { Logger.Log(e.ToString()); }
#endif
            return length;
        }
        #endregion

        internal static int IndexInPlaylist(string file_name)
        {
            try
            {
                var stmt = h2k6db.Prepare("SELECT ROWID FROM " + GetPlaylistTableName() + " WHERE file_name = ?;");
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
        /// アプリケーション全体の初期化
        /// </summary>
        /// <returns></returns>
        internal static System.Windows.Forms.Form Init()
        {
            System.Windows.Forms.Form componentAsMainForm = null;
            if (initialized) return null;
            SetDllDirectoryW("");

            // migemoのロード
            try
            {
                migemo = new Migemo(@"dict\migemo-dict");
            }
            catch(Exception e){
                Logger.Error(e);
            }

            // userDirectoryオブジェクト取得
            userDirectory = new UserDirectory();

            // ライブラリ準備
            library = userDirectory.OpenLibrary();

            // コンポーネントの読み込み
            // Core Componentをロード
            plugins.Clear();
            plugins.Add(new Core.CoreComponent());
            try
            {
                string[] components = System.IO.Directory.GetFiles(userDirectory.ComponentDir, "*.dll");
                foreach (var component_file in components)
                {
                    Lutea.Core.LuteaComponentInterface p = null;
                    try
                    {
                        //アセンブリとして読み込む
                        System.Reflection.Assembly asm =
                            System.Reflection.Assembly.LoadFrom(component_file);
                        foreach (Type t in asm.GetTypes())
                        {
                            //アセンブリ内のすべての型について、
                            //プラグインとして有効か調べる
                            if (t.IsClass && t.IsPublic && !t.IsAbstract &&
                                t.GetInterface(typeof(Lutea.Core.LuteaComponentInterface).FullName) != null)
                            {
                                p = (Lutea.Core.LuteaComponentInterface)asm.CreateInstance(t.FullName);
                                plugins.Add(p);
                                if (componentAsMainForm == null && p is System.Windows.Forms.Form)
                                {
                                    componentAsMainForm = (System.Windows.Forms.Form)p;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e.ToString());
                    }
                }
            }
            catch (Exception ee) { Logger.Error(ee.ToString()); }

            // load Plugins Settings
            Dictionary<Guid, object> pluginSettings = null;
            try
            {
                using (var fs = new System.IO.FileStream(settingFileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    pluginSettings = (Dictionary<Guid, object>)(new BinaryFormatter()).Deserialize(fs);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            if (pluginSettings == null)
            {
                pluginSettings = new Dictionary<Guid, object>();
            }

            // initialize plugins
            foreach (var pin in plugins)
            {
                if (pin != null)
                {
                    try
                    {
                        var t = pin.GetType();
                        if (pluginSettings.ContainsKey(t.GUID))
                        {
                            pin.Init(pluginSettings[t.GUID]);
                        }
                        else
                        {
                            pin.Init(null);
                        }
                    }
                    catch (Exception e) { Logger.Error(e); }
                }
            }

            if (BASS.IsAvailable)
            {
                BASS.BASS_Init(0);
                if (System.IO.Directory.Exists(userDirectory.PluginDir))
                {
                    String[] dllList = System.IO.Directory.GetFiles(userDirectory.PluginDir, "*.dll");
                    foreach (String dllFilename in dllList)
                    {
                        bool success = BASSPlugin.Load(dllFilename, 0);
                        Logger.Log("Loading " + dllFilename + (success ? " OK" : " Failed"));
                    }
                }
                if (BASS.Floatable)
                {
                    floatingPointOutput = true;
                    Logger.Log("Floating point output is supported");
                }
                else
                {
                    floatingPointOutput = false;
                    Logger.Log("Floating point output is NOT supported");
                }
                BASS.BASS_SetConfig(BASS.BASS_CONFIG.BASS_CONFIG_BUFFER, 500);
            }

            Controller.Startup();
            initialized = true;

            return componentAsMainForm;
        }

        internal static void Reload(Column[] extraColumns)
        {
            coreWorker.AddTask(() =>
            {
                FinalizeApp();
                library.AlternateLibraryDB(extraColumns);
                System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
                Quit();
            });
        }

        public void SetLibraryColumns(Library.Column[] columns)
        {
        }

        /// <summary>
        /// アプリケーション全体の終了
        /// </summary>
        private static bool FinalizeProcess = false;
        internal static void Quit()
        {
            try
            {
                FinalizeApp();
            }
            catch (Exception ex)
            {
                Logger.Log(ex.ToString());
            }
            finally
            {
                Environment.Exit(0);
            }
        }

        private static void FinalizeApp()
        {
            if (FinalizeProcess) return;
            FinalizeProcess = true;
            isPlaying = false;
            initialized = false;
            outputManager.KillOutputChannel();

            if (currentStream != null && currentStream.stream != null)
            {
                currentStream.stream.Dispose();
            }

            // Quit plugins and save setting
            using (var fs = new System.IO.FileStream(settingFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
            {
                Dictionary<Guid, object> pluginSettings = new Dictionary<Guid, object>();
                foreach (var p in plugins)
                {
                    try
                    {
                        pluginSettings.Add(p.GetType().GUID, p.GetSetting());
                    }
                    catch(Exception e) {
                        Logger.Error(e);
                    }
                    finally
                    {
                        try { p.Quit(); }
                        catch (Exception ex)
                        {
                            Logger.Error(ex);
                        }
                    }
                }
                (new BinaryFormatter()).Serialize(fs, pluginSettings);
            }
        }

        /*
         * DBに問い合わせてプレイリストを生成するための処理郡
         * 
         * 構造:
         * 1.createPlaylistProcをバックグラウンドスレッドとして起動しsleepさせておく
         * 2.createPlaylistが呼ばれたらplaylistQueryQueueに問い合わせ文字列をセットしてcreatePlaylistProcにinterrupt
         * 3.createPlaylistProcがplaylistQueryQueueを読んでプレイリストを生成
         * 
         * 3.でのプレイリスト生成中にも割り込みをかけてリセットできるようになっている
         */
        #region Create Playlist
        private static Thread playlistCreateThread;
        private static String playlistQueryQueue = null;
        private static Boolean PlayOnCreate = false;
        internal static String latestPlaylistQuery = "SELECT * FROM list;";
        internal static String LatestPlaylistQueryExpanded = "";
        private static readonly object playlistQueryQueueLock = new object();
        internal static void createPlaylist(String sql, bool playOnCreate = false)
        {
            Logger.Log("createPlaylist " + sql);
            lock (playlistQueryQueueLock)
            {
                playlistQueryQueue = sql;
                PlayOnCreate = playOnCreate;
                if (playlistCreateThread == null || playlistCreateThread.IsAlive == false)
                {
                    playlistCreateThread = new Thread(createPlaylistProc);
                    playlistCreateThread.Start();
                    playlistCreateThread.IsBackground = true;
                    playlistCreateThread.Priority = ThreadPriority.BelowNormal;
                }
                else
                {
                    if (h2k6db != null)
                    {
                        h2k6db.interrupt();
                    }
                    playlistCreateThread.Interrupt();
                }
            }
        }

        private static string GetMigemoSTMT(string sql)
        {
            if (!UseMigemo) throw new System.NotSupportedException("migemo is not enabled.");
            if (migemo == null) throw new System.NotSupportedException("migemo is not enabled.");

            string[] words = sql.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            string[] migemo_phrase = new string[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string not = "";
                if (word[0] == '-')
                {
                    not = "NOT ";
                    word = word.Substring(1);
                }
                migemo_phrase[i] = not + "migemo( '" + word.EscapeSingleQuotSQL() + "' , " + String.Join("||'\n'||",GetSearchTargetColumns()) + ")";
            }
            return "SELECT * FROM list WHERE " + String.Join(" AND ", migemo_phrase) + ";";
        }
        private static string GetRegexpSTMT(string sql)
        {
            // prepareできねぇ・・・
            Match match = new Regex(@"^\/(.+)\/[a-z]*$").Match(sql);
            if (match.Success)
            {
                return "SELECT * FROM list WHERE " + String.Join("||'\n'||", GetSearchTargetColumns()) + " regexp  '" + sql.EscapeSingleQuotSQL() + "' ;";
            }
            else
            {
                throw new System.ArgumentException();
            }
        }

        private static string[] GetSearchTargetColumns()
        {
            return Library.Columns.Where(_ => _.IsTextSearchTarget).Select(_ => _.Name).ToArray();
        }

        private static SQLite3DB.STMT prepareForCreatePlaylistView(SQLite3DB db, string subquery)
        {
            var stmt = db.Prepare("CREATE TEMP TABLE unordered_playlist AS " + subquery + " ;");
            // prepareが成功した場合のみ以下が実行される
            LatestPlaylistQueryExpanded = subquery;
            return stmt;
        }

        internal static void CreateOrderedPlaylist(string column, Controller.SortOrders sortOrder)
        {
            playlistCache = new object[playlistCache.Length][];
            CreateOrderedPlaylistTableInDB();
            Controller._PlaylistUpdated(null);
        }

        private static void CreateOrderedPlaylistTableInDB()
        {
            try
            {
                h2k6db.Exec("DROP TABLE IF EXISTS playlist;");
            }
            catch (SQLite3DB.SQLite3Exception ee)
            {
                Logger.Log(ee);
            }

            if (PlaylistSortColumn == null) return;

            var orderPhrase = "";
            orderPhrase = " ORDER BY ";
            switch (Library.Columns[Controller.GetColumnIndexByName(PlaylistSortColumn)].Type)
            {
                case LibraryColumnType.Bitrate:
                case LibraryColumnType.FileSize:
                case LibraryColumnType.Integer:
                case LibraryColumnType.Rating:
                case LibraryColumnType.Timestamp64:
                case LibraryColumnType.TrackNumber:
                case LibraryColumnType.Time:
                    orderPhrase += "list." + PlaylistSortColumn + "-0";
                    break;
                default:
                    orderPhrase += "list." + PlaylistSortColumn + "||''";
                    break;
            }

            orderPhrase += PlaylistSortOrder == Controller.SortOrders.Asc ? " ASC " : " DESC ";
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    h2k6db.Exec("CREATE TEMP TABLE playlist AS SELECT list.* FROM list, unordered_playlist WHERE list.file_name == unordered_playlist.file_name " + orderPhrase + " ;");
                    break;
                }
                catch (SQLite3DB.SQLite3Exception) { }
                Thread.Sleep(50);
            }
        }

        delegate SQLite3DB.STMT CreatePlaylistParser();
        private static void createPlaylistTableInDB(string sql)
        {
            try
            {
                // Luteaで生成するplaylistをdrop
                try
                {
                    h2k6db.Exec("DROP TABLE IF EXISTS playlist;");
                    h2k6db.Exec("DROP TABLE IF EXISTS unordered_playlist;");
                    Logger.Debug("playlist TABLE(Lutea type) DROPed");
                }
                catch (SQLite3DB.SQLite3Exception e) {
                    // H2k6で生成するplaylistをdrop
                    try
                    {
                        h2k6db.Exec("DROP VIEW IF EXISTS playlist;");
                        h2k6db.Exec("DROP TABLE IF EXISTS unordered_playlist;");
                        Logger.Debug("playlist TABLE(H2k6 type) DROPed");
                    }
                    catch (SQLite3DB.SQLite3Exception ee)
                    {
                        Logger.Error(e);
                        Logger.Log(ee);
                    }
                }

                using (SQLite3DB.Lock dbLock = h2k6db.GetLock("list"))
                using (SQLite3DB.STMT tmt = Util.Util.TryThese<SQLite3DB.STMT>(new CreatePlaylistParser[]{
                                    ()=>prepareForCreatePlaylistView(h2k6db, sql==""?"SELECT * FROM list":sql),
                                    ()=>prepareForCreatePlaylistView(h2k6db,GetRegexpSTMT(sql)),
                                    ()=>prepareForCreatePlaylistView(h2k6db,GetMigemoSTMT(sql)),
                                    ()=>prepareForCreatePlaylistView(h2k6db,"SELECT * FROM list WHERE " + String.Join("||'\n'||", GetSearchTargetColumns()) + " like '%" + sql.EscapeSingleQuotSQL() + "%';"),
                                }, null))
                {
                    if (tmt == null) return;
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            tmt.Evaluate(null);
                            break;
                        }
                        catch (SQLite3DB.SQLite3Exception) { }
                        Thread.Sleep(50);
                    }

                    //createPlaylistからinterruptが連続で発行されたとき、このsleep内で捕捉する
                    Thread.Sleep(10);

                    using (SQLite3DB.STMT tmt2 = h2k6db.Prepare("SELECT COUNT(*) FROM unordered_playlist ;"))
                    {
                        Logger.Debug("Creating new playlist " + sql);
                        tmt2.Evaluate((o) => currentPlaylistRows = int.Parse(o[0].ToString()));
                        Logger.Debug("Start Playlist Loader");
                        if (PlaylistSortColumn != null)
                        {
                            CreateOrderedPlaylistTableInDB();
                        }

                        // プレイリストキャッシュ用の配列を作成
                        playlistCache = new object[currentPlaylistRows][];
                        latestPlaylistQuery = sql;
                    }
                }
            }
            catch (SQLite3DB.SQLite3Exception e)
            {
                Logger.Log(e.ToString());
            }
        }
        private static void createPlaylistProc()
        {
            while (true)
            {
                h2k6db = h2k6db ?? Library.Connect(true);
                try
                {
                    String sql;
                    bool playOnCreate = false;
                    lock (playlistQueryQueueLock)
                    {
                        sql = playlistQueryQueue;
                        playOnCreate = PlayOnCreate;
                    }
                    if (sql == null)
                    {
                        Logger.Debug("Entering sleep");
                        Thread.Sleep(System.Threading.Timeout.Infinite);
                    } // 待機

                    //createPlaylistからinterruptが連続で発行されたとき、このsleep内で捕捉する
                    Thread.Sleep(10);

                    lock (playlistQueryQueueLock)
                    {
                        sql = playlistQueryQueue;
                        playlistQueryQueue = null;
                        PlayOnCreate = false;
                    }
                    Logger.Debug("start to create playlist " + sql);
                    lock (dblock)
                    {
                        createPlaylistTableInDB(sql);

                        // プレイリスト生成完了コールバックを呼ぶ
                        Logger.Debug("コールバックをすりゅ");
                        Controller._PlaylistUpdated(sql);

                        if (playOnCreate) PlayPlaylistItem(0);
                    }
                }
                catch (ThreadInterruptedException) { }
            }
        }

        private static string GetPlaylistTableName()
        {
            return PlaylistSortColumn == null ? "unordered_playlist" : "playlist";
        }


        public static object[] GetPlaylistRow(int index)
        {
            if (index < 0) return null;
            // このメソッドの呼び出し中にcacheの参照が変わる可能性があるので、最初に参照をコピーする
            // 一時的に古いcacheの内容を吐いても問題ないので、mutexで固めるほどではない
            var _cache = playlistCache;
            if (_cache == null) return null;
            if (_cache.Length <= index) return null;
            object[] value = null;
            if (_cache[index] == null) _cache[index] = h2k6db.FetchRow(GetPlaylistTableName(), index + 1);
            value = _cache[index];
            if (value == null || value.Length == 0) return null;
            return value;
        }

        private const int PlaylistPreCacheCount = 40;
        private static void updateCache()
        {
            Logger.Debug("Playlist Loader done. " + currentPlaylistRows + " items in playlist");
        }
        #endregion

        #region メディアファイルの再生に関する処理郡
        private static object prepareMutex = new object();
        internal static Boolean PlayPlaylistItem(int index, bool stopCurrent = true)
        {
            Logger.Debug("Enter PlayPlaylistItem");
            CoreEnqueue((Controller.VOIDVOID)(() =>
            {
                lock (prepareMutex)
                {
                    outputChannelIsReady = false;
                    if (preparedStream != null)
                    {
                        preparedStream.stream.Dispose();
                        preparedStream = null;
                    }
                    if (outputManager.Available)
                    {
                        currentStream.ready = false;
                        if (stopCurrent)
                        {
                            outputManager.SetVolume(-1F, fadeInOutOnSkip ? 100u : 0u);
                        }
                    }
                    prepareNextStream(index);
                    if (outputManager.Available && stopCurrent)
                    {
                        outputManager.Stop();
                    }
                    PlayQueuedStream(stopCurrent);
                }
            }));
            Logger.Debug("Leave PlayPlaylistItem");
            return true;
        }

        private static void PlayQueuedStream(bool stopcurrent=false){
            lock (prepareMutex)
            {
                outputChannelIsReady = false;
                isPlaying = false;
                // 再生するstreamが用意されているかどうかチェック
                if (preparedStream == null)
                {
                    Logger.Log("Playback Error");
                    stop();
                    Controller._OnPlaybackErrorOccured();
                    AppCore.CoreEnqueue(() => { System.Threading.Thread.Sleep(500); Controller.NextTrack(); });
                    return;
                }

                if (IndexInPlaylist(preparedStream.file_name) == -1)
                {
                    preparedStream = null;
                    prepareNextStream(getSuccTrackIndex());
                    PlayQueuedStream();
                    return;
                }

                // Output Streamを再構築
                if (outputManager.RebuildRequired(preparedStream.stream) || pause)
                {
                    var fname = preparedStream.file_name;
                    var freq = preparedStream.stream.GetFreq();
                    var chans = preparedStream.stream.GetChans();
                    var isFloat = (preparedStream.stream.Info.Flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) > 0;
                    try
                    {
                        outputManager.KillOutputChannel(!stopcurrent);
                    }
                    catch (Exception e) { Logger.Error(e); }
                    preparedStream.stream.Dispose();
                    preparedStream = null;
                    if (currentStream != null && currentStream.stream != null)
                    {
                        currentStream.stream.Dispose();
                        currentStream = null;
                    }
                    pause = false;
                    outputManager.ResetOutputChannel(freq, chans, isFloat, preferredDeviceName);
                    outputManager.SetVolume(_volume, 0);
                    prepareNextStream(IndexInPlaylist(fname));
                    PlayQueuedStream();
                    return;
                }

                // currentのsyncを解除
                if (currentStream != null && currentStream.stream != null)
                {
                    currentStream.stream.clearAllSync();
                }

                // prepareにsyncを設定
                if (preparedStream.cueLength > 0 || preparedStream.cueOffset > 0)
                {
                    preparedStream.stream.setSync(BASS.SYNC_TYPE.POS, d_on80Percent, preparedStream.cueOffset + (ulong)(preparedStream.cueLength * 0.80));
                    preparedStream.stream.setSync(BASS.SYNC_TYPE.POS, d_onPreFinish, preparedStream.cueOffset + (ulong)(Math.Max(preparedStream.cueLength * 0.90, preparedStream.cueLength - preparedStream.stream.Seconds2Bytes(5))));
                }
                else
                {
                    preparedStream.stream.setSync(BASS.SYNC_TYPE.POS, d_on80Percent, (ulong)(preparedStream.stream.filesize * 0.80));
                    preparedStream.stream.setSync(BASS.SYNC_TYPE.POS, d_onPreFinish, (ulong)(Math.Max(preparedStream.stream.filesize * 0.90, preparedStream.stream.filesize - preparedStream.stream.Seconds2Bytes(5))));
                }

                if (currentStream != null && currentStream.stream != preparedStream.stream)
                {
                    currentStream.stream.Dispose();
                }
                currentStream = null;
                preparedStream.ready = true;
                preparedStream.playbackCounterUpdated = false;
                currentStream = preparedStream;
                outputManager.SetVolume((float)_volume, fadeInOutOnSkip ? 100u : 0u);
                outputManager.Resume();
                outputChannelIsReady = true;
                preparedStream = null;
                pause = false;
                isPlaying = true;
                Controller._OnTrackChange(Controller.Current.IndexInPlaylist);
                BASS.SetPriority(System.Diagnostics.ThreadPriorityLevel.TimeCritical);
                BASSWASAPIOutput.SetPriority(System.Diagnostics.ThreadPriorityLevel.TimeCritical);
            }
        }

        private static StreamObject getStreamObjectCUE(CD cd, int index, BASS.Stream.StreamFlag flag, BASS.Stream newstream = null)
        {
            CD.Track track = cd.tracks[index];
            String streamFullPath = System.IO.Path.IsPathRooted(track.file_name_CUESheet)
                ? track.file_name_CUESheet
                : Path.GetDirectoryName(cd.filename) + Path.DirectorySeparatorChar + track.file_name_CUESheet;
            if (newstream == null)
            {
                Logger.Log("new Stream opened " + streamFullPath);
                newstream = new BASS.FileStream(streamFullPath, flag);
            }
            if (newstream == null)
            {
                return null;
            }
            ulong offset = (ulong)track.start * (newstream.GetFreq() / 75) * newstream.GetChans() * sizeof(float);
            ulong length = track.end > track.start
                ? (ulong)(track.end - track.start) * (newstream.GetFreq() / 75) * newstream.GetChans() * sizeof(float)
                : newstream.filesize - offset;
            StreamObject nextStream = new StreamObject(newstream, cd.filename, offset, length);
            if (!outputManager.RebuildRequired(newstream)) nextStream.ready = true;
            nextStream.cueStreamFileName = streamFullPath;
            var gain = track.getTagValue("ALBUM GAIN");
            if (gain != null)
            {
                nextStream.gain = Util.Util.parseDouble(gain.ToString());
            }
            return nextStream;
        }
        private static StreamObject getStreamObject(string filename, int tagTracknumber, BASS.Stream.StreamFlag flag, List<KeyValuePair<string,object>> tag = null)
        {
            StreamObject nextStream;
            Logger.Log(String.Format("Trying to play file {0}", filename));

            // case for CUE sheet
            if (Path.GetExtension(filename).Trim().ToUpper() == ".CUE")
            {
                CD cd = CUEparser.fromFile(filename, false);
                nextStream = getStreamObjectCUE(cd, tagTracknumber - 1, flag);
            }
            else
            {
                BASS.Stream newstream = new BASS.FileStream(filename, flag);
                if (newstream == null)
                {
                    return null;
                }
                if (tag == null)
                {
                    tag = Tags.MetaTag.readTagByFilename(filename.Trim(), false);
                }
                KeyValuePair<string, object> cue = tag.Find((match) => match.Key == "CUESHEET");

                // case for Internal CUESheet
                if (cue.Key != null)
                {
                    CD cd = CUEparser.fromString(cue.Value.ToString(), filename, false);
                    nextStream = getStreamObjectCUE(cd, tagTracknumber - 1, flag, newstream);
                    nextStream.cueStreamFileName = filename.Trim();
                }
                else
                {
                    nextStream = new StreamObject(newstream, filename);
                    KeyValuePair<string, object> gain = tag.Find((match) => match.Key == "REPLAYGAIN_ALBUM_GAIN");
                    if (gain.Value != null)
                    {
                        nextStream.gain = Util.Util.parseDouble(gain.Value.ToString());
                    }
                    KeyValuePair<string, object> iTunSMPB = tag.Find((match) => match.Key.ToUpper() == "ITUNSMPB");
                    if (iTunSMPB.Value != null)
                    {
                        var smpbs = iTunSMPB.Value.ToString().Trim().Split(new char[] { ' ' }).Select(_ => System.Convert.ToUInt64(_, 16)).ToArray();
                        // ライブラリで既に補正されている場合は何もしない
                        if (newstream.filesize > (smpbs[3]) * newstream.GetChans() * sizeof(float))
                        {
                            // ref. http://nyaochi.sakura.ne.jp/archives/2006/09/15/itunes-v70070%E3%81%AE%E3%82%AE%E3%83%A3%E3%83%83%E3%83%97%E3%83%AC%E3%82%B9%E5%87%A6%E7%90%86/
                            nextStream.cueOffset = (smpbs[1] + smpbs[2]) * newstream.GetChans() * sizeof(float);
                            nextStream.cueLength = (smpbs[3]) * newstream.GetChans() * sizeof(float);
                            nextStream.invalidateCueLengthOnSeek = true;
                        }
                    }
                    else
                    {
                        var lametag = Lametag.Read(filename.Trim());
                        if (lametag != null)
                        {
                            // ライブラリで既に補正されている場合は何もしない
                            if (nextStream.stream.filesize > newstream.filesize - (ulong)(lametag.delay + lametag.padding) * newstream.GetChans() * sizeof(float))
                            {
                                nextStream.cueOffset = (ulong)(lametag.delay) * newstream.GetChans() * sizeof(float);
                                nextStream.cueLength = newstream.filesize - (ulong)(lametag.delay + lametag.padding) * newstream.GetChans() * sizeof(float);
                                nextStream.invalidateCueLengthOnSeek = true;
                            }
                        }
                    }
                }
                if (!outputManager.RebuildRequired(newstream)) nextStream.ready = true;
            }
            return nextStream;
        }
        internal static int lastPreparedIndex;
        private static Boolean prepareNextStream(int index, List<KeyValuePair<string,object>> tags = null)
        {
            lock (prepareMutex)
            {
                lastPreparedIndex = index;
                if (preparedStream != null) return false;
                if (index >= currentPlaylistRows || index < 0)
                {
                    preparedStream = null;
                    return false;
                }
                object[] row = Controller.GetPlaylistRow(index);
                string filename = (string)row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)];
                BASS.Stream.StreamFlag flag = BASS.Stream.StreamFlag.BASS_STREAM_DECODE;
                if (floatingPointOutput) flag |= BASS.Stream.StreamFlag.BASS_STREAM_FLOAT;
                StreamObject nextStream = null;
                try
                {
                    int tr = 1;
                    Util.Util.tryParseInt(row[Controller.GetColumnIndexByName("tagTracknumber")].ToString(), ref tr);
                    nextStream = getStreamObject(filename, tr, flag, tags);
                    if (nextStream == null)
                    {
                        preparedStream = null;
                        return false;
                    }
                    if (nextStream.cueOffset < 30000)
                    {
                        ulong left = nextStream.cueOffset;
                        var mem = Marshal.AllocHGlobal(1000);
                        while (left > 0)
                        {
                            int toread = (int)Math.Min(1000, left);
                            nextStream.stream.GetData(mem, (uint)toread);
                            Thread.Sleep(0);
                            left -= (uint)toread;
                        }
                        Marshal.FreeHGlobal(mem);
                    }
                    else
                    {
                        nextStream.stream.position = nextStream.cueOffset;
                    }
                    nextStream.meta = row;
                    preparedStream = nextStream;
                    return true;
                }
                catch (Exception e)
                {
                    preparedStream = null;
                    Logger.Log(e.ToString());
                    return false;
                }
            }
        }

        internal static void stop()
        {
            isPlaying = false;
            outputManager.KillOutputChannel();
            if (currentStream == null) return;
            if (currentStream.stream != null) currentStream.stream.Dispose();
            currentStream = null;
            Controller._OnTrackChange(-1);
        }
        #endregion

        #region トラック終端でのイベント
        private static void UpdatePlaybackCount(StreamObject strm)
        {
            if (strm.playbackCounterUpdated) return;
            strm.playbackCounterUpdated = true;
            var currentIndexInPlaylist = Controller.Current.IndexInPlaylist;
            int count = int.Parse(Controller.Current.MetaData(Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.playcount))) + 1;
            string file_name = Controller.Current.MetaData(Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name));
            Logger.Log("再生カウントを更新しまつ" + file_name + ",  " + count);
            CoreEnqueue(() =>
            {
                // db書き込み
                CoreEnqueue(() =>
                {
                    using (var db = library.Connect(false)) {
                        using (var stmt = db.Prepare("UPDATE list SET " + LibraryDBColumnTextMinimum.lastplayed + " = current_timestamp64(), " + LibraryDBColumnTextMinimum.playcount + " = " + count + " WHERE " + LibraryDBColumnTextMinimum.file_name + " = '" + file_name.EscapeSingleQuotSQL() + "';"))
                        {
                            stmt.Evaluate(null);
                        }
                    }
                });
                var row = Controller.GetPlaylistRow(currentIndexInPlaylist);
                if (row != null)
                {
                    row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.playcount)] = (int.Parse(row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.playcount)].ToString()) + 1).ToString();
                    row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.lastplayed)] = (MusicLibrary.currentTimestamp).ToString();
                }
                Controller._PlaylistUpdated(null);
            });
        }
        private static BASS.Channel.SyncProc d_onFinish = new BASS.Channel.SyncProc(onFinish);
        private static BASS.Channel.SyncProc d_onPreFinish = new BASS.Channel.SyncProc(onPreFinish);
        private static BASS.Channel.SyncProc d_on80Percent = new BASS.Channel.SyncProc(on80Percent);
        private static void on80Percent(BASS.SYNC_TYPE type, object cookie)
        {
            UpdatePlaybackCount(currentStream);
        }
        private static void onFinish(BASS.SYNC_TYPE type, object cookie)
        {
            if (cookie != currentStream) return;
            if (!currentStream.ready) return;
            currentStream.ready = false;

            UpdatePlaybackCount(currentStream);
            if (preparedStream == null)
            {
                Controller.NextTrack(false);
            }
            else
            {
                CoreEnqueue(() => { PlayQueuedStream(); });
            }
        }

        private static void onPreFinish(BASS.SYNC_TYPE type, object cookie)
        {
            Logger.Log("preSync");
            var th = new Thread(() => {
                var row = Controller.GetPlaylistRow(getSuccTrackIndex());
                if (row == null) return;
                string filename = (string)row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)];
                List<KeyValuePair<string,object>> tag = null;
                if (File.Exists(filename))
                {
                    byte[] buf = new byte[10 * 1000];
                    using (var f = File.OpenRead(filename))
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            f.Read(buf, 0, buf.Count());
                            Thread.Sleep(0);
                        }
                    }
                    tag = MetaTag.readTagByFilename(filename, false);
                }
                CoreEnqueue(() => prepareNextStream(getSuccTrackIndex(), tag));
            });
            th.Priority = ThreadPriority.Lowest;
            th.Start();
        }

        internal static int getSuccTrackIndex() // ストリーム終端に達した場合の次のトラックを取得
        {
            int id;
            if (playbackOrder == Controller.PlaybackOrder.Track)
            {
                return Controller.Current.IndexInPlaylist;
            }
            if (playbackOrder == Controller.PlaybackOrder.Random)
            {
                if (currentPlaylistRows == 1) return 0;
                do
                {
                    id = (new Random()).Next(currentPlaylistRows);
                } while (id == Controller.Current.IndexInPlaylist);
                return id;
            }
            else
            {
                id = (Controller.Current.IndexInPlaylist) + 1;
                if (id >= currentPlaylistRows)
                {
                    id = 0;
                }
                return id;
            }
        }
        #endregion

        /// <summary>
        /// 外部からデータベースに書き込んだ後に呼ぶ
        /// </summary>
        /// <param name="silent"></param>
        #region DatabaseUpdated
        internal static void DatabaseUpdated(bool silent = false)
        {
            string latest = latestPlaylistQuery;
            if (!silent)
            {
                if (h2k6db != null)
                {
                    h2k6db.Dispose();
                    h2k6db = null;
                }
                createPlaylist(latestPlaylistQuery);
                Controller._OnDatabaseUpdated();
            }
        }
        #endregion

        [DllImport("Kernel32.dll")]
		private static extern bool SetDllDirectoryW(string lpPathName);

        [DllImport("Kernel32.dll")]
        private static extern void ZeroMemory(IntPtr dest, UInt32 length);
    }
}
