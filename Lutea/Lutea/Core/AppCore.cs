using System;
using System.Collections.Generic;
using Gageas.Wrapper.BASS;
using Gageas.Lutea.Util;
using Gageas.Lutea.Library;
using KaoriYa.Migemo;
using System.Runtime.InteropServices;

namespace Gageas.Lutea.Core
{
    class AppCore
    {
        private const string SETTING_FILE_NAME = "settings.dat";
        internal const int QUEUE_STOP = -1;
        internal const int QUEUE_CLEAR = -2;

        /// <summary>
        /// 既に終了処理に入っているかどうか
        /// </summary>
        private static bool FinalizeProcess = false;

        /// <summary>
        /// 次のトラックを準備している間lockするオブジェクト
        /// </summary>
        private static readonly object PrepareMutex = new object();

        /// <summary>
        /// コアコンポーネント
        /// </summary>
        private static readonly CoreComponent MyCoreComponent = new CoreComponent();

        /// <summary>
        /// アプリケーションのコアスレッド。
        /// WASAPIの初期化・解放などを担当する
        /// </summary>
        private static readonly WorkerThread CoreWorker = new WorkerThread();

        /// <summary>
        /// コンポーネントを管理
        /// </summary>
        private static readonly ComponentManager MyComponentManager = new ComponentManager(SETTING_FILE_NAME);

        /// <summary>
        /// ユーザディレクトリ関連を管理
        /// </summary>
        internal static readonly UserDirectory MyUserDirectory = new UserDirectory();

        /// <summary>
        /// プレイリストを管理
        /// </summary>
        internal static PlaylistManager MyPlaylistManager;

        /// <summary>
        /// Migemoオブジェクト
        /// </summary>
        internal static Migemo MyMigemo = null;

        /// <summary>
        /// OutputStreamへの出力を待機するByte単位の長さの値．
        /// サウンドデバイスの初期化完了(関数戻り)から実際の出音開始までの遅延分ぐらいをこれで待つ．
        /// </summary>
        private static int StreamProcHold;

        /// <summary>
        /// 再生中かどうか
        /// </summary>
        internal static bool IsPlaying { get { return OutputManager.Available; } }

        /// <summary>
        /// ライブラリ
        /// </summary>
        internal static MusicLibrary Library { get; private set; }

        /// <summary>
        /// 直前に再生準備をしたトラックの番号
        /// </summary>
        internal static int LastPreparedIndex;

        /// <summary>
        /// 再生中のストリーム
        /// </summary>
        internal static DecodeStream CurrentStream { get; private set; }

        /// <summary>
        /// 次に再生するストリーム
        /// </summary>
        internal static DecodeStream PreparedStream { get; private set; }

        /// <summary>
        /// 出力デバイスを管理
        /// </summary>
        private static readonly OutputManager OutputManager = new OutputManager(StreamProc);

        /// <summary>
        /// 読み込まれたコンポーネントのリストを取得
        /// </summary>
        internal static LuteaComponentInterface[] Components
        {
            get
            {
                return MyComponentManager.GetComponents();
            }
        }

        /// <summary>
        /// プレイリスト中のアルバムタグの連続数情報を取得
        /// </summary>
        internal static int[] TagAlbumContinuousCount
        {
            get
            {
                return MyPlaylistManager.TagAlbumContinuousCount;
            }
        }

        /// <summary>
        /// プレイリストのキャッシュを無効化する
        /// </summary>
        /// <param name="file_name"></param>
        internal static void InvalidatePlaylistCache(string file_name)
        {
            var index = IndexInPlaylist(file_name);
            if (index == -1) return;
            MyPlaylistManager.InvalidatePlaylistRowCache(index);
        }

        /// <summary>
        /// 現在のプレイリストの行数を取得
        /// </summary>
        internal static int CurrentPlaylistRows
        {
            get
            {
                return MyPlaylistManager.CurrentPlaylistRows;
            }
        }

        /// <summary>
        /// プレイリストの行を取得
        /// </summary>
        /// <param name="index">行番号</param>
        /// <returns>プレイリストの行</returns>
        public static object[] GetPlaylistRow(int index)
        {
            return MyPlaylistManager.GetPlaylistRow(index);
        }

