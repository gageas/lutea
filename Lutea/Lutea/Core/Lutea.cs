﻿using System;
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
            float_32bit,
            integer_8bit,
            integer_16bit,
            integer_24bit,
            integer_32bit,
        }

        /// <summary>
        /// FFTポイント列挙体
        /// </summary>
        public enum FFTNum : uint
        {
            FFT256 = 256,
            FFT512 = 512,
            FFT1024 = 1024,
            FFT2048 = 2048,
            FFT4096 = 4096,
            FFT8192 = 8192,
        };

        /// <summary>
        /// 再生モード列挙体
        /// </summary>
        public enum PlaybackOrder { Default, Endless, Track, Random, Album };
        #endregion

        #region Event definition
        // Event definition

        // general purpose delegates
        public delegate void VOIDVOID();
        public delegate void VOIDINT(int sec);
        public static event VOIDINT onTrackChange;
        public static event VOIDVOID onPause;
        public static event VOIDVOID onResume;
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

        #region Output Channel
        /// <summary>
        /// 出力チャンネルのFFTデータを取得する．
        /// </summary>
        /// <param name="buffer">出力先</param>
        /// <param name="fftopt">FFTオプション．ポイント数他</param>
        /// <returns></returns>
        public static uint FFTData(float[] buffer, FFTNum fftopt){
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
                AppCore.Quit();
            }
            catch(Exception ex)
            {
                Logger.Error(ex);
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
                if (AppCore.Pause != value)
                {
                    AppCore.Pause = value;
                    var evt = (value ? onPause : onResume);
                    if (evt != null) evt.Invoke();
                }
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

        public static bool QueueNext(int index)
        {
            if (IsPlaying)
            {
                return AppCore.QueuePlaylistItem(index);
            }
            else
            {
                return AppCore.PlayPlaylistItem(index);
            }
        }

        public static void QueueStop()
        {
            if (IsPlaying)
            {
                AppCore.QueuePlaylistItem(AppCore.QUEUE_STOP);
            }
        }

        public static void QueueClear()
        {
            AppCore.QueuePlaylistItem(AppCore.QUEUE_CLEAR);
        }

        /// <summary>
        /// プレイリストの指定したindex番目のトラックを再生する
        /// </summary>
        /// <param name="index"></param>
        public static void PlayPlaylistItem(int index)
        {
            AppCore.CoreEnqueue(() =>
            {
                icache = index;
                AppCore.PlayPlaylistItem(index);
            });
        }

        public static void Stop()
        {
            AppCore.CoreEnqueue(() =>
            {
                AppCore.Stop();
            });
        }

        public static void NextTrack()
        {
            Logger.Log("next Track");
            AppCore.CoreEnqueue(() => {
                int i = (icache > 0 ? icache : Current.IndexInPlaylist);
                if (playbackOrder == Controller.PlaybackOrder.Track)
                {
                    icache = (i + 1 >= PlaylistRowCount) ? 0 : i + 1;
                }
                else
                {
                    icache = AppCore.GetSuccTrackIndex();
                }
                AppCore.PlayPlaylistItem(icache);
            });
        }

        public static void PrevTrack()
        {
            Logger.Log("prev Track");
            AppCore.CoreEnqueue(() =>
            {
                int i = (icache > 0 ? icache : Current.IndexInPlaylist);
                if (i == -1)
                {
                    icache = 0;
                }
                else
                {
                    if (Current.Position > 5) // 現在位置が5秒以内なら現在のトラックの頭に
                    {
                        icache = i;
                    }
                    else
                    {
                        icache = (i - 1 < 0) ? PlaylistRowCount - 1 : i - 1;
                    }
                }
                AppCore.PlayPlaylistItem(icache);
            });
        }
        #endregion

        #region Current track's Information
        public static class Current
        {
            public static double Length
            {
                get
                {
                    return AppCore.GetLength();
                }
            }
            public static double Position
            {
                get
                {
                    return AppCore.GetPosition();
                }
                set
                {
                    AppCore.SetPosition(value);
                }
            }
            public static String MetaData(int colidx)
            {
                if (AppCore.CurrentStream == null) return null;
                if (colidx < 0 || colidx >= AppCore.CurrentStream.meta.Length) return null;
                return AppCore.CurrentStream.meta[colidx].ToString();
            }
            public static String MetaData(Library.Column col)
            {
                return MetaData(GetColumnIndexByName(col.Name));
            }
            public static String MetaData(string DBText)
            {
                return MetaData(GetColumnIndexByName(DBText));
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
                    return MetaData(LibraryDBColumnTextMinimum.file_name);
                }
            }
            public static String StreamFilename
            {
                get
                {
                    if (AppCore.CurrentStream == null) return null;
                    return AppCore.CurrentStream.Location;
                }
            }
            public static int IndexInPlaylist
            {
                get
                {
                    if (AppCore.CurrentStream == null) return -1;
                    return IndexInPlaylist(AppCore.CurrentStream.DatabaseFileName);
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

            private static string[] GetLyricsByTagExt(string _filename, string ext)
            {
                var invalidChars = System.IO.Path.GetInvalidPathChars();
                try
                {
                    var asTitleTxtCandidates = System.IO.Directory.GetFiles(System.IO.Path.GetDirectoryName(_filename), new string(Current.MetaData("tagTitle").Select(_ => invalidChars.Contains(_) ? '?' : _).ToArray()) + "." + ext, System.IO.SearchOption.TopDirectoryOnly);
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

            private static string[] GetLyricsByFilenameExt(string _filename, string ext)
            {
                try
                {
                    var asTxt = System.IO.Path.ChangeExtension(_filename, ext);
                    if (System.IO.File.Exists(asTxt))
                    {
                        return ReadAllLinesAutoEncoding(asTxt);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
                return null;
            }

            public static string[] GetLyrics()
            {
                if (Filename == null) return null;
                var _filename = Filename.Trim();
                // Internal cueから検索
                try
                {
                    var cue = InternalCUEReader.Read(_filename, false);
                    if (cue != null)
                    {
                        int track = Util.Util.GetTrackNumberInt(MetaData("tagTracknumber"), 1);
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

                string[] ret;

                // lrcファイルから検索
                ret = GetLyricsByFilenameExt(_filename, "lrc");
                if (ret != null) return ret;

                // txtファイルから検索
                ret = GetLyricsByFilenameExt(_filename, "txt");
                if (ret != null) return ret;

                // タイトル.lrcファイルから検索
                ret = GetLyricsByTagExt(_filename, "lrc");
                if (ret != null) return ret;

                // タイトル.txtファイルから検索
                ret = GetLyricsByTagExt(_filename, "txt");
                if (ret != null) return ret;

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
            return AppCore.Components;
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
            return AppCore.Library.Connect();
        }
        #endregion

        #region User directory operation
        public static string UserDirectory
        {
            get
            {
                return AppCore.MyUserDirectory.UserDir;
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
                            AppCore.InvalidatePlaylistCache(file_name);
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
                // ループでDELETEするとBEGIN,COMMIT内でも異常に遅いので、適当なカラムを無効な値に設定してフラグにして一気に削除する。
                db.Exec("BEGIN;");
                using (var stmt = db.Prepare("UPDATE list SET file_size = -1 WHERE " + LibraryDBColumnTextMinimum.file_name + " = ?;"))
                {
                    foreach (var file_name in file_names)
                    {
                        stmt.Bind(1, file_name);
                        stmt.Evaluate(null);
                    }
                }
                db.Exec("DELETE FROM list WHERE file_size = -1;");
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

        private static Dictionary<string, int> columnIndexCache = null;
        /// <summary>
        /// ライブラリのカラム名からカラム番号を取得する．
        /// カラム番号は0オリジン．
        /// 今の構造では動作中にライブラリのカラムが変更されることはないため、
        /// ディクショナリにキャッシュしている．
        /// </summary>
        /// <param name="Name">カラム名</param>
        /// <returns>カラム番号．0オリジン，エラー時は-1</returns>
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
            return AppCore.MyMigemo;
        }

        /// <summary>
        /// MultipleValuesの値のリストを取得
        /// </summary>
        /// <param name="columnName">値を取得するデータベース内のカラムの名前</param>
        /// <param name="wherePhraseBody">WHERE条件。不要の場合はnull</param>
        /// <returns>MultipleValuesのvalue(string)、件数(int)によるKeyValuePairのリスト</returns>
        public static IEnumerable<KeyValuePair<string, int>> FetchColumnValueMultipleValue(string columnName, string wherePhraseBody)
        {
            var sql = "SELECT " + columnName + " ,COUNT(*) FROM list " + (string.IsNullOrEmpty(wherePhraseBody) ? "" : ("WHERE " + wherePhraseBody)) + " GROUP BY " + columnName + " ORDER BY " + columnName + " desc;";
            using (var db = Controller.GetDBConnection())
            using (var stmt = db.Prepare(sql))
            {
                // タグの値の文字列を改行で分割して，個別の値に分離
                return stmt.EvaluateAll()
                    .SelectMany(_ => ((string)_[0]).Split('\n').Select(__ => new KeyValuePair<string, int>(__, int.Parse(((string)_[1])))))
                    .GroupBy(_ => _.Key, (_key, _values) => new KeyValuePair<string, int>(_key, _values.Select(_ => _.Value).Sum()));
            }
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
                return AppCore.CurrentPlaylistRows;
            }
        }

        /// <summary>
        /// プレイリストを生成する
        /// </summary>
        /// <param name="query">SQL文または検索語</param>
        /// <param name="playOnCreate">プレイリスト生成後再生を開始</param>
        public static void CreatePlaylist(String query, bool playOnCreate = false)
        {
            AppCore.CreatePlaylist(query, playOnCreate);
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
            if (GetColumnIndexByName(columnName) >= 0)
            {
                // ソート順を交換
                if (sortOrder == SortOrders.KeepOrFlip && AppCore.PlaylistSortColumn == columnName)
                {
                    sortOrder = AppCore.PlaylistSortOrder == SortOrders.Asc ? SortOrders.Desc : SortOrders.Asc;
                }
            }
            else
            {
                columnName = null;
            }

            // ソート順変更イベントを通知
            if (PlaylistSortOrderChanged != null)
            {
                var lambda = (Action)(() =>
                {
                    AppCore.SetPlaylistSort(columnName, sortOrder);
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
        }

        #region Event invocation request by Core
        internal static void _PlaylistUpdated(string sql)
        {
            if (PlaylistUpdated == null) return;
            PlaylistUpdated.Invoke(sql);
        }

        internal static void _OnElapsedTimeChange(int t)
        {
            if (t > 0) icache = -1;
            if (onElapsedTimeChange != null)
            {
                foreach (var dlg in onElapsedTimeChange.GetInvocationList())
                {
                    var _dlg = (VOIDINT)dlg;
                    _dlg.BeginInvoke(t, (_ => { _dlg.EndInvoke(_); }), null);
                }
            }
        }

        internal static void _OnTrackChange(int index)
        {
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
