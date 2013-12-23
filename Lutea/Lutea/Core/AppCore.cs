using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
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

        private static CoreComponent MyCoreComponent = new CoreComponent();
        /// <summary>
        /// アプリケーションのコアスレッド。
        /// WASAPIの初期化・解放などを担当する
        /// </summary>
        private static WorkerThread CoreWorker = new WorkerThread();
        private static OutputManager OutputManager = new OutputManager(StreamProc);
        internal static List<Lutea.Core.LuteaComponentInterface> Plugins = new List<Core.LuteaComponentInterface>();
        internal static List<Assembly> Assemblys = new List<Assembly>();
        internal static Migemo Migemo = null;

        internal static UserDirectory userDirectory;
        internal static SQLite3DB h2k6db;
        internal static int currentPlaylistRows;

        internal static StreamObject CurrentStream; // 再生中のストリーム
        internal static StreamObject PreparedStream;

        internal static object[][] PlaylistCache;
        internal static int[] TagAlbumContinuousCount;

        private static Object DBlock = new Object();

        private static bool UseFloatingPointOutput = false; 
        internal static bool IsPlaying;
        internal static bool OutputChannelIsReady = false;

        internal static MusicLibrary Library { get; private set; }

        private static BASS.Channel.SyncProc d_onFinish = new BASS.Channel.SyncProc(onFinish);
        private static BASS.Channel.SyncProc d_onPreFinish = new BASS.Channel.SyncProc(onPreFinish);
        private static BASS.Channel.SyncProc d_on80Percent = new BASS.Channel.SyncProc(on80Percent);

        internal static int lastPreparedIndex;

        #region Properies
        public static bool EnableWASAPIExclusive
        {
            get
            {
                return MyCoreComponent.EnableWASAPIExclusive;
            }
        }

        public static float Volume
        {
            get
            {
                return MyCoreComponent.Volume;
            }
            set
            {
                MyCoreComponent.Volume = value;
                OutputManager.Volume = value;
            }
        }

        internal static bool Pause
        {
            get
            {
                return OutputManager.Pause;
            }
            set
            {
                OutputManager.Pause = value;
            }
        }
                
        internal static Controller.OutputModeEnum OutputMode
        {
            get
            {
                return OutputManager.OutputMode;
            }
        }

        internal static Controller.Resolutions OutputResolution
        {
            get
            {
                return OutputManager.OutputResolution;
            }
        }

        public static Controller.SortOrders PlaylistSortOrder
        {
            get
            {
                return MyCoreComponent.PlaylistSortOrder;
            }
            set
            {
                MyCoreComponent.PlaylistSortOrder = value;
            }
        }

        public static Controller.PlaybackOrder PlaybackOrder
        {
            get
            {
                return MyCoreComponent.PlaybackOrder;
            }
            set
            {
                MyCoreComponent.PlaybackOrder = value;
            }
        }

        public static string PlaylistSortColumn
        {
            get
            {
                return MyCoreComponent.PlaylistSortColumn;
            }
            set
            {
                MyCoreComponent.PlaylistSortColumn = value;
            }
        }

        public static string LatestPlaylistQuery
        {
            get
            {
                return MyCoreComponent.LatestPlaylistQuery;
            }
        }

        public static Importer.ImportableTypes TypesToImport
        {
            get
            {
                return MyCoreComponent.ImportTypes.ToEnum();
            }
        }
        #endregion

        public static void CoreEnqueue(Controller.VOIDVOID d)
        {
            CoreWorker.AddTask(d);
        }

        internal static uint FFTData(float[] buffer, Wrapper.BASS.BASS.IPlayable.FFT fftopt)
        {
            if (OutputManager.Available)
            {
                return OutputManager.GetDataFFT(buffer, fftopt);
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
            var _current = CurrentStream;
            if (_current == null) return;
            OutputManager.Pause = true;
            if (_current.invalidateCueLengthOnSeek)
            {
                _current.cueLength = 0;
            }
            _current.stream.position = _current.stream.Seconds2Bytes(value) + _current.cueOffset;
            OutputManager.Start();

        }

        #region ストリームプロシージャ
        private static uint ReadStreamGained(IntPtr buffer, uint length, BASS.Stream stream, double gaindB)
        {
            uint read = stream.GetData(buffer, length);
            if (read == 0xffffffff) return read;
            uint read_size = read & 0x7fffffff;
            if (MyCoreComponent.EnableReplayGain)
            {
                LuteaHelper.ApplyGain(buffer, read_size, gaindB, OutputMode == Controller.OutputModeEnum.WASAPI || OutputMode == Controller.OutputModeEnum.WASAPIEx ? Volume : 1.0);
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
            var _current = CurrentStream;
            uint read1 = 0xffffffff;
            // currentStreamから読み出し
            if ((!OutputChannelIsReady) || (!OutputManager.Available)) return 0;
            if (_current != null && _current.stream != null && _current.ready)
            {
                uint toread = length;
                if ((_current.cueLength > 0) && length + _current.stream.position > _current.cueLength + _current.cueOffset)
                {
                    toread = (uint)(_current.cueLength + _current.cueOffset - _current.stream.position);
                }
                read1 = ReadStreamGained(buffer, toread, _current.stream, _current.gain == null ? MyCoreComponent.NoReplaygainGainBoost : (MyCoreComponent.ReplaygainGainBoost + _current.gain ?? 0));
            }
            if (read1 == 0xffffffff || read1 == 0)
            {
                read1 = 0;
                onFinish(BASS.SYNC_TYPE.END, _current);
            }
            return read1;
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
            SetDllDirectoryW("");

            // migemoのロード
            try
            {
                Migemo = new Migemo(@"dict\migemo-dict");
            }
            catch(Exception e){
                Logger.Error(e);
            }

            // userDirectoryオブジェクト取得
            userDirectory = new UserDirectory();

            // ライブラリ準備
            Library = userDirectory.OpenLibrary();

            // コンポーネントの読み込み
            // Core Componentをロード
            Plugins.Add(MyCoreComponent);
            try
            {
                foreach (var component_file in System.IO.Directory.GetFiles(userDirectory.ComponentDir, "*.dll"))
                {
                    try
                    {
                        //アセンブリとして読み込む
                        var asm = System.Reflection.Assembly.LoadFrom(component_file);
                        var types = asm.GetTypes().Where(_ => _.IsClass && _.IsPublic && !_.IsAbstract && _.GetInterface(typeof(Lutea.Core.LuteaComponentInterface).FullName) != null);
                        foreach (Type t in types)
                        {
                            var p = (Lutea.Core.LuteaComponentInterface)asm.CreateInstance(t.FullName);
                            if (p == null) continue;
                            Plugins.Add(p);
                            Assemblys.Add(asm);
                            if (componentAsMainForm == null && p is System.Windows.Forms.Form)
                            {
                                componentAsMainForm = (System.Windows.Forms.Form)p;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }
            }
            catch (Exception ee)
            {
                Logger.Error(ee);
            }

            // load Plugins Settings
            Dictionary<Guid, object> pluginSettings = new Dictionary<Guid, object>();
            try
            {
                using (var fs = new System.IO.FileStream(settingFileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                    pluginSettings = (Dictionary<Guid, object>)(new BinaryFormatter()).Deserialize(fs);
                    AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve); 
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            // initialize plugins
            foreach (var pin in Plugins)
            {
                try
                {
                    pin.Init(pluginSettings.FirstOrDefault(_ => _.Key == pin.GetType().GUID).Value);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
            
            createPlaylist(MyCoreComponent.LatestPlaylistQuery);

            if (BASS.IsAvailable)
            {
                BASS.BASS_Init(0);
                if (System.IO.Directory.Exists(userDirectory.PluginDir))
                {
                    foreach (String dllFilename in System.IO.Directory.GetFiles(userDirectory.PluginDir, "*.dll"))
                    {
                        bool success = BASSPlugin.Load(dllFilename, 0);
                        Logger.Log("Loading " + dllFilename + (success ? " OK" : " Failed"));
                    }
                }
                UseFloatingPointOutput = BASS.Floatable;
                Logger.Log("Floating point output is " + (UseFloatingPointOutput ? "" : "NOT") + "supported");

                BASS.BASS_SetConfig(BASS.BASS_CONFIG.BASS_CONFIG_BUFFER, 500);
            }

            Controller.Startup();

            return componentAsMainForm;
        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assemblys.First(_ => _.FullName == args.Name);
        }

        internal static void Reload(Column[] extraColumns)
        {
            CoreWorker.AddTask(() =>
            {
                FinalizeApp();
                Library.AlternateLibraryDB(extraColumns);
                System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
                Quit();
            });
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
            IsPlaying = false;
            OutputManager.KillOutputChannel();

            if (CurrentStream != null && CurrentStream.stream != null)
            {
                CurrentStream.stream.Dispose();
            }

            // Quit plugins and save setting
            using (var fs = new System.IO.FileStream(settingFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
            {
                Dictionary<Guid, object> pluginSettings = new Dictionary<Guid, object>();
                foreach (var p in Plugins)
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

        internal static void ActivateUI()
        {
            foreach (var plg in Plugins)
            {
                if (plg is LuteaUIComponentInterface)
                {
                    ((LuteaUIComponentInterface)plg).ActivateUI();
                }
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
            if (!MyCoreComponent.UseMigemo) throw new System.NotSupportedException("migemo is not enabled.");
            if (Migemo == null) throw new System.NotSupportedException("migemo is not enabled.");

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
            PlaylistCache = new object[PlaylistCache.Length][];
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

            if (MyCoreComponent.PlaylistSortColumn == null)
            {
                LuteaHelper.ClearRepeatCount(currentPlaylistRows);
                h2k6db.Exec("SELECT __x_lutea_count_continuous(tagAlbum) FROM unordered_playlist ;");
                TagAlbumContinuousCount = (int[])LuteaHelper.counter.Clone();
                return;
            }

            var orderPhrase = "";
            orderPhrase = " ORDER BY ";
            switch (Library.Columns[Controller.GetColumnIndexByName(MyCoreComponent.PlaylistSortColumn)].Type)
            {
                case LibraryColumnType.Bitrate:
                case LibraryColumnType.FileSize:
                case LibraryColumnType.Integer:
                case LibraryColumnType.Rating:
                case LibraryColumnType.Timestamp64:
                case LibraryColumnType.TrackNumber:
                case LibraryColumnType.Time:
                    orderPhrase += "list." + MyCoreComponent.PlaylistSortColumn + "-0";
                    break;
                default:
                    orderPhrase += "list." + MyCoreComponent.PlaylistSortColumn + "||'' COLLATE NOCASE ";
                    break;
            }

            orderPhrase += MyCoreComponent.PlaylistSortOrder == Controller.SortOrders.Asc ? " ASC " : " DESC ";
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

            LuteaHelper.ClearRepeatCount(currentPlaylistRows);
            h2k6db.Exec("SELECT __x_lutea_count_continuous(tagAlbum) FROM " + GetPlaylistTableName() + " ;");
            TagAlbumContinuousCount = (int[])LuteaHelper.counter.Clone();
        }

        delegate string CreatePlaylistParser();
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
                catch (SQLite3DB.SQLite3Exception e)
                {
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
                {
                    SQLite3DB.STMT tmt = null;
                    foreach (var dlg in new CreatePlaylistParser[]{
                        ()=>sql==""?"SELECT * FROM list":sql,
                        ()=>GetRegexpSTMT(sql),
                        ()=>GetMigemoSTMT(sql),
                        ()=>"SELECT * FROM list WHERE " + String.Join("||'\n'||", GetSearchTargetColumns()) + " like '%" + sql.EscapeSingleQuotSQL() + "%';"})
                    {
                        try
                        {
                            tmt = prepareForCreatePlaylistView(h2k6db, dlg());
                            break;
                        }
                        catch (Exception) { }
                    };
                    if (tmt == null) return;
                    using (tmt)
                    {
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
                            tmt2.Evaluate((o) => currentPlaylistRows = int.Parse(o[0].ToString()));
                            if (MyCoreComponent.PlaylistSortColumn != null)
                            {
                                CreateOrderedPlaylistTableInDB();
                            }
                            else
                            {
                                LuteaHelper.ClearRepeatCount(currentPlaylistRows);
                                h2k6db.Exec("SELECT __x_lutea_count_continuous(tagAlbum) FROM " + GetPlaylistTableName() + " ;");
                                TagAlbumContinuousCount = (int[])LuteaHelper.counter.Clone();
                            }

                            // プレイリストキャッシュ用の配列を作成
                            PlaylistCache = new object[currentPlaylistRows][];
                            MyCoreComponent.LatestPlaylistQuery = sql;
                        }
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
                    lock (DBlock)
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
            return MyCoreComponent.PlaylistSortColumn == null ? "unordered_playlist" : "playlist";
        }

        public static object[] GetPlaylistRow(int index)
        {
            if (index < 0) return null;
            // このメソッドの呼び出し中にcacheの参照が変わる可能性があるので、最初に参照をコピーする
            // 一時的に古いcacheの内容を吐いても問題ないので、mutexで固めるほどではない
            var _cache = PlaylistCache;
            if (_cache == null) return null;
            if (_cache.Length <= index) return null;
            object[] value = null;
            if (_cache[index] == null) _cache[index] = h2k6db.FetchRow(GetPlaylistTableName(), index + 1);
            if ((_cache[index].Length == 0) || (_cache[index][0] == null)) return null;
            value = _cache[index];
            if (value == null || value.Length == 0) return null;
            return value;
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
                    OutputChannelIsReady = false;
                    if (PreparedStream != null)
                    {
                        PreparedStream.stream.Dispose();
                        PreparedStream = null;
                    }
                    if (OutputManager.Available)
                    {
                        CurrentStream.ready = false;
                        if (stopCurrent)
                        {
                            OutputManager.SetVolume(-1F, MyCoreComponent.FadeInOutOnSkip ? 100u : 0u);
                        }
                    }
                    prepareNextStream(index);
                    if (OutputManager.Available && stopCurrent)
                    {
                        OutputManager.Stop();
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
                OutputChannelIsReady = false;
                IsPlaying = false;
                // 再生するstreamが用意されているかどうかチェック
                if (PreparedStream == null)
                {
                    Logger.Log("Playback Error");
                    stop();
                    Controller._OnPlaybackErrorOccured();
                    AppCore.CoreEnqueue(() => { System.Threading.Thread.Sleep(500); Controller.NextTrack(); });
                    return;
                }

                if (IndexInPlaylist(PreparedStream.file_name) == -1)
                {
                    PreparedStream = null;
                    prepareNextStream(getSuccTrackIndex());
                    PlayQueuedStream();
                    return;
                }

                // Output Streamを再構築
                if (OutputManager.RebuildRequired(PreparedStream.stream) || Pause)
                {
                    var fname = PreparedStream.file_name;
                    var freq = PreparedStream.stream.GetFreq();
                    var chans = PreparedStream.stream.GetChans();
                    var isFloat = (PreparedStream.stream.Info.Flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) > 0;
                    try
                    {
                        OutputManager.KillOutputChannel(!stopcurrent);
                    }
                    catch (Exception e) { Logger.Error(e); }
                    PreparedStream.stream.Dispose();
                    PreparedStream = null;
                    if (CurrentStream != null && CurrentStream.stream != null)
                    {
                        CurrentStream.stream.Dispose();
                        CurrentStream = null;
                    }
                    Pause = false;
                    OutputManager.ResetOutputChannel(freq, chans, isFloat, (uint)MyCoreComponent.BufferLength, MyCoreComponent.PreferredDeviceName);
                    OutputManager.SetVolume(Volume, 0);
                    prepareNextStream(IndexInPlaylist(fname));
                    PlayQueuedStream();
                    return;
                }

                // currentのsyncを解除
                if (CurrentStream != null && CurrentStream.stream != null)
                {
                    CurrentStream.stream.clearAllSync();
                }

                // prepareにsyncを設定
                if (PreparedStream.cueLength > 0 || PreparedStream.cueOffset > 0)
                {
                    PreparedStream.stream.setSync(BASS.SYNC_TYPE.POS, d_on80Percent, PreparedStream.cueOffset + (ulong)(PreparedStream.cueLength * 0.80));
                    PreparedStream.stream.setSync(BASS.SYNC_TYPE.POS, d_onPreFinish, PreparedStream.cueOffset + (ulong)(Math.Max(PreparedStream.cueLength * 0.90, PreparedStream.cueLength - PreparedStream.stream.Seconds2Bytes(5))));
                }
                else
                {
                    PreparedStream.stream.setSync(BASS.SYNC_TYPE.POS, d_on80Percent, (ulong)(PreparedStream.stream.filesize * 0.80));
                    PreparedStream.stream.setSync(BASS.SYNC_TYPE.POS, d_onPreFinish, (ulong)(Math.Max(PreparedStream.stream.filesize * 0.90, PreparedStream.stream.filesize - PreparedStream.stream.Seconds2Bytes(5))));
                }

                if (CurrentStream != null && CurrentStream.stream != PreparedStream.stream)
                {
                    CurrentStream.stream.Dispose();
                }
                CurrentStream = null;
                PreparedStream.ready = true;
                PreparedStream.playbackCounterUpdated = false;
                CurrentStream = PreparedStream;
                OutputManager.SetVolume((float)Volume, MyCoreComponent.FadeInOutOnSkip ? 100u : 0u);
                OutputManager.Resume();
                OutputChannelIsReady = true;
                PreparedStream = null;
                Pause = false;
                IsPlaying = true;
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
                : Path.GetDirectoryName(track.file_name) + Path.DirectorySeparatorChar + track.file_name_CUESheet;
            if (newstream == null)
            {
                Logger.Log("new Stream opened " + streamFullPath);
                newstream = new BASS.FileStream(streamFullPath, flag);
            }
            if (newstream == null)
            {
                return null;
            }
            ulong offset = (ulong)track.Start * (newstream.GetFreq() / 75) * newstream.GetChans() * sizeof(float);
            ulong length = track.End > track.Start
                ? (ulong)(track.End - track.Start) * (newstream.GetFreq() / 75) * newstream.GetChans() * sizeof(float)
                : newstream.filesize - offset;
            StreamObject nextStream = new StreamObject(newstream, track.file_name, offset, length);
            if (!OutputManager.RebuildRequired(newstream)) nextStream.ready = true;
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

            filename = filename.Trim();

            // case for CUE sheet
            if (Path.GetExtension(filename).ToUpper() == ".CUE")
            {
                CD cd = CUEReader.ReadFromFile(filename, false);
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
                    tag = Tags.MetaTag.readTagByFilename(filename, false);
                }
                KeyValuePair<string, object> cue = tag.Find((match) => match.Key == "CUESHEET");

                // case for Internal CUESheet
                if (cue.Key != null)
                {
                    CD cd = CUEReader.ReadFromString(cue.Value.ToString(), filename, false);
                    nextStream = getStreamObjectCUE(cd, tagTracknumber - 1, flag, newstream);
                    nextStream.cueStreamFileName = filename;
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
                        var lametag = Lametag.Read(filename);
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
                if (!OutputManager.RebuildRequired(newstream)) nextStream.ready = true;
            }
            return nextStream;
        }
        private static Boolean prepareNextStream(int index, List<KeyValuePair<string,object>> tags = null)
        {
            lock (prepareMutex)
            {
                lastPreparedIndex = index;
                if (PreparedStream != null) return false;
                if (index >= currentPlaylistRows || index < 0)
                {
                    PreparedStream = null;
                    return false;
                }
                object[] row = Controller.GetPlaylistRow(index);
                string filename = (string)row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)];
                BASS.Stream.StreamFlag flag = BASS.Stream.StreamFlag.BASS_STREAM_DECODE;
                if (UseFloatingPointOutput) flag |= BASS.Stream.StreamFlag.BASS_STREAM_FLOAT;
                StreamObject nextStream = null;
                try
                {
                    int tr = 1;
                    Util.Util.tryParseInt(row[Controller.GetColumnIndexByName("tagTracknumber")].ToString(), ref tr);
                    nextStream = getStreamObject(filename, tr, flag, tags);
                    if (nextStream == null)
                    {
                        PreparedStream = null;
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
                    PreparedStream = nextStream;
                    return true;
                }
                catch (Exception e)
                {
                    PreparedStream = null;
                    Logger.Log(e.ToString());
                    return false;
                }
            }
        }

        internal static void stop()
        {
            IsPlaying = false;
            OutputManager.KillOutputChannel();
            if (CurrentStream == null) return;
            if (CurrentStream.stream != null) CurrentStream.stream.Dispose();
            CurrentStream = null;
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
            CoreEnqueue(() =>
            {
                Logger.Log("再生カウントを更新しまつ" + file_name + ",  " + count);
                // db書き込み
                using (var db = Library.Connect(false))
                {
                    using (var stmt = db.Prepare("UPDATE list SET " + LibraryDBColumnTextMinimum.lastplayed + " = current_timestamp64(), " + LibraryDBColumnTextMinimum.playcount + " = " + count + " WHERE " + LibraryDBColumnTextMinimum.file_name + " = '" + file_name.EscapeSingleQuotSQL() + "';"))
                    {
                        stmt.Evaluate(null);
                    }
                }
                var row = Controller.GetPlaylistRow(currentIndexInPlaylist);
                if (row != null)
                {
                    row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.playcount)] = (int.Parse(row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.playcount)].ToString()) + 1).ToString();
                    row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.lastplayed)] = (MusicLibrary.currentTimestamp).ToString();
                }
                Controller._PlaylistUpdated(null);
            });
        }

        private static void on80Percent(BASS.SYNC_TYPE type, object cookie)
        {
            UpdatePlaybackCount(CurrentStream);
        }

        private static void onFinish(BASS.SYNC_TYPE type, object cookie)
        {
            if (cookie != CurrentStream) return;
            if (!CurrentStream.ready) return;
            CurrentStream.ready = false;

            UpdatePlaybackCount(CurrentStream);
            if (PreparedStream == null)
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
                var succIndex = getSuccTrackIndex();
                var row = Controller.GetPlaylistRow(succIndex);
                if (row == null) return;
                string filename = (string)row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)];
                CoreEnqueue(() => prepareNextStream(succIndex, null));
            });
            th.Priority = ThreadPriority.Lowest;
            th.Start();
        }

        internal static int getSuccTrackIndex() // ストリーム終端に達した場合の次のトラックを取得
        {
            int id;
            if (MyCoreComponent.PlaybackOrder == Controller.PlaybackOrder.Track)
            {
                return Controller.Current.IndexInPlaylist;
            }
            if (MyCoreComponent.PlaybackOrder == Controller.PlaybackOrder.Random)
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
            string latest = MyCoreComponent.LatestPlaylistQuery;
            if (!silent)
            {
                if (h2k6db != null)
                {
                    h2k6db.Dispose();
                    h2k6db = null;
                }
                createPlaylist(MyCoreComponent.LatestPlaylistQuery);
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
