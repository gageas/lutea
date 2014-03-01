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

    /// <summary>
    /// Streamを保持。内部的に使用する
    /// </summary>
    class StreamObject : IDisposable
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

        public void Dispose()
        {
            if (stream != null)
            {
                stream.Dispose();
            }
        }
    }

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
        internal static List<Lutea.Core.LuteaComponentInterface> Plugins = new List<Core.LuteaComponentInterface>();
        internal static List<Assembly> Assemblys = new List<Assembly>();
        internal static UserDirectory userDirectory;
        internal static PlaylistManager playlistBuilder;

        internal static Migemo Migemo = null;
        internal static StreamObject CurrentStream; // 再生中のストリーム
        internal static StreamObject PreparedStream;

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
        private static bool UseFloatingPointOutput = false; 
        internal static bool IsPlaying;

        internal static MusicLibrary Library { get; private set; }

        private static BASS.Channel.SyncProc d_onFinish = new BASS.Channel.SyncProc(onFinish);
        private static BASS.Channel.SyncProc d_onPreFinish = new BASS.Channel.SyncProc(onPreFinish);
        private static BASS.Channel.SyncProc d_on80Percent = new BASS.Channel.SyncProc(on80Percent);

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
            StreamProcHold = 8192;
            if (_current.invalidateCueLengthOnSeek)
            {
                _current.cueLength = 0;
            }
            _current.stream.position = _current.stream.Seconds2Bytes(value) + _current.cueOffset;
            OutputManager.Start(); // これ非WASAPI時に必要

        }

        #region ストリームプロシージャ
        private static uint ReadAsPossibleWithGain(StreamObject strm, IntPtr buffer, uint length)
        {
            if (strm == null || strm.stream == null || !strm.ready) return 0;
            if ((strm.cueLength > 0) && length + strm.stream.position > strm.cueLength + strm.cueOffset)
            {
                length = (uint)(strm.cueLength + strm.cueOffset - strm.stream.position);
            }
            uint read = strm.stream.GetData(buffer, length);
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
            onFinish(BASS.SYNC_TYPE.END, _current);

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
            catch (Exception ex)
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

            if (CurrentStream != null)
            {
                CurrentStream.Dispose();
            }

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
        internal static Boolean QueuePlaylistItem(int index)
        {
            lock (prepareMutex)
            {
                if (PreparedStream != null)
                {
                    PreparedStream.Dispose();
                    PreparedStream = null;
                }
                if (index == QUEUE_CLEAR)
                {
                    return true;
                }
                else if (index == QUEUE_STOP)
                {
                    PreparedStream = new StreamObject(null, ":STOP:");
                    return true;
                }
            }
            return prepareNextStream(index);
        }
        internal static Boolean PlayPlaylistItem(int index)
        {
            Logger.Debug("Enter PlayPlaylistItem");
            CoreEnqueue((Controller.VOIDVOID)(() =>
            {
                lock (prepareMutex)
                {
                    if (PreparedStream != null)
                    {
                        PreparedStream.Dispose();
                        PreparedStream = null;
                    }
                    if (OutputManager.CanAbort)
                    {
                        OutputManager.Stop();
                    }
                    if (CurrentStream != null)
                    {
                        CurrentStream.ready = false;
                    }
                    prepareNextStream(index);
                    PlayQueuedStream();
                }
            }));
            Logger.Debug("Leave PlayPlaylistItem");
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

                if (PreparedStream.file_name == ":STOP:")
                {
                    stop();
                    return;
                }

                if (IndexInPlaylist(PreparedStream.file_name) == -1)
                {
                    PreparedStream = null;
                    prepareNextStream(getSuccTrackIndex());
                    PlayQueuedStream();
                    return;
                }

                IsPlaying = false;

                // Output Streamを再構築
                if (OutputManager.RebuildRequired(PreparedStream.stream) || Pause)
                {
                    var fname = PreparedStream.file_name;
                    var freq = PreparedStream.stream.GetFreq();
                    var chans = PreparedStream.stream.GetChans();
                    var isFloat = (PreparedStream.stream.Info.Flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) > 0;
                    try
                    {
                        OutputManager.KillOutputChannel();
                    }
                    catch (Exception e) { Logger.Error(e); }
                    PreparedStream.Dispose();
                    PreparedStream = null;
                    if (CurrentStream != null)
                    {
                        CurrentStream.Dispose();
                        CurrentStream = null;
                    }
                    Pause = false;
                    OutputManager.ResetOutputChannel(freq, chans, isFloat, (uint)MyCoreComponent.BufferLength, MyCoreComponent.PreferredDeviceName);
                    OutputManager.SetVolume(Volume, 0);
                    prepareNextStream(IndexInPlaylist(fname));
                    StreamProcHold = 32768;
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

                if (CurrentStream != null)
                {
                    CurrentStream.Dispose();
                }
                CurrentStream = null;
                PreparedStream.ready = true;
                PreparedStream.playbackCounterUpdated = false;
                CurrentStream = PreparedStream;
                OutputManager.Resume();
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
                BASS.Stream.StreamFlag flag = BASS.Stream.StreamFlag.BASS_STREAM_DECODE | BASS.Stream.StreamFlag.BASS_STREAM_ASYNCFILE;
                if (UseFloatingPointOutput) flag |= BASS.Stream.StreamFlag.BASS_STREAM_FLOAT;
                if (MyCoreComponent.UsePrescan) flag |= BASS.Stream.StreamFlag.BASS_STREAM_PRESCAN;
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
            CurrentStream.Dispose();
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
                Controller.NextTrack();
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
