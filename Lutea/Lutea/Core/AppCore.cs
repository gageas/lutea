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
        private const int BASS_BUFFFER_LEN = 1500;
        private const string settingFileName = "settings.dat";

        internal static List<Lutea.Core.LuteaComponentInterface> plugins = new List<Core.LuteaComponentInterface>();

        // このへん今後後で使う
        private Column[] columns = {
                                       new Column(){nameDB = "file_name", nameDisplay = "ファイル名"}
                                   };

        /// <summary>
        /// アプリケーションのコアスレッド。
        /// WASAPIの初期化・解放などを担当する
        /// </summary>
        #region Core Thread
        private static WorkerThread coreWorker = new WorkerThread();
//        private static WorkerThread outputStreamWorker = new WorkerThread();
        public static void CoreEnqueue(Controller.VOIDVOID d)
        {
            coreWorker.CoreEnqueue(d);
        }
        #endregion

        #region Settigs
        internal static bool EnableReplayGain = true;
        internal static double ReplaygainGainBoost = 5.0;
        internal static double NoReplaygainGainBoost = 0.0;
        internal static bool enableWASAPIExclusive = true;
        internal static bool enableWASAPIVolume = false;
        internal static uint OutputFreq = 44100;
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
                lock (volumeLock)
                {
                    if (AppCore.outputChannel != null)
                    {
                        if (value)
                        {
                            AppCore.outputChannel.SetVolume(0);
                        }
                        else
                        {
                            AppCore.outputChannel.SetVolume(_volume); //.stream.volume = (float)(_volume * Math.Pow(10.0, ((preparedStream.gain ?? (-gainBoost) + gainBoost) / 20.0)));
                        }
                    }
                    AppCore._mute = value;
                }
            }
        }
        #endregion mute

        #region set/get Volume
        private static Object volumeLock = new Object();
        private static float _volume = 1.0F;
        internal static float volume
        {
            get
            {
                return _volume;
            }
            set
            {
                lock (volumeLock)
                {
                    _volume = value;
                    if (!mute && AppCore.outputChannel != null)
                    {
                        AppCore.outputChannel.SetVolume(_volume);
                    }
                }
            }

        }
        #endregion

        #region set/get Pause
        private static bool _pause = false;
        internal static bool pause
        {
            get
            {
                return _pause;
            }
            set
            {
                if (AppCore.currentStream == null) return;
                _pause = value;
                if (value)
                {
                    AppCore.outputChannel.Pause();
                    //                    currentStream.stream.pause();
                }
                else
                {
                    //                    currentStream.stream.play();
                    AppCore.outputChannel.Resume();
                }
            }
        }
        #endregion

        /// <summary>
        /// タグ(ApeTag)からデータベースのColumnへのマッピングを表すDicionary
        /// </summary>
        #region tagColumnMapping
        public static Dictionary<string, DBCol> tagColumnMapping = new Dictionary<string, DBCol>(){
            {"TITLE",DBCol.tagTitle},
            {"ARTIST",DBCol.tagArtist},
            {"ALBUM",DBCol.tagAlbum},
            {"GENRE",DBCol.tagGenre},
            {"DATE",DBCol.tagDate},
            {"COMMENT",DBCol.tagComment},
            {"TRACK",DBCol.tagTracknumber},
            {"LYRICS",DBCol.tagLyrics},
        };
        #endregion


        private static Boolean floatingPointOutput = false;
        internal static StreamObject currentStream; // 再生中のストリーム
        internal static StreamObject preparedStream;
        private static object outputChannelLock = new object();
        internal static BASS.IPlayable outputChannel; // 出力ストリーム

        internal static object[][] playlistCache;

        private static bool initialized = false;

        private static Object dblock = new Object();
        internal static Boolean isPlaying;


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
        private static Controller.OutputModeEnum outputMode;
        internal static Controller.OutputModeEnum OutputMode
        {
            get
            {
                return outputMode;
            }
        }
        #endregion

        internal static UserDirectory userDirectory;
        private static H2k6Library library;
        internal static H2k6Library Library
        {
            get
            {
                return library;
            }
        }
        
        internal static SQLite3DB h2k6db;
        internal static int currentPlaylistRows;

        #region 出力ストリーム関連
        private static bool OutputStreamRebuildRequired(BASS.Stream reference)
        {
            if (reference == null) return true;
            var info = reference.Info;
            uint freq = info.freq;
            uint chans = info.chans;

            return OutputStreamRebuildRequired(freq,chans, (info.flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) != 0);
        }
        private static bool OutputStreamRebuildRequired(uint freq,uint chans, bool useFloat)
        {
            if (outputChannel == null || outputChannel.GetFreq() != freq || outputChannel.GetChans() != chans)// || (outputChannel.Info.flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) != flag)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reference"></param>
        /// <returns>OutputStreamを作り直した時true</returns>
        internal static bool ResetOutputChannel(uint freq, uint chans, bool useFloat){ // BASS.Stream reference
            bool ret = false;
//            var info = reference.Info;
            if(OutputStreamRebuildRequired(freq, chans, useFloat))
            {
                outputMode = Controller.OutputModeEnum.STOP;
                if (outputChannel != null)
                {
                    outputChannel.Dispose();
                    outputChannel = null;
                }
                lock (outputChannelLock)
                {
                    Logger.Log("Rebuild output");
                    if (useFloat)
                    {
                        // WASAPI出力の生成を試行
                        if (BASS.isWASAPIAvailable)
                        {
                            // WASAPI排他モードの生成を試行
                            if (enableWASAPIExclusive)
                            {
                                try
                                {
                                    BASS.BASS_Free();
                                    BASS.BASS_Init(0, OutputFreq, BASS_BUFFFER_LEN);
                                    outputChannel = new BASS.WASAPIOutput(freq, chans, StreamProc, true, enableWASAPIVolume, false);
                                    if (outputChannel != null)
                                    {
                                        outputMode = Controller.OutputModeEnum.WASAPIEx;
                                        Logger.Log(outputChannel.GetFreq().ToString());
                                        Logger.Debug("Use WASAPI Exclusive Output");
                                    }
                                }
                                catch (Exception e) { Logger.Log(e.ToString()); }
                            }

                            // WASAPI共有モードの生成を試行
                            if (outputChannel == null)
                            {
                                try
                                {
                                    BASS.BASS_Free();
                                    BASS.BASS_Init(0, OutputFreq, BASS_BUFFFER_LEN);
                                    outputChannel = new BASS.WASAPIOutput(freq, chans, StreamProc, false, enableWASAPIVolume, false);
                                    if (outputChannel != null)
                                    {
                                        outputMode = Controller.OutputModeEnum.WASAPI;
                                        Logger.Log(outputChannel.GetFreq().ToString());
                                        Logger.Debug("Use WASAPI Shared Output");
                                    }
                                }
                                catch (Exception e) { Logger.Log(e.ToString()); }
                            }
                        }

                        // Floating point出力の生成を試行
                        if (outputChannel == null)
                        {
                            try
                            {
                                BASS.BASS_Free();
                                BASS.BASS_Init(-1, OutputFreq, BASS_BUFFFER_LEN);
                                outputChannel = new BASS.UserSampleStream(freq, chans, StreamProc, (BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) | BASS.Stream.StreamFlag.BASS_STREAM_AUTOFREE);
                                if (outputChannel != null) outputMode = Controller.OutputModeEnum.FloatingPoint;
                                Logger.Debug("Use Float Output");
                            }
                            catch { }
                        }
                    }
                }
                ret = true;
            }
            if (outputChannel != null)
            {
                outputChannel.SetVolume((float)_volume);
                outputChannel.SetVolume((float)_volume);
            }
            return ret;
        }

        private static void Memset(IntPtr ptr, byte set,int sizebytes, int offset = 0)
        {
            int sizedw = sizebytes >> 2;
            sizebytes = sizebytes & 3;
            for(int i=0;i<sizedw;i++){
                Marshal.WriteInt32(ptr,offset,0);
                offset += 4;
            }
            for(int i=0;i<sizebytes;i++){
                Marshal.WriteByte(ptr, offset++, 0);
            }
        }

        private static void KillOutputChannel(bool waitsync=false)
        {
            var _outputChannel = outputChannel;
            lock (outputChannelLock)
            {
                if (outputChannel != null)
                {
                    outputChannel = null;
                    if (waitsync)
                    {
                        Thread.Sleep(BASS_BUFFFER_LEN);
                    }
                    _outputChannel.Stop();
                    _outputChannel.Dispose();
                }
            }
            outputMode = Controller.OutputModeEnum.STOP;
        }
        #endregion

        #region ストリームプロシージャ
        private static uint readStreamGained(IntPtr buffer, uint length, BASS.Stream stream, double gaindB)
        {
            uint read = stream.GetData(buffer, length);
            if (read == 0xffffffff) return read;
            uint read_size = read & 0x7fffffff;
            if (EnableReplayGain)
            {
                ApplyGain(buffer, read_size, gaindB);
            }
            return read;
        }
        private unsafe static void ApplyGain(IntPtr buffer, uint length, double gaindB)
        {
            double gain_l = Math.Pow(10.0, gaindB / 20.0);
            float* dest = (float*)buffer;
            for (int i = 0, l = (int)(length / sizeof(float)); i < l; i++)
            {
                dest[i] *= (float)gain_l;
            }
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
            Memset(buffer, 0, (int)length);
            // BASSのデフォルトで0.01秒毎ぐらいにコールされるっぽい。(WASAPI時?)
            // try-catchのコストが気になるのでリリースビルドでは除去する。通常例外おきないはず。
#if DEBUG
            try
            {
#endif
            if (outputChannel != null)
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
                var stmt = h2k6db.Prepare("SELECT ROWID FROM playlist WHERE file_name = ?;");
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

            BASS.BASS_Init(0, OutputFreq, BASS_BUFFFER_LEN);

            userDirectory = new UserDirectory();
            // Load Components
            plugins.Add(new Core.CoreComponent());

            library = userDirectory.OpenLibrary(tagColumnMapping.Keys);

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
                        Logger.Log(e.ToString());
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
                    object dec = (new BinaryFormatter()).Deserialize(fs);
                    pluginSettings = (Dictionary<Guid, object>)dec;
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e.ToString());
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
                    catch { }
                }
            }

            if (BASS.isAvailable)
            {
                String[] dllList = System.IO.Directory.GetFiles(userDirectory.PluginDir, "*.dll");
                foreach (String dllFilename in dllList)
                {
                    bool success = BASS.BASS_PluginLoad(dllFilename, 0);
                    Logger.Log("Loading " + dllFilename + (success ? " OK" : " Failed"));
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

        public void SetLibraryColumns(Library.Column[] columns)
        {
        }

        /// <summary>
        /// アプリケーション全体の終了
        /// </summary>
        internal static void Quit()
        {
            try
            {
                isPlaying = false;
                KillOutputChannel();

                if (currentStream != null && currentStream.stream != null)
                {
                    currentStream.stream.Dispose();
                }

                // saveSetting
                using (var fs = new System.IO.FileStream(settingFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
                {
                    Dictionary<Guid, object> pluginSettings = new Dictionary<Guid, object>();
                    foreach (var p in plugins)
                    {
                        try
                        {
                            pluginSettings.Add(p.GetType().GUID, p.GetSetting());
                        }
                        catch { }
                    }
                    (new BinaryFormatter()).Serialize(fs, pluginSettings);
                }
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

        private static SQLite3DB.STMT GetMigemoSTMT(string sql,SQLite3DB db)
        {
            if (!Library.MigemoEnabled) throw new System.NotSupportedException("migemo is not enabled.");

            string[] words = sql.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            string[] migemo_phrase = new string[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string not = "";
                if (word[0] == '-')
                {
                    not = " NOT ";
                    word = word.Substring(1);
                }
                migemo_phrase[i] = not + " migemo( '" + word.EscapeSingleQuotSQL() + "' , tagTitle||'\n'||tagAlbum||'\n'||tagArtist||'\n'||tagComment||'\n'||tagGenre)";
            }
            return db.Prepare("CREATE TEMP TABLE playlist AS SELECT * FROM list WHERE " + String.Join(" AND ", migemo_phrase) + " ;");
        }
        private static SQLite3DB.STMT GetRegexpSTMT(string sql, SQLite3DB db)
        {
            // prepareできねぇ・・・
            Match match = new Regex(@"^\/(.+)\/[a-z]*$").Match(sql);
            if (match.Success)
            {
                return h2k6db.Prepare("CREATE TEMP TABLE playlist AS SELECT * FROM list WHERE tagTitle||'\n'||tagAlbum||'\n'||tagArtist||'\n'||tagComment regexp  '" + sql.EscapeSingleQuotSQL() + "' ;");
            }
            else
            {
                throw new System.ArgumentException();
            }
        }

        delegate SQLite3DB.STMT CreatePlaylistParser();
        private static void createPlaylistProc()
        {
            while (true)
            {
                if (h2k6db == null)
                {
                    h2k6db = Library.Connect(true);
                }
//                bool success = false;
                SQLite3DB.STMT tmt = null;
                try
                {
                    String sql;
                    bool playOnCreate = false;
                    lock (playlistQueryQueueLock)
                    {
                        sql = playlistQueryQueue;
                        playOnCreate = PlayOnCreate;
                    }
                    if (sql == null) {
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
                        try
                        {
                            h2k6db.Exec("DROP TABLE IF EXISTS playlist;");
                            Logger.Debug("playlist TABLE DROPed");
                            using (SQLite3DB.Lock dbLock = h2k6db.GetLock("list"))
                            {
                                tmt = Util.Util.TryThese<SQLite3DB.STMT>(new CreatePlaylistParser[]{
                                    ()=>h2k6db.Prepare("CREATE TEMP TABLE playlist AS " + sql + ";"),
                                    ()=>GetRegexpSTMT(sql,h2k6db),
                                    ()=>GetMigemoSTMT(sql,h2k6db),
                                    ()=>h2k6db.Prepare("CREATE TEMP TABLE playlist AS SELECT * FROM list WHERE tagTitle||tagAlbum||tagArtist||tagComment like '%" + sql.EscapeSingleQuotSQL() + "%';"),
                                },null);
                                if (tmt != null)
                                {
                                    for (int i = 0; i < 10; i++)
                                    {
                                        try
                                        {
                                            tmt.Evaluate(null);
                                            break;
                                        }
                                        catch (Exception)
                                        {
                                            Logger.Error("Evaluate failed");
                                            Thread.Sleep(200);
                                        }
                                    }

                                    //createPlaylistからinterruptが連続で発行されたとき、このsleep内で捕捉する
                                    Thread.Sleep(10);

                                    using (SQLite3DB.STMT tmt2 = h2k6db.Prepare("SELECT COUNT(*) FROM playlist ;"))
                                    {
                                        Logger.Debug("Creating new playlist " + sql);
                                        tmt2.Evaluate((o) => currentPlaylistRows = int.Parse(o[0].ToString()));
                                        Logger.Debug("Start Playlist Loader");

                                        // プレイリストの先頭一定数をキャッシュ
                                        playlistCache = new object[currentPlaylistRows][];
                                        h2k6db.FetchRowRange("playlist", 0, PlaylistPreCacheCount, playlistCache);
                                        latestPlaylistQuery = sql;
//                                        success = true; // TODO: 本来、SQLのparse失敗の時などfalseを変えす。今のところparse失敗しないので常にtrueになってる
                                    }
                                }
                            }
                        }
                        catch (SQLite3DB.SQLite3Exception e)
                        {
                            Logger.Log(e.ToString());
                        }
                        finally
                        {
                            // プレイリスト生成完了コールバックを呼ぶ
                            Logger.Debug("コールバックをすりゅ");
                            Controller._PlaylistUpdated(sql);

                            if (tmt != null) tmt.Dispose();
                            tmt = null;
                            /*
                            try
                            {
                                var t = h2k6db.Prepare("SELECT tagArtist,COUNT(*) FROM playlist GROUP BY tagArtist ORDER BY COUNT(*) desc;");
                                cache_filter = t.EvaluateAll();
                            }
                            catch (SQLite3DB.SQLite3Exception) { }
                            */
                            if(playOnCreate){
                            PlayPlaylistItem(0);
                                }

                            // プレイリストの残りをなめてキャッシュする
                            for (int i = PlaylistPreCacheCount; i < currentPlaylistRows; i++)
                            {
                                Controller.GetPlaylistRow(i);
//                                if ((i % 16) == 0) Thread.Sleep(10);
                            }
                            Logger.Debug("なめおわった");
                        }
                    }
                }
                catch (ThreadInterruptedException)
                {
                    if (tmt != null) tmt.Dispose();
                }
            }
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
                    if (preparedStream != null)
                    {
                        preparedStream.stream.Dispose();
                        preparedStream = null;
                    }
                    if (outputChannel != null)
                    {
                        currentStream.ready = false;
                        if (stopCurrent)
                        {
                            outputChannel.SetVolume(0F);
                        }
                    }
                    prepareNextStream(index);
                    if (outputChannel != null && stopCurrent)
                    {
                        outputChannel.Stop();
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
                isPlaying = false;
                // 再生するstreamが用意されているかどうかチェック
                if (preparedStream == null)
                {
                    Logger.Log("Playback Error");
                    stop();
                    Controller._OnPlaybackErrorOccured();
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
                if (OutputStreamRebuildRequired(preparedStream.stream))
                {
                    var fname = preparedStream.file_name;
                    var freq = preparedStream.stream.GetFreq();
                    var chans = preparedStream.stream.GetChans();
                    var isFloat = (preparedStream.stream.Info.flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) > 0;
                    try
                    {
                        KillOutputChannel(!stopcurrent);
                    }
                    catch (Exception e) { Logger.Log(e.ToString()); }
                    preparedStream.stream.Dispose();
                    preparedStream = null;
                    if (currentStream != null && currentStream.stream != null)
                    {
                        currentStream.stream.Dispose();
                        currentStream = null;
                    }
                    ResetOutputChannel(freq, chans, isFloat);
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
                outputChannel.SetVolume((float)_volume);
                outputChannel.Resume();
                preparedStream = null;
                _pause = false;
                isPlaying = true;
                Controller._OnTrackChange(Controller.Current.IndexInPlaylist);
            }
        }

        internal static int lastPreparedIndex;
        private static Boolean prepareNextStream(int index)
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
                string filename = (string)row[(int)DBCol.file_name];
                BASS.Stream.StreamFlag flag = BASS.Stream.StreamFlag.BASS_STREAM_DECODE;
                if (floatingPointOutput) flag |= BASS.Stream.StreamFlag.BASS_STREAM_FLOAT;
                StreamObject nextStream;
                try
                {
                    Logger.Log(String.Format("Trying to play file {0}", filename));
                    if (Path.GetExtension(filename).Trim().ToUpper() == ".CUE")
                    {
                        BASS.Stream newstream;
                        CD cd = CUEparser.fromFile(filename, false);
                        CD.Track track = cd.tracks[int.Parse(row[(int)DBCol.tagTracknumber].ToString()) - 1]; // dirty...
                        String streamFullPath = System.IO.Path.IsPathRooted(track.file_name_CUESheet)
                            ? track.file_name_CUESheet
                            : Path.GetDirectoryName(filename) + Path.DirectorySeparatorChar + track.file_name_CUESheet;

                        Logger.Log("new Stream opened " + streamFullPath);
                        newstream = new BASS.FileStream(streamFullPath, flag);
                        if (newstream == null)
                        {
                            preparedStream = null;
                            return false;
                        }
                        nextStream = new StreamObject(newstream, filename, newstream.Seconds2Bytes(track.start / 75.0) , newstream.Seconds2Bytes((track.end > track.start ? ((track.end - track.start) / 75.0) : (newstream.length - (track.start / 75.0)))));
                        if (!OutputStreamRebuildRequired(newstream)) nextStream.ready = true;
                        nextStream.cueStreamFileName = streamFullPath;
                        nextStream.cueOffset = (ulong)track.start * (newstream.GetFreq() / 75) * newstream.GetChans() * sizeof(float);
                        if (track.end > track.start)
                        {
                            nextStream.cueLength = (ulong)(track.end - track.start) * (newstream.GetFreq() / 75) * newstream.GetChans() * sizeof(float);
                        }
                        else
                        {
                            nextStream.cueLength = (ulong)newstream.filesize - nextStream.cueOffset;
                        }
                        var gain = track.getTagValue("ALBUM GAIN");
                        if (gain != null)
                        {
                            nextStream.gain = Util.Util.parseDouble(gain.ToString());
                        }
                        Logger.Debug("Setting sync on " + (track.end / 75.0));
                    }
                    else
                    {
                        BASS.Stream newstream = new BASS.FileStream(filename, flag);
                        if (newstream == null)
                        {
                            preparedStream = null;
                            return false;
                        }
                        nextStream = new StreamObject(newstream, filename);
                        nextStream.cueLength = 0;
                        var tag = Tags.MetaTag.readTagByFilename(filename.Trim(), false);
                        KeyValuePair<string, object> cue = tag.Find((match) => match.Key == "CUESHEET" ? true : false);
                        if (cue.Key != null)
                        {
                            CD cd = CUEparser.fromString(cue.Value.ToString(), filename, false);
                            CD.Track track = cd.tracks[Int32.Parse(row[(int)DBCol.tagTracknumber].ToString()) - 1];
                            nextStream.cueStreamFileName = filename.Trim();
                            nextStream.cueOffset = (ulong)track.start * (newstream.GetFreq() / 75) * newstream.GetChans() * sizeof(float);
                            if (track.end > track.start)
                            {
                                nextStream.cueLength = (ulong)(track.end - track.start) * (newstream.GetFreq() / 75) * newstream.GetChans() * sizeof(float);
                            }
                            else
                            {
                                nextStream.cueLength = (ulong)newstream.filesize - nextStream.cueOffset;
                            }
                            var gain = track.getTagValue("ALBUM GAIN");
                            if (gain != null)
                            {
                                nextStream.gain = double.Parse(gain.ToString());
                            }
                        }
                        else
                        {
                            KeyValuePair<string, object> gain = tag.Find((match) => match.Key == "REPLAYGAIN_ALBUM_GAIN" ? true : false);
                            if (gain.Value != null)
                            {
                                nextStream.gain = Util.Util.parseDouble(gain.Value.ToString());
                            }
                            KeyValuePair<string, object> iTunSMPB = tag.Find((match) => match.Key.ToUpper() == "ITUNSMPB" ? true : false);
                            if (iTunSMPB.Value != null)
                            {
                                var smpbs = iTunSMPB.Value.ToString().Trim().Split(new char[] { ' ' }).Select(_ => System.Convert.ToUInt64(_, 16)).ToArray();
                                // ref. http://nyaochi.sakura.ne.jp/archives/2006/09/15/itunes-v70070%E3%81%AE%E3%82%AE%E3%83%A3%E3%83%83%E3%83%97%E3%83%AC%E3%82%B9%E5%87%A6%E7%90%86/
                                nextStream.cueOffset = (smpbs[1]) * newstream.GetChans() * sizeof(float);
                                nextStream.cueLength = (smpbs[3]) * newstream.GetChans() * sizeof(float);
                            }
                            else
                            {
                                try
                                {
                                    using (var fs = System.IO.File.Open(filename.Trim(), System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                                    {
                                        var lametag = Lametag.Read(fs);
                                        if (lametag != null)
                                        {
                                            nextStream.cueOffset = (ulong)(lametag.delay) * newstream.GetChans() * sizeof(float);
                                            nextStream.cueLength = newstream.filesize - (ulong)(lametag.delay + lametag.padding) * newstream.GetChans() * sizeof(float);
                                            nextStream.invalidateCueLengthOnSeek = true;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        if (!OutputStreamRebuildRequired(newstream)) nextStream.ready = true;
                    }
                    nextStream.stream.position = nextStream.cueOffset;
                    nextStream.meta = row;
                    // Outputを再構築せずにそのまま使えますのとき
                    preparedStream = nextStream;
                }
                catch (Exception e)
                {
                    preparedStream = null;
                    Logger.Log(e.ToString());
                    return false;
                }
                return true;
            }
        }

        internal static void stop()
        {
            isPlaying = false;
            KillOutputChannel();
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
            int count = int.Parse(Controller.Current.MetaData(DBCol.playcount)) + 1;
            string file_name = Controller.Current.MetaData(DBCol.file_name);
            Logger.Log("再生カウントを更新しまつ" + file_name + ",  " + count);
            CoreEnqueue(() =>
            {
                // db書き込み
                CoreEnqueue(() =>
                {
                    using (var db = library.Connect(false)) {
                        using (var stmt = db.Prepare("UPDATE list SET lastplayed = current_timestamp64(), playcount = " + count + " WHERE file_name = '" + file_name.EscapeSingleQuotSQL() + "';"))
                        {
                            stmt.Evaluate(null);
                        }
                    }
                });
                var row = Controller.GetPlaylistRow(currentIndexInPlaylist);
                if (row != null)
                {
                    row[(int)DBCol.playcount] = (int.Parse(row[(int)DBCol.playcount].ToString()) + 1).ToString();
                    row[(int)DBCol.lastplayed] = (H2k6Library.currentTimestamp).ToString();
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
            if (playbackOrder == Controller.PlaybackOrder.Track)
            {
                currentStream.stream.positionSec = currentStream.cueOffset;
                return;
            }
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
            if (playbackOrder == Controller.PlaybackOrder.Track)
            {
                return;
            }
            CoreEnqueue(() => prepareNextStream(getSuccTrackIndex()));
        }

        internal static int getSuccTrackIndex() // ストリーム終端に達した場合の次のトラックを取得
        {
            int id;
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
    }
}
