using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.IO;
using Gageas.Wrapper.BASS;
using Gageas.Lutea.Util;
using Gageas.Lutea.Library;
using KaoriYa.Migemo;
using System.Runtime.InteropServices;

namespace Gageas.Lutea.Core
{
    class AppCore
    {
        private const string settingFileName = "settings.dat";
        internal const int QUEUE_STOP = -1;
        internal const int QUEUE_CLEAR = -2;

        private static CoreComponent MyCoreComponent = new CoreComponent();

        /// <summary>
        /// アプリケーションのコアスレッド。
        /// WASAPIの初期化・解放などを担当する
        /// </summary>
        private static WorkerThread CoreWorker = new WorkerThread();
        private static OutputManager OutputManager = new OutputManager(StreamProc);
        internal static List<LuteaComponentInterface> Plugins = new List<LuteaComponentInterface>();
        internal static List<Assembly> Assemblys = new List<Assembly>();
        internal static UserDirectory userDirectory;
        internal static PlaylistManager playlistBuilder;

        internal static Migemo Migemo = null;

        /// <summary>
        /// 再生中のストリーム
        /// </summary>
        internal static DecodeStream CurrentStream
        {
            get;
            private set;
        }

        /// <summary>
        /// 次に再生するストリーム
        /// </summary>
        internal static DecodeStream PreparedStream
        {
            get;
            private set;
        }

        internal static int[] TagAlbumContinuousCount
        {
            get
            {
                return playlistBuilder.TagAlbumContinuousCount;
            }
        }

        internal static void InvalidatePlaylistCache(string file_name)
        {
            var index = IndexInPlaylist(file_name);
            if (index == -1) return;
            playlistBuilder.InvalidatePlaylistRowCache(index);
        }

        internal static int currentPlaylistRows
        {
            get
            {
                return playlistBuilder.CurrentPlaylistRows;
            }
        }

        public static object[] GetPlaylistRow(int index)
        {
            return playlistBuilder.GetPlaylistRow(index);
        }

        public static void createPlaylist(string sql, bool playOnCreate = false)
        {
            playlistBuilder.CreatePlaylist(sql, playOnCreate);
            AppCore.LatestPlaylistQuery = sql;
        }

        /// <summary>
        /// Byte単位の長さの値．この値が0になるまでOutputStreamへの出力を待機する．
        /// サウンドデバイスの初期化完了(関数戻り)から実際の出音開始までの遅延分ぐらいをこれで待つ．
        /// </summary>
        private static int StreamProcHold;
        internal static bool IsPlaying;

        internal static MusicLibrary Library { get; private set; }
        internal static int lastPreparedIndex;

        #region Properies
        public static bool UseMigemo
        {
            get
            {
                return MyCoreComponent.UseMigemo;
            }
        }
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

        public static void SetPlaylistSort(string column, Controller.SortOrders order)
        {
            MyCoreComponent.PlaylistSortColumn = column;
            MyCoreComponent.PlaylistSortOrder = order;
            playlistBuilder.CreateOrderedPlaylist(column, order);
        }

        public static Controller.SortOrders PlaylistSortOrder
        {
            get
            {
                return MyCoreComponent.PlaylistSortOrder;
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
        }

        public static string LatestPlaylistQuery
        {
            get
            {
                return MyCoreComponent.LatestPlaylistQuery;
            }
            private set
            {
                MyCoreComponent.LatestPlaylistQuery = value;
            }
        }

        public static string LatestPlaylistQueryExpanded
        {
            get
            {
                return playlistBuilder.LatestPlaylistQueryExpanded;
            }
        }

        public static Importer.ImportableTypes TypesToImport
        {
            get
            {
                return MyCoreComponent.ImportTypes;
            }
        }
        #endregion

        public static void CoreEnqueue(Action d)
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
            StreamProcHold = 8192;
            _current.PositionSec = value;
            OutputManager.Start(); // これ非WASAPI時に必要
        }

        #region ストリームプロシージャ
        private static uint ReadAsPossibleWithGain(DecodeStream strm, IntPtr buffer, uint length)
        {
            if (strm == null) return 0;
            uint read = strm.GetData(buffer, length);
            if (read == 0xFFFFFFFF) return 0;
            read &= 0x7FFFFFFF;
            double gaindB = 0;
            if (MyCoreComponent.EnableReplayGain)
            {
                gaindB = strm.gain == null ? MyCoreComponent.NoReplaygainGainBoost : (MyCoreComponent.ReplaygainGainBoost + strm.gain ?? 0);
            }
            LuteaHelper.ApplyGain(buffer, read, gaindB, OutputMode == Controller.OutputModeEnum.WASAPI || OutputMode == Controller.OutputModeEnum.WASAPIEx ? Volume : 1.0);
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
            var _prepare = PreparedStream;

            if (StreamProcHold > 0)
            {
                StreamProcHold = Math.Max(0, (int)(StreamProcHold - length));
                ZeroMemory(buffer, length);
                return length; // StreamProcHoldの値に関係なくlengthを返す．StreamProcHoldがキリの悪い値になっていてもこれなら大丈夫
            }
            if (!OutputManager.Available)
            {
                ZeroMemory(buffer, length);
                return length;
            }

            // currentStreamから読み出し
            var read1 = ReadAsPossibleWithGain(_current, buffer, length);
            if (read1 == length) return length;

            // バッファの最後まで読み込めなかった時、prepareStreamからの読み込みを試す
            var read2 = ReadAsPossibleWithGain(_prepare, IntPtr.Add(buffer, (int)read1), length - read1);
            onFinish(_current);

            var readTotal = read1 + read2;
            if (readTotal != length)
            {
                ZeroMemory(IntPtr.Add(buffer, (int)readTotal), length - readTotal);
            }
            return length;
        }
        #endregion

