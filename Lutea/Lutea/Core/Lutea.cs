using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;

using Gageas.Wrapper.BASS;
using Gageas.Wrapper.SQLite3;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// Databaseに定義されているColumn
    /// </summary>
    public enum DBCol
    {
        file_name, file_title, file_ext, file_size,
        tagTitle, tagArtist, tagAlbum, tagGenre, tagDate, tagComment, tagTracknumber, tagAPIC, tagLyrics,
        statDuration, statChannels, statSamplingrate, statBitrate, statVBR,
        infoCodec, infoCodec_sub, infoTagtype, gain, rating, playcount, lastplayed, modify
    };

    /// <summary>
    /// クエリ実行モードの列挙体
    /// </summary>
    public enum SearchQueryMode
    {
        AUTO,
        RAWSQL,
        REGEXP,
        MIGEMO,
        LIKE
    };

    /// <summary>
    /// アプリケーションのコントローラ
    /// AppCoreから公開インタフェースを分離したが、中途半端
    /// </summary>
    public static class Controller
    {
        public enum OutputModeEnum
        {
            STOP,
            Integer16,
            FloatingPoint,
            ASIO,
            WASAPI,
            WASAPIEx,
        }
        // 再生モード列挙体
        public enum PlaybackOrder { Default, Track, Random };

        // FIXME?: 値を書き換えられてしまう
        public static uint FFTData(float[] buffer, Wrapper.BASS.BASS.IPlayable.FFT fftopt){
            if (AppCore.outputChannel != null)
            {
                return AppCore.outputChannel.GetDataFFT(buffer, fftopt);
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

        public static Boolean IsPlaying
        {
            get
            {
                return AppCore.isPlaying;
            }
        }

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
        #endregion
        /// <summary>
        /// databaseのcolumnからテキストへのマッピングを表すDictionary
        /// FIXME: 書き換えられるので後でなおす
        /// </summary>
        #region ColumnLocalStringMapping
        private static Dictionary<DBCol, string> ColumnLocalStringMapping = new Dictionary<DBCol, string>()
        {
            {DBCol.file_name,"ファイルパス"},
            {DBCol.file_title,"ファイル名"},
            {DBCol.file_ext,"拡張子"},
            {DBCol.file_size,"ファイルサイズ"},

            {DBCol.statDuration,"長さ"},
            {DBCol.statBitrate,"ビットレート"},
            {DBCol.statSamplingrate,"サンプリング周波数"},
            {DBCol.statChannels,"チャンネル"},
            {DBCol.statVBR,"VBRフラグ(未使用)"},

            {DBCol.lastplayed,"最終再生日"},
            {DBCol.modify,"最終更新日"},
            {DBCol.rating,"評価"},
            {DBCol.playcount,"再生回数"},
            {DBCol.gain,"ゲイン(未使用)"},

            {DBCol.infoCodec,"コーデック"},
            {DBCol.infoCodec_sub,"コーデック2"},
            {DBCol.infoTagtype,"タグ形式(未使用)"},

            {DBCol.tagTracknumber,"No"},
            {DBCol.tagTitle,"タイトル"},
            {DBCol.tagArtist,"アーティスト"},
            {DBCol.tagAlbum,"アルバム"},
            {DBCol.tagGenre,"ジャンル"},
            {DBCol.tagComment,"コメント"},
            {DBCol.tagDate,"年"},
            {DBCol.tagAPIC,"カバーアート(未使用)"},
            {DBCol.tagLyrics,"歌詞(未使用)"},

        };
        public static string GetColumnLocalString(DBCol col)
        {
            if (ColumnLocalStringMapping.ContainsKey(col)) return ColumnLocalStringMapping[col];
            return col.ToString();
        }
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
                        onElapsedTimeChange.BeginInvoke(t, null, null);
                        GC.Collect();
                    }
                    if (t > 0) icache = -1;
                }
            }
        }
        #endregion

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
//            KillOutputChannel();
            NextTrack();
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
            //            KillOutputChannel();
            AppCore.CoreEnqueue((VOIDVOID)(() => {
                int i = (icache > 0 ? icache : Current.IndexInPlaylist);
                int id;
                if (playbackOrder == Controller.PlaybackOrder.Random)
                {
                    if (CurrentPlaylistRows == 1) id = 0;
                    else
                    {
                        do
                        {
                            id = (new Random()).Next(CurrentPlaylistRows);
                        } while (id == i);
                    }
                }
                else
                {
                    id = (i) + 1;
                    if (id >= CurrentPlaylistRows)
                    {
                        id = 0;
                    }
                }
                icache = id;
                AppCore.PlayPlaylistItem(id, stopCurrent);
            }));
        }
        /// <summary>
        /// prev,next連打時用。直前のprev, nextで選択したidを保持。再生開始後破棄。
        /// </summary>
        private static int icache;
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
                        //                    id = cache.First((e) => e.Value == currentPlaylistRow).Key; // (currentPlaylistRow);
                        id = i;
                    }
                    else
                    {
                        //                    id = cache.First((e) => e.Value == currentPlaylistRow).Key - 1;
                        id = i - 1;
                    }
                }
                if (id < 0)
                {
                    id = CurrentPlaylistRows - 1;
                }
                icache = id;
                AppCore.PlayPlaylistItem(id);
            }));

