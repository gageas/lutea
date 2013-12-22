using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;

using Gageas.Wrapper.BASS;
using Gageas.Wrapper.SQLite3;
using Gageas.Lutea.Util;
using Gageas.Lutea.Library;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// アプリケーションのコントローラ
    /// AppCoreから公開インタフェースを分離したが、中途半端
    /// </summary>
    public static class Controller
    {
        #region Enumeration definition
        /// <summary>
        /// プレイリストのソート順を設定するために使う列挙体。
        /// KeepOrFlipは、
        /// ・同じColumnを指定した場合ソート順を反転
        /// ・違うColumnを指定した場合ソート順は維持
        /// </summary>
        public enum SortOrders
        {
            Asc,Desc,KeepOrFlip
        }

        /// <summary>
        /// 出力モード列挙体
        /// </summary>
        public enum OutputModeEnum
        {
            STOP,
            Integer16,
            FloatingPoint,
            ASIO,
            WASAPI,
            WASAPIEx,
        }

        /// <summary>
        /// 出力ビット深度列挙体
        /// </summary>
        public enum Resolutions
        {
            Unknown,
            Float_32Bit,
            Integer_8bit,
            Integer_16bit,
            Integer_24bit,
            Integer_32bit,
        }


        /// <summary>
        /// 再生モード列挙体
        /// </summary>
        public enum PlaybackOrder { Default, Track, Random };
        #endregion

        #region Event definition
        // Event definition

        // general purpose delegates
        public delegate void VOIDVOID();
        public delegate void VOIDINT(int sec);
        public static event VOIDINT onTrackChange;
        public static event VOIDINT onElapsedTimeChange;
        public static event VOIDVOID onPlaybackErrorOccured; //TODO: 失敗理由を通知できるようにする
        public static event VOIDVOID onVolumeChange;
        public static event VOIDVOID onPlaybackOrderChange;
        public static event VOIDVOID onDatabaseUpdated;
        public delegate void PlaylistUpdatedEvent(string sql);
        public static event PlaylistUpdatedEvent PlaylistUpdated;
        public delegate void PlaylistSortOrderChangeEvent(string columnText, Controller.SortOrders sortOrder);
        public static event PlaylistSortOrderChangeEvent PlaylistSortOrderChanged;
        #endregion

        #region elapsedTimeWatcher
        private static Thread elapsedTimeWatcherThread;
        private static int elapsedtime = 0;
        public static void elapsedTimeWatcher()
        {
            while (true)
            {
                Thread.Sleep(100);
                if (!AppCore.IsPlaying) continue;
                if (onElapsedTimeChange != null)
                {
                    int t = (int)Controller.Current.Position;
                    if (t == -1) continue; // 再生開始前
                    if (t != elapsedtime)
                    {
                        elapsedtime = t;
                        foreach (var dlg in onElapsedTimeChange.GetInvocationList())
                        {
                            var _dlg = (VOIDINT)dlg;
                            _dlg.BeginInvoke(t, (_ => { _dlg.EndInvoke(_); }), null);
                        }
                    }
                    if (t > 0) icache = -1;
                }
            }
        }
        #endregion

        #region Output Channel
        /// <summary>
        /// 出力チャンネルのFFTデータを取得する．
        /// </summary>
        /// <param name="buffer">出力先</param>
        /// <param name="fftopt">FFTオプション．ポイント数他</param>
        /// <returns></returns>
        public static uint FFTData(float[] buffer, Wrapper.BASS.BASS.IPlayable.FFT fftopt){
            return AppCore.FFTData(buffer, fftopt);
        }

        public static OutputModeEnum OutputMode
        {
            get
            {
                return AppCore.OutputMode;
            }
        }
        public static Resolutions OutputResolution
        {
            get
            {
                return AppCore.OutputResolution;
            }
        }
        #endregion

        #region Music Player Control
        /// <summary>
        /// prev,next連打時用。直前のprev, nextで選択したidを保持。再生開始後破棄。
        /// </summary>
        private static int icache;

        public static Boolean IsPlaying
        {
            get
            {
                return AppCore.IsPlaying;
            }
        }

        public static void Quit()
        {
            try
            {
                if (elapsedTimeWatcherThread.IsAlive)
                {
                    elapsedTimeWatcherThread.Abort();
                    elapsedTimeWatcherThread.Join();
                }
            }
            finally
            {
                AppCore.Quit();
            }
        }

        public static bool Pause
        {
            get
            {
                return AppCore.Pause;
            }
            set
            {
                AppCore.Pause = value;
            }
        }

        public static Boolean TogglePause()
        {
            Pause = !Pause;
            return Pause;
        }

        public static float Volume
        {
            get
            {
                return AppCore.Volume;
            }
            set
            {
                AppCore.Volume = value;
                if (onVolumeChange != null) onVolumeChange.Invoke();
            }
        }

        public static PlaybackOrder playbackOrder
        {
            get
            {
                return AppCore.PlaybackOrder;
            }
            set
            {
                AppCore.PlaybackOrder = value;
                if (onPlaybackOrderChange != null) onPlaybackOrderChange.Invoke();
            }
        }

        public static void Play()
        {
            NextTrack();
        }

        /// <summary>
        /// プレイリストの指定したindex番目のトラックを再生する
        /// </summary>
        /// <param name="index"></param>
        public static void PlayPlaylistItem(int index)
        {
            AppCore.CoreEnqueue((VOIDVOID)(() =>
            {
                icache = index;
                AppCore.PlayPlaylistItem(index);
            }));
        }

        public static void Stop()
        {
            AppCore.CoreEnqueue((VOIDVOID)(() =>
            {
                AppCore.stop();
            }));
        }

        public static void NextTrack(bool stopCurrent = true)
        {
            Logger.Log("next Track");
            AppCore.CoreEnqueue((VOIDVOID)(() => {
                int i = (icache > 0 ? icache : icache = Current.IndexInPlaylist);
                int id;
                if (playbackOrder == Controller.PlaybackOrder.Random)
                {
                    if (PlaylistRowCount == 1) id = 0;
                    else
                    {
                        do
                        {
                            id = (new Random()).Next(PlaylistRowCount);
                        } while (id == i);
                    }
                }
                else
                {
                    id = (i) + 1;
                    if (id >= PlaylistRowCount)
                    {
                        id = 0;
                    }
                }
                icache = id;
                AppCore.PlayPlaylistItem(id, stopCurrent);
            }));
        }

        public static void PrevTrack()
        {
            Logger.Log("prev Track");
            AppCore.CoreEnqueue((VOIDVOID)(() =>
            {
                int i = (icache > 0?icache:Current.IndexInPlaylist);
                int id;
                if (i == -1)
                {
                    id = 0;
                }
                else
                {
                    if (Current.Position > 5) // 現在位置が5秒以内なら現在のトラックの頭に
                    {
                        id = i;
                    }
                    else
                    {
                        id = i - 1;
                    }
                }
                if (id < 0)
                {
                    id = PlaylistRowCount - 1;
                }
                icache = id;
                AppCore.PlayPlaylistItem(id);
            }));
        }
        #endregion

        #region Current track's Information
        public static class Current
        {
            public static double Length
            {
                get
                {
                    return (AppCore.CurrentStream != null) ? (AppCore.CurrentStream.cueLength > 0 ? AppCore.CurrentStream.stream.Bytes2Seconds(AppCore.CurrentStream.cueLength) : AppCore.CurrentStream.stream.length) : 0;
                }
            }
            public static double Position
            {
                get
                {
                    return (AppCore.CurrentStream != null) ? AppCore.CurrentStream.stream.positionSec - AppCore.CurrentStream.stream.Bytes2Seconds(AppCore.CurrentStream.cueOffset) : 0;
                }
                set
                {
                    AppCore.SetPosition(value);
                }
            }
            public static String MetaData(Library.Column col)
            {
                if (AppCore.CurrentStream == null) return null;
                int idx = AppCore.Library.Columns.ToList().IndexOf(col);
                if (idx < 0 || idx >= AppCore.CurrentStream.meta.Length) return null;
                return AppCore.CurrentStream.meta[idx].ToString();
            }
            public static String MetaData(int colidx)
            {
                if (AppCore.CurrentStream == null) return null;
                if (colidx < 0 || colidx >= AppCore.CurrentStream.meta.Length) return null;
                return AppCore.CurrentStream.meta[colidx].ToString();
            }
            public static String MetaData(string DBText)
            {
                if (AppCore.CurrentStream == null) return null;
                int idx = AppCore.Library.Columns.ToList().IndexOf(AppCore.Library.Columns.First(_ => _.Name == DBText));
                if (idx < 0 || idx >= AppCore.CurrentStream.meta.Length) return null;
                return AppCore.CurrentStream.meta[idx].ToString();
            }

            public static int Rating
            {
                get
                {
                    return int.Parse(MetaData(AppCore.Library.Columns.First(_ => _.Type == LibraryColumnType.Rating)));
                }
                set
                {
                    AppCore.CoreEnqueue(() =>
                    {
                        using (var db = GetDBConnection())
                        {
                            using (var stmt = db.Prepare("UPDATE list SET " + LibraryDBColumnTextMinimum.rating + " = " + value + " WHERE " + LibraryDBColumnTextMinimum.file_name + " = '" + Filename.EscapeSingleQuotSQL() + "'"))
                            {
                                stmt.Evaluate(null);
                            }
                        
                        }
                    });
                }
            }
            public static String Filename
            {
                get
                {
                    return MetaData(Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name));
                }
            }
            public static String StreamFilename
            {
                get
                {
                    if (AppCore.CurrentStream == null) return null;
                    return AppCore.CurrentStream.cueStreamFileName != null ? AppCore.CurrentStream.cueStreamFileName : Filename;
                }
            }
            public static int IndexInPlaylist
            {
                get
                {
                    if (AppCore.CurrentStream == null) return -1;
                    return IndexInPlaylist(AppCore.CurrentStream.file_name);
                }
            }
            
            /// <summary>
            /// 現在のカバーアートをImageオブジェクトとして返す。
            /// カバーアートが無ければdefault.jpgのImageオブジェクトを返す。
            /// default.jpgも見つからなければnullを返す。
            /// FIXME?: この機能はCoreに移すかも
            /// </summary>
            /// <returns></returns>
            internal static bool CacheIsOutdated = false;
            private static System.Drawing.Image coverArtImage = null;
            private static readonly object GetCoverArtImageLock = new object();
            public static System.Drawing.Image CoverArtImage()
            {
                lock (GetCoverArtImageLock)
                {
                    if (CacheIsOutdated)
                    {
                        coverArtImage = CoverArtImageForFile(StreamFilename);
                        CacheIsOutdated = false;
                    }
                    if (coverArtImage == null) return null;
                    lock (coverArtImage)
                    {
                        return new System.Drawing.Bitmap(coverArtImage);
                    }
                }
            }

            public static string[] GetLyrics()
            {
                var _filename = Filename;
                if (_filename == null) return null;
                _filename = _filename.Trim();
                // Internal cueから検索
                try
                {
                    var cue = InternalCUEReader.Read(_filename, false);
                    if (cue != null)
                    {
                        int track = 1;
                        Util.Util.tryParseInt(MetaData("tagTracknumber"), ref track);
                        if (cue.tracks.Count >= track)
                        {
                            track--;
                            if (cue.tracks[track].tag.Exists(_ => _.Key == "LYRICS"))
                            {
                                return cue.tracks[track].tag.First(_ => _.Key == "LYRICS").Value.ToString().Replace("\r", "").Split('\n');
                            }
                        }
                    }
                }
                catch { }

                // タグから検索
                try
                {
                    var tag = Tags.MetaTag.readTagByFilename(_filename, false);
                    if (tag != null && tag.Exists(_ => _.Key == "LYRICS"))
                    {
                        return tag.First(_ => _.Key == "LYRICS").Value.ToString().Replace("\r\n", "\n").Replace("\r","\n").Split('\n');
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                // lrcファイルから検索
                try
                {
                    var asLrc = System.IO.Path.ChangeExtension(_filename, "lrc");
                    if (System.IO.File.Exists(asLrc))
                    {
                        return ReadAllLinesAutoEncoding(asLrc);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                var invalidChars = System.IO.Path.GetInvalidPathChars();
                // txtファイルから検索
                try
                {
                    var asTxt = System.IO.Path.ChangeExtension(_filename, "txt");
                    if (System.IO.File.Exists(asTxt))
                    {
                        return ReadAllLinesAutoEncoding(asTxt);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                // タイトル.lrcファイルから検索
                try
                {
                    var asTitleTxtCandidates = System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(_filename), new string(Current.MetaData(Controller.GetColumnIndexByName("tagTitle")).Select(_ => invalidChars.Contains(_) ? '?' : _).ToArray()) + ".lrc", System.IO.SearchOption.TopDirectoryOnly);
                    if (asTitleTxtCandidates.Length > 0)
                    {
                        return ReadAllLinesAutoEncoding(asTitleTxtCandidates[0]);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                // タイトル.txtファイルから検索
                try
                {
                    var asTitleTxtCandidates = System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(_filename), new string(Current.MetaData(Controller.GetColumnIndexByName("tagTitle")).Select(_ => invalidChars.Contains(_) ? '?' : _).ToArray()) + ".txt", System.IO.SearchOption.TopDirectoryOnly);
                    if (asTitleTxtCandidates.Length > 0)
                    {
                        return ReadAllLinesAutoEncoding(asTitleTxtCandidates[0]);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                return null;
            }
        }

        private static string[] ReadAllLinesAutoEncoding(string filename){
            var asDef = System.IO.File.ReadAllLines(filename, Encoding.Default);
            var asUtf8 = System.IO.File.ReadAllLines(filename, Encoding.UTF8);
            return (asDef.Sum(_ => _.Length) < asUtf8.Sum(_ => _.Length) ? asDef : asUtf8);
        }

        public static System.Drawing.Image CoverArtImageForFile(string filename)
        {
            System.Drawing.Image image = null;
            if (filename != null)
            {
                List<KeyValuePair<string, object>> tag = Tags.MetaTag.readTagByFilename(filename, true);
                if (tag != null)
                {
                    image = (System.Drawing.Image)tag.Find((match) => match.Value is System.Drawing.Image).Value;
                }
                if (image == null)
                {
                    image = GetExternalCoverArt(filename);
                }
            }
            return image;
        }

        /// <summary>
        /// メディアファイルと同じフォルダにあるカバーアートっぽい画像を読み込む
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static System.Drawing.Image GetExternalCoverArt(string path)
        {
            String name = System.IO.Path.GetDirectoryName(path);
            String[] searchPatterns = { "folder.jpg", "*.jpg", "*.jpeg", "*.png"};
            foreach (String searchPattern in searchPatterns)
            {
                try
                {
                    String[] filename_candidate = System.IO.Directory.GetFiles(name, searchPattern);
                    foreach (var file_name in filename_candidate)
                    {
                        using (var fs = new System.IO.FileStream(file_name, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            var image = System.Drawing.Image.FromStream(fs);
                            if (image != null) return image;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        public static int IndexInPlaylist(string file_name)
        {
            return AppCore.IndexInPlaylist(file_name);
        }
        #endregion

        #region Component operation
        /// <summary>
        /// ロードされているコンポーネントのインスタンスのリストを取得．
        /// </summary>
        /// <returns></returns>
        public static Lutea.Core.LuteaComponentInterface[] GetComponents()
        {
            return AppCore.Plugins.ToArray();
        }

        public static void Reload(Column[] extraColumns)
        {
            AppCore.Reload(extraColumns);
        }
        #endregion

        #region Database operation
        /// <summary>
        /// ライブラリデータベースへの接続を取得する
        /// コンポーネントに対してはReadOnlyのデータベースアクセスを提供したい気もするが気のせい
        /// </summary>
        /// <returns></returns>
        public static SQLite3DB GetDBConnection()
        {
            return AppCore.Library.Connect(false);
        }
        #endregion

        #region User directory operation
        public static string UserDirectory
        {
            get
            {
                return AppCore.userDirectory.UserDir;
            }
        }
        #endregion

        #region Library operation
        public static void SetRating(string filename, int rate)
        {
            SetRating(new String[] { filename }, rate);
        }

        public static void SetRating(string[] filenames, int rate)
        {
            AppCore.CoreEnqueue(() =>
            {
                using (var db = GetDBConnection())
                {
                    db.Exec("BEGIN;");
                    using (var stmt = db.Prepare("UPDATE list SET " + LibraryDBColumnTextMinimum.rating + " = ? WHERE " + LibraryDBColumnTextMinimum.file_name + " = ? ;"))
                    {
                        foreach (var file_name in filenames)
                        {
                            stmt.Bind(1, rate.ToString());
                            stmt.Bind(2, file_name);
                            stmt.Evaluate(null);
                            stmt.Reset();
                            var row = AppCore.PlaylistCache.First(((o) => o != null && ((string)o[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)]) == file_name));
                            if (row != null)
                            {
                                row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.rating)] = rate.ToString();
                            }
                        }
                    }
                    db.Exec("COMMIT;");
                }
                _PlaylistUpdated(null);
            });
        }

        public static List<string> GetDeadLink(VOIDINT callbackMax = null, VOIDINT callbackStep = null)
        {
            object[][] file_names;
            List<string> dead_filenames = new List<string>();
            using (var db = GetDBConnection())
            {
                using (var stmt = db.Prepare("SELECT " + LibraryDBColumnTextMinimum.file_name + " FROM list;"))
                {
                    file_names = stmt.EvaluateAll();
                    if (callbackMax != null) { callbackMax(file_names.Length); }
                    int i = 0;
                    foreach (var _file_name in file_names)
                    {
                        if (callbackStep != null && i % 10 == 0) { callbackStep(i); }
                        string file_name = _file_name[0].ToString();
                        if (!System.IO.File.Exists(file_name))
                        {
                            dead_filenames.Add(file_name);
                        }
                        i++;
                    }
                }
            }
            return dead_filenames;
        }

        public static Boolean removeItem(IEnumerable<string> file_names)
        {
            using (var db = GetDBConnection())
            {
                db.Exec("BEGIN;");
                using (var stmt = db.Prepare("DELETE FROM list WHERE " + LibraryDBColumnTextMinimum.file_name + " = ?;"))
                {
                    foreach (var file_name in file_names)
                    {
                        stmt.Bind(1, file_name);
                        stmt.Evaluate(null);
                        stmt.Reset();

                    }
                }
                db.Exec("COMMIT;");
            }
            AppCore.DatabaseUpdated();
            return true;
        }

        /// <summary>
        /// ライブラリのカラム一覧を返す．
        /// Cloneするのでちょっと重いのであんまり呼ばない．
        /// プラグインのインスタンス内でキャッシュしてよいので．
        /// </summary>
        public static Column[] Columns
        {
            get
            {
                return (Column[])AppCore.Library.Columns.Clone();
            }
        }

        public static Column[] ExtraColumns
        {
            get
            {
                return AppCore.Library.GetExtraColumns();
            }
        }

        /// <summary>
        /// ライブラリのカラム名からカラム番号を取得する．
        /// カラム番号は0オリジン．
        /// 今の構造では動作中にライブラリのカラムが変更されることはないため、
        /// ディクショナリにキャッシュしている．
        /// </summary>
        /// <param name="Name">カラム名</param>
        /// <returns>カラム番号．0オリジン，エラー時は-1</returns>
        private static Dictionary<string, int> columnIndexCache = null;
        public static int GetColumnIndexByName(string Name)
        {
            if (string.IsNullOrEmpty(Name)) return -1;
            if (columnIndexCache == null)
            {
                var tmp = new Dictionary<string, int>();
                for (int i = 0; i < AppCore.Library.Columns.Length; i++)
                {
                    tmp.Add(AppCore.Library.Columns[i].Name, i);
                }
                columnIndexCache = tmp;
            }
            return columnIndexCache.ContainsKey(Name) ? columnIndexCache[Name] : -1;
        }

        public static KaoriYa.Migemo.Migemo GetMigemo()
        {
            return AppCore.Migemo;
        }
        #endregion

        #region Playlist operation
        /// <summary>
        /// プレイリストの項目数を取得
        /// </summary>
        public static int PlaylistRowCount
        {
            get
            {
                return AppCore.currentPlaylistRows;
            }
        }

        /// <summary>
        /// プレイリストを生成する
        /// </summary>
        /// <param name="query">SQL文または検索語</param>
        /// <param name="playOnCreate">プレイリスト生成後再生を開始</param>
        public static void CreatePlaylist(String query, bool playOnCreate = false)
        {
            AppCore.createPlaylist(query, playOnCreate);
        }

        /// <summary>
        /// 最後にプレイリストを生成したクエリ/検索語
        /// </summary>
        public static string LatestPlaylistQuery
        {
            get { return AppCore.LatestPlaylistQuery; }
        }

        /// <summary>
        /// 最後にプレイリストを生成したクエリ．正規表現や検索語をSQL文に展開している
        /// </summary>
        public static string LatestPlaylistQueryExpanded
        {
            get { return AppCore.LatestPlaylistQueryExpanded; }
        }

        /// <summary>
        /// プレイリストのindex行目の内容を取得する．ライブラリのカラム全ての内容を配列で返す．
        /// </summary>
        /// <param name="rowid">取得する行番号</param>
        /// <returns></returns>
        public static object[] GetPlaylistRow(int rowid)
        {
            // rowidのチェックはCore内で行うので省略
            return AppCore.GetPlaylistRow(rowid);
        }

        /// <summary>
        /// プレイリストのindex行目のindex番目のカラムの内容を取得する．
        /// </summary>
        /// <param name="rowid">取得する行番号</param>
        /// <param name="columnid">取得するカラム番号</param>
        /// <returns>指定した行・カラムの内容またはnull</returns>
        public static string GetPlaylistRowColumn(int rowid, int columnid)
        {
            // columnidチェック
            if (columnid < 0) return null;

            // rowidのチェックはCore内で行うので省略
            var row = AppCore.GetPlaylistRow(rowid);
            if (row == null) return null;
            if (row.Length <= columnid) return null;
            if (row[columnid] == null) return null;

            return row[columnid].ToString();
        }

        /// <summary>
        /// プレイリストをソートするカラムを設定する
        /// [ソートなし]に設定する場合，columnText=nullとする
        /// </summary>
        /// <param name="columnName">ソートに使用するカラム名</param>
        /// <param name="sortOrder">ソート順</param>
        public static void SetSortColumn(String columnName, SortOrders sortOrder = SortOrders.KeepOrFlip)
        {
            // ソートなし設定
            if (columnName == null)
            {
                AppCore.PlaylistSortColumn = null;
            }
            else if (GetColumnIndexByName(columnName) >= 0)
            {
                // ソート順を設定
                if (sortOrder == SortOrders.KeepOrFlip)
                {
                    // ソート順を交換
                    if (AppCore.PlaylistSortColumn == columnName)
                    {
                        if (AppCore.PlaylistSortOrder == SortOrders.Asc)
                        {
                            AppCore.PlaylistSortOrder = SortOrders.Desc;
                        }else{
                            AppCore.PlaylistSortOrder = SortOrders.Asc;
                        }
                    }
                }else{
                    AppCore.PlaylistSortOrder = sortOrder;
                }

                // ソート対象カラムを設定
                AppCore.PlaylistSortColumn = columnName;
            }

            // ソート順変更イベントを通知
            if (PlaylistSortOrderChanged != null)
            {
                var lambda = (Action)(() => {
                    AppCore.CreateOrderedPlaylist(columnName, sortOrder);
                    PlaylistSortOrderChanged.Invoke(AppCore.PlaylistSortColumn, AppCore.PlaylistSortOrder);
                });
                lambda.BeginInvoke(_ => lambda.EndInvoke(_), null);
            }

        }

        /// <summary>
        /// tagAlbumの連続数行列を返す
        /// </summary>
        /// <returns></returns>
        public static int[] GetTagAlbumContinuousCount()
        {
            if (AppCore.TagAlbumContinuousCount == null) return null;
            return (int[])AppCore.TagAlbumContinuousCount.Clone();
        }
        #endregion

        #region Interface between Core
        /// <summary>
        /// Coreより,Core,プラグイン初期化後に呼ばれる．
        /// 初期イベント通知とController部の動作を開始．
        /// </summary>
        internal static void Startup()
        {
            if (onVolumeChange != null) onVolumeChange.Invoke();
            if (onPlaybackOrderChange != null) onPlaybackOrderChange.Invoke();
            if (PlaylistUpdated != null) PlaylistUpdated.Invoke(AppCore.LatestPlaylistQuery);
            if (PlaylistSortOrderChanged != null) PlaylistSortOrderChanged.Invoke(AppCore.PlaylistSortColumn, AppCore.PlaylistSortOrder);

            elapsedTimeWatcherThread = new Thread(elapsedTimeWatcher);
            elapsedTimeWatcherThread.IsBackground = true;
            elapsedTimeWatcherThread.Start();
        }

        #region Event invocation request by Core
        internal static void _PlaylistUpdated(string sql)
        {
            if (PlaylistUpdated == null) return;
            PlaylistUpdated.Invoke(sql);
        }

        internal static void _OnTrackChange(int index)
        {
            elapsedtime = -1;
            Current.CacheIsOutdated = true;
            if (onTrackChange == null) return;
            foreach (var dlg in onTrackChange.GetInvocationList())
            {
                var _dlg = (VOIDINT)dlg;
                _dlg.BeginInvoke(index, (_ => { _dlg.EndInvoke(_); }), null);
            }
        }

        internal static void _OnPlaybackErrorOccured()
        {
            if (onPlaybackErrorOccured == null) return;
            foreach (var dlg in onPlaybackErrorOccured.GetInvocationList())
            {
                var _dlg = (VOIDVOID)dlg;
                _dlg.BeginInvoke((_ => { _dlg.EndInvoke(_); }), null);
            }
        }

        internal static void _OnDatabaseUpdated()
        {
            if (onDatabaseUpdated == null) return;
            foreach (var dlg in onDatabaseUpdated.GetInvocationList())
            {
                var _dlg = (VOIDVOID)dlg;
                _dlg.BeginInvoke((_ => { _dlg.EndInvoke(_); }), null);
            }
        }
        #endregion
        #endregion
    }
}