        /// <summary>
        /// プレイリストを生成
        /// </summary>
        /// <param name="query">クエリ文字列</param>
        /// <param name="playOnCreate">生成後に再生を開始するかどうか</param>
        public static void CreatePlaylist(string query, bool playOnCreate = false)
        {
            MyPlaylistManager.CreatePlaylist(query, playOnCreate);
            AppCore.LatestPlaylistQuery = query;
        }

        #region Properies
        /// <summary>
        /// Migemoが使用可能かどうか
        /// </summary>
        public static bool UseMigemo
        {
            get
            {
                return MyCoreComponent.UseMigemo;
            }
        }

        /// <summary>
        /// WASAPI排他を使用するかどうか
        /// </summary>
        public static bool EnableWASAPIExclusive
        {
            get
            {
                return MyCoreComponent.EnableWASAPIExclusive;
            }
        }

        /// <summary>
        /// ボリュームの取得・設定
        /// </summary>
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

        /// <summary>
        /// ポーズ状態
        /// </summary>
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
                
        /// <summary>
        /// 出力モード
        /// </summary>
        internal static Controller.OutputModeEnum OutputMode
        {
            get
            {
                return OutputManager.OutputMode;
            }
        }

        /// <summary>
        /// 出力深度
        /// </summary>
        internal static Controller.Resolutions OutputResolution
        {
            get
            {
                return OutputManager.OutputResolution;
            }
        }

        /// <summary>
        /// プレイリストのソートを設定
        /// </summary>
        /// <param name="column">ソート基準カラム</param>
        /// <param name="order">ソート順</param>
        public static void SetPlaylistSort(string column, Controller.SortOrders order)
        {
            MyCoreComponent.PlaylistSortColumn = column;
            MyCoreComponent.PlaylistSortOrder = order;
            MyPlaylistManager.CreateOrderedPlaylist(column, order);
        }

        /// <summary>
        /// プレイリストのソート基準カラム
        /// </summary>
        public static string PlaylistSortColumn
        {
            get
            {
                return MyCoreComponent.PlaylistSortColumn;
            }
        }

        /// <summary>
        /// プレイリストのソート順
        /// </summary>
        public static Controller.SortOrders PlaylistSortOrder
        {
            get
            {
                return MyCoreComponent.PlaylistSortOrder;
            }
        }

        /// <summary>
        /// 再生順
        /// </summary>
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

        /// <summary>
        /// 最後に実行したプレイリスト生成クエリの内容
        /// </summary>
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

        /// <summary>
        /// 最後に実行したプレイリスト生成クエリ(展開後)
        /// </summary>
        public static string LatestPlaylistQueryExpanded
        {
            get
            {
                return MyPlaylistManager.LatestPlaylistQueryExpanded;
            }
        }

        /// <summary>
        /// ライブラリにインポートするタイプ一覧
        /// </summary>
        public static Importer.ImportableTypes TypesToImport
        {
            get
            {
                return MyCoreComponent.ImportTypes;
            }
        }
        #endregion

        /// <summary>
        /// コアスレッドに処理をキューイングする
        /// </summary>
        /// <param name="d">処理のデリゲート</param>
        public static void CoreEnqueue(Action d)
        {
            CoreWorker.AddTask(d);
        }

        /// <summary>
        /// FFT値を取得
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="fftopt"></param>
        /// <returns></returns>
        internal static uint FFTData(float[] buffer, Wrapper.BASS.BASS.IPlayable.FFT fftopt)
        {
            if (OutputManager.Available)
            {
                return OutputManager.GetDataFFT(buffer, fftopt);
            }
            else
            {
                Array.Clear(buffer, 0, buffer.Length);
            }
            return 0;
        }

        /// <summary>
        /// 位置を設定(シーク)
        /// </summary>
        /// <param name="value">位置(秒)</param>
        internal static void SetPosition(double value)
        {
            var _current = CurrentStream;
            if (_current == null) return;
            StreamProcHold = 8192;
            _current.PositionSec = value;
            OutputManager.Start(); // これ非WASAPI時に必要
        }

