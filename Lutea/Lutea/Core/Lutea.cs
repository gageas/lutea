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
                if (!AppCore.isPlaying) continue;
                if (onElapsedTimeChange != null)
                {
                    int t = (int)Controller.Current.Position;
                    if (t == -1) continue; // 再生開始前
                    if (t != elapsedtime)
                    {
                        elapsedtime = t;
                        onElapsedTimeChange.BeginInvoke(t, (_ => { onElapsedTimeChange.EndInvoke(_); }), null);
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
                return AppCore.isPlaying;
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

        public static bool Mute
        {
            get
            {
                return AppCore.mute;
            }
            set
            {
                AppCore.mute = value;
            }
        }

        public static bool Pause
        {
            get
            {
                return AppCore.pause;
            }
            set
            {
                AppCore.pause = value;
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
                return AppCore.volume;
            }
            set
            {
                AppCore.volume = value;
                if (onVolumeChange != null) onVolumeChange.Invoke();
            }
        }

        public static PlaybackOrder playbackOrder
        {
            get
            {
                return AppCore.playbackOrder;
            }
            set
            {
                AppCore.playbackOrder = value;
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
                    return (AppCore.currentStream != null) ? (AppCore.currentStream.cueLength > 0 ? AppCore.currentStream.stream.Bytes2Seconds(AppCore.currentStream.cueLength) : AppCore.currentStream.stream.length) : 0;
                }
            }
            public static double Position
            {
                get
                {
                    return (AppCore.currentStream != null) ? AppCore.currentStream.stream.positionSec - AppCore.currentStream.stream.Bytes2Seconds(AppCore.currentStream.cueOffset) : 0;
                }
                set
                {
                    AppCore.SetPosition(value);
                }
            }
            public static String MetaData(Library.Column col)
            {
                if (AppCore.currentStream == null) return null;
                int idx = AppCore.Library.Columns.ToList().IndexOf(col);
                if (idx < 0 || idx >= AppCore.currentStream.meta.Length) return null;
                return AppCore.currentStream.meta[idx].ToString();
            }
            public static String MetaData(int colidx)
            {
                if (AppCore.currentStream == null) return null;
                if (colidx < 0 || colidx >= AppCore.currentStream.meta.Length) return null;
                return AppCore.currentStream.meta[colidx].ToString();
            }
            public static String MetaData(string DBText)
            {
                if (AppCore.currentStream == null) return null;
                int idx = AppCore.Library.Columns.ToList().IndexOf(AppCore.Library.Columns.First(_ => _.Name == DBText));
                if (idx < 0 || idx >= AppCore.currentStream.meta.Length) return null;
                return AppCore.currentStream.meta[idx].ToString();
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
                    if (AppCore.currentStream == null) return null;
                    return AppCore.currentStream.cueStreamFileName != null ? AppCore.currentStream.cueStreamFileName : Filename;
                }
            }
            public static int IndexInPlaylist
            {
                get
                {
                    if (AppCore.currentStream == null) return -1;
                    return IndexInPlaylist(AppCore.currentStream.file_name);
                }
            }
            
            /// <summary>
            /// 現在のカバーアートをImageオブジェクトとして返す。
            /// カバーアートが無ければdefault.jpgのImageオブジェクトを返す。
            /// default.jpgも見つからなければnullを返す。
            /// FIXME?: この機能はCoreに移すかも
            /// </summary>
            /// <returns></returns>
            internal static bool coverArtImageIsInvalid = false;
            private static System.Drawing.Image coverArtImage = null;
            private static readonly object GetCoverArtImageLock = new object();
            public static System.Drawing.Image CoverArtImage()
            {
                lock (GetCoverArtImageLock)
                {
                    if (coverArtImageIsInvalid)
                    {
                        coverArtImage = CoverArtImageForFile(StreamFilename);
                        coverArtImageIsInvalid = false;
                    }
                    if (coverArtImage == null) return null;
                    lock (coverArtImage)
                    {
                        return new System.Drawing.Bitmap(coverArtImage);
                    }
                }
            }
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
            String[] searchPatterns = { "folder.jpg", "folder.jpeg", "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp" };
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
            return AppCore.plugins.ToArray();
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
                            var row = AppCore.playlistCache.First(((o) => ((string)o[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)]) == file_name));
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

        public static List<string> GetDeadLink()
        {
            object[][] file_names;
            List<string> dead_filenames = new List<string>();
            using (var db = GetDBConnection())
            {
                using (var stmt = db.Prepare("SELECT " + LibraryDBColumnTextMinimum.file_name + " FROM list;"))
                {
                    file_names = stmt.EvaluateAll();
                    foreach (var _file_name in file_names)
                    {
                        string file_name = _file_name[0].ToString();
                        if (!System.IO.File.Exists(file_name))
                        {
                            dead_filenames.Add(file_name);
                        }
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
        /// </summary>
        /// <param name="Name">カラム名</param>
        /// <returns>カラム番号．0オリジン，エラー時は-1</returns>
        public static int GetColumnIndexByName(string Name)
        {
            return AppCore.Library.Columns.ToList().IndexOf(AppCore.Library.Columns.FirstOrDefault(_ => _.Name == Name));
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
            get { return AppCore.latestPlaylistQuery; }
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
            if (PlaylistUpdated != null) PlaylistUpdated.Invoke(AppCore.latestPlaylistQuery);
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
            Current.coverArtImageIsInvalid = true;
            if (onTrackChange == null) return;
            onTrackChange.Invoke(index);
        }

        internal static void _OnPlaybackErrorOccured()
        {
            if (onPlaybackErrorOccured == null) return;
            onPlaybackErrorOccured.Invoke();
        }

        internal static void _OnDatabaseUpdated()
        {
            if (onDatabaseUpdated == null) return;
            onDatabaseUpdated.Invoke();
        }
        #endregion
        #endregion
    }
}