        internal static int IndexInPlaylist(string file_name)
        {
            return playlistBuilder.GetIndexInPlaylist(file_name);
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
            catch (Exception e)
            {
                Logger.Error(e);
            }

            // userDirectoryオブジェクト取得
            userDirectory = new UserDirectory();

            // ライブラリ準備
            Library = userDirectory.OpenLibrary();

            // プレイリスト管理の開始
            playlistBuilder = new PlaylistManager(Library.Connect());

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
                    var pluginSettingsTmp = (Dictionary<Guid, byte[]>)(new BinaryFormatter()).Deserialize(fs);
                    foreach (var e in pluginSettingsTmp)
                    {
                        try
                        {
                            pluginSettings.Add(e.Key, (new BinaryFormatter()).Deserialize(new MemoryStream(e.Value)));
                        }
                        catch { }
                    }
                    AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve); 
                }
            }
            catch (Exception)
            {
                try
                {
                    using (var fs = new System.IO.FileStream(settingFileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                        pluginSettings = (Dictionary<Guid, object>)(new BinaryFormatter()).Deserialize(fs);
                        AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                    }
                }
                catch (Exception ex2) { Logger.Log(ex2); }
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
            
            playlistBuilder.CreatePlaylist(MyCoreComponent.LatestPlaylistQuery);

            if (BASS.IsAvailable)
            {
                BASS.BASS_Init(0, buffer_len: 500);
                if (System.IO.Directory.Exists(userDirectory.PluginDir))
                {
                    foreach (String dllFilename in System.IO.Directory.GetFiles(userDirectory.PluginDir, "*.dll"))
                    {
                        bool success = BASSPlugin.Load(dllFilename, 0);
                        Logger.Log("Loading " + dllFilename + (success ? " OK" : " Failed"));
                    }
                }
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

            // Quit plugins and save setting
            using (var fs = new System.IO.FileStream(settingFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
            {
                Dictionary<Guid, byte[]> pluginSettings = new Dictionary<Guid, byte[]>();
                foreach (var p in Plugins)
                {
                    try
                    {
                        var ms = new MemoryStream();
                        (new BinaryFormatter()).Serialize(ms, p.GetSetting());
                        pluginSettings.Add(p.GetType().GUID, ms.ToArray());
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

        #region メディアファイルの再生に関する処理郡
        private static object prepareMutex = new object();
        private static void DisposeCurrentStream()
        {
            if (CurrentStream == null) return;
            CurrentStream.Dispose();
            CurrentStream = null;
        }

        private static void DisposePreparedStream()
        {
            if (PreparedStream == null) return;
            PreparedStream.Dispose();
            PreparedStream = null;
        }

        internal static Boolean QueuePlaylistItem(int index)
        {
            lock (prepareMutex)
            {
                DisposePreparedStream();
                switch (index)
                {
                    case QUEUE_CLEAR:
                        return true;
                    case QUEUE_STOP:
                        PreparedStream = DecodeStream.CreateStopRequestStream();
                        return true;
                    default:
                        return prepareNextStream(index);
                }
            }
            
        }

        internal static Boolean PlayPlaylistItem(int index)
        {
            CoreEnqueue(() =>
            {
                lock (prepareMutex)
                {
                    DisposePreparedStream();
                    if (OutputManager.CanAbort)
                    {
                        OutputManager.Stop();
                    }
                    if (CurrentStream != null)
                    {
                        CurrentStream.Ready = false;
                    }
                    prepareNextStream(index);
                    PlayQueuedStream();
                }
            });
            return true;
        }

        private static void PlayQueuedStream(){
            lock (prepareMutex)
            {
                // 再生するstreamが用意されているかどうかチェック
                if (PreparedStream == null)
                {
                    Logger.Log("Playback Error");
                    stop();
                    Controller._OnPlaybackErrorOccured();
                    AppCore.CoreEnqueue(() => { System.Threading.Thread.Sleep(500); Controller.NextTrack(); });
                    return;
                }

                if (PreparedStream.FileName == ":STOP:")
                {
                    stop();
                    return;
                }

                if (IndexInPlaylist(PreparedStream.FileName) == -1)
                {
                    DisposePreparedStream();
                    prepareNextStream(getSuccTrackIndex());
                    PlayQueuedStream();
                    return;
                }

                IsPlaying = false;

                // Output Streamを再構築
                var freq = PreparedStream.Freq;
                var chans = PreparedStream.Chans;
                var isFloat = PreparedStream.IsFloat;
                if (OutputManager.RebuildRequired(freq, chans, isFloat) || Pause)
                {
                    try
                    {
                        OutputManager.KillOutputChannel();
                    }
                    catch (Exception e) { Logger.Error(e); }
                    DisposeCurrentStream();
                    Pause = false;
                    OutputManager.ResetOutputChannel(freq, chans, isFloat, (uint)MyCoreComponent.BufferLength, MyCoreComponent.PreferredDeviceName);
                    OutputManager.SetVolume(Volume, 0);
                    StreamProcHold = 32768;
                    PlayQueuedStream();
                    return;
                }

                // prepareにsyncを設定
                PreparedStream.SetSyncSec(on80Percent, PreparedStream.LengthSec * 0.80);
                PreparedStream.SetSyncSec(onPreFinish, Math.Max(PreparedStream.LengthSec * 0.90, PreparedStream.LengthSec - 5));

                DisposeCurrentStream();
                PreparedStream.Ready = true;
                CurrentStream = PreparedStream;
                OutputManager.Resume();
                PreparedStream = null;
                Pause = false;
                IsPlaying = true;
                Controller._OnTrackChange(Controller.Current.IndexInPlaylist);
            }
        }

        private static bool prepareNextStream(int index, List<KeyValuePair<string,object>> tags = null)
        {
            lock (prepareMutex)
            {
                lastPreparedIndex = index;
                if (PreparedStream != null) return false;
                if (index >= currentPlaylistRows || index < 0) return false;

                object[] row = Controller.GetPlaylistRow(index);
                string filename = (string)row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)];
                try
                {
                    int tr = 1;
                    Util.Util.tryParseInt(row[Controller.GetColumnIndexByName("tagTracknumber")].ToString(), ref tr);
                    var nextStream = DecodeStream.CreateStream(filename, tr, true, MyCoreComponent.UsePrescan, tags);
                    if (nextStream == null) return false;
                    nextStream.meta = row;
                    PreparedStream = nextStream;
                }
                catch (Exception e)
                {
                    Logger.Log(e.ToString());
                    return false;
                }
            }
            return true;
        }

        internal static void stop()
        {
            IsPlaying = false;
            OutputManager.KillOutputChannel();
            DisposeCurrentStream();
            Controller._OnTrackChange(-1);
        }
        #endregion

        #region トラック終端でのイベント
        private static void UpdatePlaybackCount(DecodeStream strm)
        {
            if (strm.PlaybackCounterUpdated) return;
            strm.PlaybackCounterUpdated = true;
            var currentIndexInPlaylist = Controller.Current.IndexInPlaylist;
            int count = int.Parse(Controller.Current.MetaData(Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.playcount))) + 1;
            string file_name = Controller.Current.MetaData(Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name));
            CoreEnqueue(() =>
            {
                Logger.Log("再生カウントを更新しまつ" + file_name + ",  " + count);
                // db書き込み
                using (var db = Library.Connect())
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

        private static void on80Percent(object cookie)
        {
            UpdatePlaybackCount(CurrentStream);
        }

        private static void onFinish(DecodeStream _current)
        {
            if (_current != CurrentStream) return;
            if (!CurrentStream.Ready) return;
            CurrentStream.Ready = false;

            UpdatePlaybackCount(CurrentStream);
            if (PreparedStream == null)
            {
                var succIndex = getSuccTrackIndex();
                CoreEnqueue(() => prepareNextStream(succIndex, null));
            }
            CoreEnqueue(() => PlayQueuedStream());
        }

        private static void onPreFinish(object cookie)
        {
            var succIndex = getSuccTrackIndex();
            CoreEnqueue(() => prepareNextStream(succIndex, null));
        }

        internal static int getSuccTrackIndex() // ストリーム終端に達した場合の次のトラックを取得
        {
            int id;
            switch (MyCoreComponent.PlaybackOrder)
            {
                case Controller.PlaybackOrder.Track:
                    return Controller.Current.IndexInPlaylist;

                case Controller.PlaybackOrder.Random:
                    if (currentPlaylistRows == 1) return 0;
                    do
                    {
                        id = (new Random()).Next(currentPlaylistRows);
                    } while (id == Controller.Current.IndexInPlaylist);
                    return id;

                case Controller.PlaybackOrder.Default:
                    id = (Controller.Current.IndexInPlaylist) + 1;
                    if (id >= currentPlaylistRows)
                    {
                        return -1;
                    }
                    return id;

                case Controller.PlaybackOrder.Endless:
                    id = (Controller.Current.IndexInPlaylist) + 1;
                    if (id >= currentPlaylistRows)
                    {
                        id = 0;
                    }
                    return id;
            }
            return 0;
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
                playlistBuilder.RefreshPlaylist();
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