        #region ストリームプロシージャ
        /// <summary>
        /// ストリームから要求データ長以内のサイズで読めるだけ読み，ゲインを適用する
        /// </summary>
        /// <param name="strm">ストリーム</param>
        /// <param name="buffer">出力バッファ</param>
        /// <param name="length">要求データ長</param>
        /// <returns>読めたデータ長</returns>
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
            return MyPlaylistManager.GetIndexInPlaylist(file_name);
        }

        #region 起動と終了
        /// <summary>
        /// アプリケーション全体の初期化
        /// </summary>
        /// <returns>メインウィンドウ</returns>
        internal static System.Windows.Forms.Form Init()
        {
            System.Windows.Forms.Form componentAsMainForm = null;
            SetDllDirectoryW("");

            // migemoのロード
            try
            {
                MyMigemo = new Migemo(@"dict\migemo-dict");
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            // ライブラリ準備
            Library = MyUserDirectory.OpenLibrary();

            // プレイリスト管理の開始
            MyPlaylistManager = new PlaylistManager(Library.Connect());

            // コンポーネントの読み込み
            MyComponentManager.Add(MyCoreComponent);
            componentAsMainForm = MyComponentManager.BuildAllInstance(System.IO.Directory.GetFiles(MyUserDirectory.ComponentDir, "*.dll"));
            MyComponentManager.LoadSettings();
            
            MyPlaylistManager.CreatePlaylist(MyCoreComponent.LatestPlaylistQuery);

            if (BASS.IsAvailable)
            {
                BASS.BASS_Init(0, buffer_len: 500);
                if (System.IO.Directory.Exists(MyUserDirectory.PluginDir))
                {
                    foreach (String dllFilename in System.IO.Directory.GetFiles(MyUserDirectory.PluginDir, "*.dll"))
                    {
                        bool success = BASSPlugin.Load(dllFilename, 0);
                        Logger.Log("Loading " + dllFilename + (success ? " OK" : " Failed"));
                    }
                }
            }

            Controller.Startup();

            return componentAsMainForm;
        }

        /// <summary>
        /// ライブラリデータベースを更新して再起動
        /// </summary>
        /// <param name="extraColumns">ライブラリデータベースに設定する拡張カラム</param>
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

        /// <summary>
        /// アプリケーション全体の終了準備
        /// </summary>
        private static void FinalizeApp()
        {
            if (FinalizeProcess) return;
            FinalizeProcess = true;
            OutputManager.KillOutputChannel();
            MyComponentManager.FinalizeComponents();
        }
        #endregion

        internal static void ActivateUI()
        {
            MyComponentManager.ActivateUIComponents();
        }

        #region メディアファイルの再生に関する処理郡
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
            lock (PrepareMutex)
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
                        return PrepareNextStream(index);
                }
            }
            
        }

        internal static Boolean PlayPlaylistItem(int index)
        {
            CoreEnqueue(() =>
            {
                lock (PrepareMutex)
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
                    PrepareNextStream(index);
                    PlayQueuedStream();
                }
            });
            return true;
        }

        /// <summary>
        /// デコードストリームに対して出力デバイスをチェックし，必要があれば初期化する
        /// </summary>
        /// <param name="stream"></param>
        private static void EnsureOutputDevice(DecodeStream stream)
        {
            var freq = stream.Freq;
            var chans = stream.Chans;
            var isFloat = stream.IsFloat;
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
                if (OutputManager.RebuildRequired(freq, chans, isFloat))
                {
                    Logger.Error("freq: " + freq);
                    Logger.Error("chans: " + chans);
                    Logger.Error("isFloat: " + isFloat);
                    throw new Exception("Can not initialize output device");
                }
                OutputManager.SetVolume(Volume, 0);
                StreamProcHold = 32768;
            }
        }

        /// <summary>
        /// キューのストリームを再生開始する
        /// </summary>
        private static void PlayQueuedStream(){
            lock (PrepareMutex)
            {
                // 再生するstreamが用意されているかどうかチェック
                if (PreparedStream == null)
                {
                    Logger.Log("Playback Error");
                    Stop();
                    Controller._OnPlaybackErrorOccured();
                    AppCore.CoreEnqueue(() => { System.Threading.Thread.Sleep(500); Controller.NextTrack(); });
                    return;
                }

                if (PreparedStream.FileName == ":STOP:")
                {
                    Stop();
                    return;
                }

                if (IndexInPlaylist(PreparedStream.FileName) == -1)
                {
                    DisposePreparedStream();
                    PrepareNextStream(GetSuccTrackIndex());
                    PlayQueuedStream();
                    return;
                }

                // Output Streamを確認・再構築
                try
                {
                    EnsureOutputDevice(PreparedStream);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    Stop();
                    Controller._OnPlaybackErrorOccured();
                    AppCore.CoreEnqueue(() => { System.Threading.Thread.Sleep(500); Controller.NextTrack(); });
                    return;
                }

                DisposeCurrentStream();
                PreparedStream.Ready = true;
                CurrentStream = PreparedStream;
                OutputManager.Resume();
                PreparedStream = null;
                Pause = false;
                Controller._OnTrackChange(Controller.Current.IndexInPlaylist);
            }
        }

        /// <summary>
        /// 次のストリームを準備する
        /// </summary>
        private static bool PrepareNextStream(int index, List<KeyValuePair<string,object>> tags = null)
        {
            lock (PrepareMutex)
            {
                LastPreparedIndex = index;
                if (PreparedStream != null) return false;
                if (index >= CurrentPlaylistRows || index < 0) return false;

                object[] row = Controller.GetPlaylistRow(index);
                string filename = (string)row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)];
                try
                {
                    int tr = 1;
                    Util.Util.tryParseInt(row[Controller.GetColumnIndexByName("tagTracknumber")].ToString(), ref tr);
                    var nextStream = DecodeStream.CreateStream(filename, tr, true, MyCoreComponent.UsePrescan, tags);
                    if (nextStream == null) return false;
                    nextStream.meta = row;

                    // prepareにsyncを設定
                    nextStream.SetSyncSec(on80Percent, nextStream.LengthSec * 0.80);
                    nextStream.SetSyncSec(onPreFinish, Math.Max(nextStream.LengthSec * 0.90, nextStream.LengthSec - 5));

                    nextStream.Ready = !OutputManager.RebuildRequired(nextStream.Freq, nextStream.Chans, nextStream.IsFloat);

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

        /// <summary>
        /// 再生停止
        /// </summary>
        internal static void Stop()
        {
            OutputManager.KillOutputChannel();
            DisposeCurrentStream();
            DisposePreparedStream();
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
                var succIndex = GetSuccTrackIndex();
                CoreEnqueue(() => PrepareNextStream(succIndex, null));
            }
            CoreEnqueue(() => PlayQueuedStream());
        }

        private static void onPreFinish(object cookie)
        {
            var succIndex = GetSuccTrackIndex();
            CoreEnqueue(() => PrepareNextStream(succIndex, null));
        }

        internal static int GetSuccTrackIndex() // ストリーム終端に達した場合の次のトラックを取得
        {
            int id;
            switch (MyCoreComponent.PlaybackOrder)
            {
                case Controller.PlaybackOrder.Track:
                    return Controller.Current.IndexInPlaylist;

                case Controller.PlaybackOrder.Random:
                    if (CurrentPlaylistRows == 1) return 0;
                    do
                    {
                        id = (new Random()).Next(CurrentPlaylistRows);
                    } while (id == Controller.Current.IndexInPlaylist);
                    return id;

                case Controller.PlaybackOrder.Default:
                    id = (Controller.Current.IndexInPlaylist) + 1;
                    if (id >= CurrentPlaylistRows)
                    {
                        return -1;
                    }
                    return id;

                case Controller.PlaybackOrder.Endless:
                    id = (Controller.Current.IndexInPlaylist) + 1;
                    if (id >= CurrentPlaylistRows)
                    {
                        id = 0;
                    }
                    return id;
            }
            return 0;
        }
        #endregion

        #region DatabaseUpdated
        /// <summary>
        /// 外部からデータベースに書き込んだ後に呼ぶ
        /// </summary>
        /// <param name="silent"></param>
        internal static void DatabaseUpdated(bool silent = false)
        {
            string latest = MyCoreComponent.LatestPlaylistQuery;
            if (!silent)
            {
                MyPlaylistManager.RefreshPlaylist();
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