//            AppCore.KillOutputChannel();
        }
        #region 再生中のトラックに関する情報
        // 再生中のトラックに関する情報を取得するための静的クラス
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
                    var _current = AppCore.currentStream;
                    if (_current == null) return;
                    //                    KillOutputChannel();
                    AppCore.outputChannel.Pause();
                    if (_current.invalidateCueLengthOnSeek)
                    {
                        _current.cueLength = 0;
                    }
                    _current.stream.position = _current.stream.Seconds2Bytes(value) + _current.cueOffset;
//                    AppCore.ResetOutputChannel(AppCore.currentStream.stream.GetFreq(), (AppCore.currentStream.stream.Info.flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) != 0);
                    AppCore.outputChannel.Start();
                }
            }
            public static String MetaData(DBCol col)
            {
                if (AppCore.currentStream == null) return null;
                if ((int)col >= AppCore.currentStream.meta.Length) return null;
                return AppCore.currentStream.meta[(int)col].ToString();
            }
            public static int Rating
            {
                get
                {
                    return int.Parse(MetaData(DBCol.rating));
                }
                set
                {
                    AppCore.CoreEnqueue(() =>
                    {
                        using (var db = DBConnection)
                        {
                            using(var stmt = db.Prepare("UPDATE list SET rating = " + value + " WHERE file_name = '" + Filename.EscapeSingleQuotSQL() + "'")){
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
                    return MetaData(DBCol.file_name);
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
        }

        public static int IndexInPlaylist(string file_name)
        {
            return AppCore.IndexInPlaylist(file_name);
        }
        #endregion


        public static Lutea.Core.LuteaComponentInterface[] GetComponents()
        {
            return AppCore.plugins.ToArray();
        }

        public static OutputModeEnum OutputMode
        {
            get
            {
                return AppCore.OutputMode;
            }
        }

        public static SQLite3DB DBConnection
        {
            get
            {
                return AppCore.Library.Connect(false);
            }
        }

        public static string UserDirectory
        {
            get
            {
                return AppCore.userDirectory.UserDir;
            }
        }

        public static int CurrentPlaylistRows
        {
            get
            {
                return AppCore.currentPlaylistRows;
            }
        }

        public static void SetRating(string filename, int rate)
        {
            AppCore.CoreEnqueue(() =>
            {
                using (var db = DBConnection)
                {
                    using (var stmt = db.Prepare("UPDATE list SET rating = " + rate + " WHERE file_name = '" + filename.EscapeSingleQuotSQL() + "';"))
                    {
                        stmt.Evaluate(null);
                    }
                }
                //                    AppCore.DatabaseWriterQueue.Enqueue("UPDATE list SET rating = " + rate + " WHERE file_name = '" + filename.EscapeSingleQuotSQL() + "';");
                var row = AppCore.playlistCache.First(((o) => ((string)o[(int)DBCol.file_name]) == filename));
                if (row != null)
                {
                    row[(int)DBCol.rating] = rate.ToString();
                }
                _PlaylistUpdated(null);
            });
        }
        public static void SetRating(string[] filenames, int rate)
        {
            AppCore.CoreEnqueue(() => {
                foreach (string filename in filenames)
                {
                    using (var db = DBConnection)
                    {
                        using (var stmt = db.Prepare("UPDATE list SET rating = " + rate + " WHERE file_name = '" + filename.EscapeSingleQuotSQL() + "';"))
                        {
                            stmt.Evaluate(null);
                        }
                    }
//                    AppCore.DatabaseWriterQueue.Enqueue("UPDATE list SET rating = " + rate + " WHERE file_name = '" + filename.EscapeSingleQuotSQL() + "';");
                    var row = AppCore.playlistCache.First(((o) => ((string)o[(int)DBCol.file_name]) == filename));
                    if (row != null)
                    {
                        row[(int)DBCol.rating] = rate.ToString();
                    }
                }
                _PlaylistUpdated(null);
            });
        }

        public static DateTime timestamp2DateTime(Int64 timestamp)
        {
            return H2k6Library.timestamp2DateTime(timestamp);
        }

        public static void PlayPlaylistItem(int index)
        {
            AppCore.CoreEnqueue((VOIDVOID)(() =>
            {
                AppCore.PlayPlaylistItem(index);
            }));
        }

        public static void createPlaylist(String sql, bool playOnCreate = false)
        {
            AppCore.createPlaylist(sql, playOnCreate);
        }

        public static string LatestPlaylistQuery
        {
            get { return AppCore.latestPlaylistQuery; }
        }

        public static string LatestPlaylistQuerySub
        {
            get { return AppCore.latestPlaylistQuerySub; }
        }

        public static object[] GetPlaylistRow(int index)
        {
            if (index < 0) return null;
            // このメソッドの呼び出し中にcacheの参照が変わる可能性があるので、最初に参照をコピーする
            // 一時的に古いcacheの内容を吐いても問題ないので、mutexで固めるほどではない
            var _cache = AppCore.playlistCache;
            if (_cache.Length <= index) return null;
            object[] value = null;
            if (_cache[index] == null) _cache[index] = AppCore.h2k6db.FetchRow("playlist", index + 1);
            value = _cache[index];
            if (value == null || value.Length == 0) return null;
            return value;
        }

        public static string GetPlaylistRowColumn(int rowindex, DBCol column)
        {
            var row = GetPlaylistRow(rowindex);
            if(row == null)return null;
            if (row.Length <= (int)column) return null;
            return row[(int)column].ToString();
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

        public static List<string> GetDeadLink()
        {
            object[][] file_names;
            List<string> dead_filenames = new List<string>();
            using (var db = DBConnection)
            {
                using (var stmt = db.Prepare("SELECT file_name FROM list;"))
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
            using (var db = DBConnection)
            {
                db.Exec("BEGIN;");
                using (var stmt = db.Prepare("DELETE FROM list WHERE file_name = ?;"))
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

        internal static void Startup()
        {
            if (onVolumeChange != null) onVolumeChange.Invoke();
            if (onPlaybackOrderChange != null) onPlaybackOrderChange.Invoke();
            PlaylistUpdated.Invoke(AppCore.latestPlaylistQuery);


            elapsedTimeWatcherThread = new Thread(elapsedTimeWatcher);
            elapsedTimeWatcherThread.IsBackground = true;
            elapsedTimeWatcherThread.Start();
        }

        internal static void _PlaylistUpdated(string sql)
        {
            if (PlaylistUpdated == null) return;
            PlaylistUpdated.Invoke(sql);
        }

        internal static void _OnTrackChange(int index)
        {
            elapsedtime = -1;
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
    }
}
