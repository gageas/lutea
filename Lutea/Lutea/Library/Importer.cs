using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Gageas.Wrapper.BASS;
using Gageas.Wrapper.SQLite3;
using Gageas.Lutea.Tags;
using Gageas.Lutea.Core;
using Gageas.Lutea.Library;

namespace Gageas.Lutea.Library
{
    public sealed class Importer
    {
        private const int WORKER_THREADS_N = 8;
        private const string SelectModifySTMT = "SELECT modify FROM list WHERE file_name = ? OR file_name = ?;";
        private static object LOCKOBJ = new object();
        private readonly string[] supportedExtensions = new string[] { ".MP3", ".MP2", ".M4A", ".MP4", ".TAK", ".FLAC", ".TTA", ".OGG", ".APE", ".WV", ".WMA", ".ASF"};
        private static readonly Regex regex_year = new Regex(@"(?<1>\d{4})");
        private static readonly Regex regex_date = new Regex(@"(?<1>\d{4})[\-\/\.](?<2>\d+)[\-\/\.](?<3>\d+)");

        private string ToBeImportPath;
        private IEnumerable<string> ToBeImportFilenames;
        private Thread ImporterThread; // Importerのメインスレッド
        private List<Thread> Workers; // Importerのワーカスレッド
        private Queue<string> ToBeAnalyzeDirectories;
        private List<LuteaAudioTrack> ToBeImportTracks = new List<LuteaAudioTrack>();
        private Dictionary<string, bool> AlreadyAnalyzedFiles = new Dictionary<string, bool>();
        private bool IsFastMode; // ファイルのタイムスタンプを見て省略するモード

        public event Controller.VOIDINT SetMaximum_read = new Controller.VOIDINT((i) => { });
        public event Controller.VOIDVOID Step_read = new Controller.VOIDVOID(() => { });
        public event Controller.VOIDINT SetMaximum_import = new Controller.VOIDINT((i) => { });
        public event Controller.VOIDVOID Step_import = new Controller.VOIDVOID(() => { });
        public event Controller.VOIDVOID Complete = new Controller.VOIDVOID(() => { });
        public delegate void Message_event(string msg);
        public event Message_event Message = new Message_event((s) => { });

        /// <summary>
        /// ディレクトリに対するインポートを行うImporterを作成する
        /// </summary>
        /// <param name="path">インポート処理の検索対象</param>
        public Importer(string directoryPath, bool fastMode = true)
        {
            this.ToBeImportPath = directoryPath.Trim();
            this.IsFastMode = fastMode;
            this.ImporterThread = new Thread(ImportDirectoryThreadProc);
        }

        /// <summary>
        /// 複数のファイルのインポート処理を行うImporterを作成する
        /// </summary>
        /// <param name="filenames"></param>
        /// <param name="fastMode"></param>
        public Importer(IEnumerable<string> filenames, bool fastMode = true)
        {
            this.ToBeImportFilenames = filenames;
            this.IsFastMode = fastMode;
            this.ImporterThread = new Thread(ImportMultipleFilesThreadProc);
        }

        /// <summary>
        /// インポート処理を開始
        /// </summary>
        public void Start() {
            ImporterThread.Priority = ThreadPriority.BelowNormal;
            ImporterThread.IsBackground = true; 
            ImporterThread.Start();
        }

        /// <summary>
        /// 実行中のインポートを中断する
        /// </summary>
        public void Abort()
        {
            ImporterThread.Abort();
            AbortWorkers();
        }

        /// <summary>
        /// トラックの情報をSTMTにBINDする
        /// </summary>
        /// <param name="stmt"></param>
        /// <param name="track"></param>
        private void BindTrackInfo(SQLite3DB.STMT stmt, LuteaAudioTrack track)
        {
            stmt.Reset();
            string extension = (((track.file_ext == "CUE") && (track is CD.Track)) ? ((CD.Track)track).file_ext_CUESheet : track.file_ext).ToUpper();
            var cols = GetToBeImportColumn().ToArray();
            for (int i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                object value = 0;
                switch (col.Name)
                {
                    case LibraryDBColumnTextMinimum.file_name: value = track.file_name; break;
                    case LibraryDBColumnTextMinimum.file_title: value = track.file_title; break;
                    case LibraryDBColumnTextMinimum.file_ext: value = extension; break;
                    case LibraryDBColumnTextMinimum.file_size: value = track.file_size; break;

                    case LibraryDBColumnTextMinimum.statDuration: value = track.duration; break;
                    case LibraryDBColumnTextMinimum.statChannels: value = track.channels; break;
                    case LibraryDBColumnTextMinimum.statSamplingrate: value = track.freq; break;
                    case LibraryDBColumnTextMinimum.statBitrate: value = track.bitrate; break;
                    case LibraryDBColumnTextMinimum.statVBR: value = 0; break;

                    case LibraryDBColumnTextMinimum.infoCodec: value = track.codec; break;
                    case LibraryDBColumnTextMinimum.infoCodec_sub: value = extension; break;
                    case LibraryDBColumnTextMinimum.infoTagtype: value = 0; break;

                    case LibraryDBColumnTextMinimum.gain: value = 0; break;
                    case LibraryDBColumnTextMinimum.modify: value = MusicLibrary.currentTimestamp; break;

                    default:
                        KeyValuePair<string, object> tagEntry = track.tag.Find((e) => { return e.Key == col.MappedTagField; });
                        if (tagEntry.Key != null)
                        {
                            // DATEの表現形式を正規化して格納する
                            if (col.MappedTagField == "DATE")
                            {
                                var regulated = RegulateTagDate(tagEntry.Value.ToString());
                                value = regulated == null ? tagEntry.Value.ToString() : regulated;
                            }
                            else
                            {
                                value = tagEntry.Value.ToString();
                            }
                        }
                        else
                        {
                            value = "";
                        }
                        break;

                }
                stmt.Bind(i + 1, value.ToString());
            }
        }

        /// <summary>
        /// SQLQueのLibraryDBへのインポート処理を行う
        /// </summary>
        /// <param name="OptimizeDB">完了後にデータベースの最適化を実施するかどうか</param>
        private void RunInsertUpdateQuery(bool OptimizeDB)
        {
            SQLite3DB libraryDB = AppCore.Library.Connect(true);
            try
            {
                libraryDB.Exec("BEGIN;");
                Logger.Log("library.dbへのインポートを開始しました");
            }
            catch
            {
                Logger.Error("library.dbへのインポートを開始できませんでした");
                return;
            }

            SetMaximum_import(ToBeImportTracks.Count);

            lock (ToBeImportTracks)
            {
                using (var stmt_insert = GetInsertPreparedStatement(libraryDB))
                using (var stmt_update = GetUpdatePreparedStatement(libraryDB))
                using (var stmt_test = libraryDB.Prepare("SELECT rowid FROM list WHERE file_name = ?;"))
                {
                    try
                    {
                        ToBeImportTracks.OrderBy(_ =>
                        {
                            int tr = 0;
                            Util.Util.tryParseInt((_.getTagValue("TRACK") ?? "0").ToString(), ref tr);
                            return (_.getTagValue("ALBUM") ?? "") + tr.ToString("0000");
                        }).ToList().ForEach(track =>
                        {
                            Step_import();

                            if (track.duration != 0)
                            {
                                // Titleが無かったらfile_titleを付与
                                if (track.tag.Find((e) => e.Key == "TITLE").Key == null) track.tag.Add(new KeyValuePair<string, object>("TITLE", track.file_title));
                                try
                                {
                                    stmt_test.Reset();
                                    stmt_test.Bind(1, track.file_name);
                                    if (stmt_test.EvaluateAll().Length > 0)
                                    {
                                        stmt_update.Reset();
                                        BindTrackInfo(stmt_update, track);
                                        stmt_update.Evaluate(null);
                                    }
                                    else
                                    {
                                        stmt_insert.Reset();
                                        BindTrackInfo(stmt_insert, track);
                                        stmt_insert.Evaluate(null);
                                    }
                                }
                                catch (SQLite3DB.SQLite3Exception e)
                                {
                                    Logger.Error(e.ToString());
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }
                    ToBeImportTracks.Clear();
                }
                try
                {
                    if (OptimizeDB)
                    {
                        Message("ライブラリを最適化しています");
                    }
                    libraryDB.Exec("COMMIT;");
                    if (OptimizeDB)
                    {
                        libraryDB.Exec("VACUUM;");
                        libraryDB.Exec("REINDEX;");
                    }
                }
                catch
                {
                    Logger.Error("library.dbへのインポートを完了できませんでした");
                }
                finally
                {
                    libraryDB.Dispose();
                }
                Logger.Log("library.dbへのインポートが完了しました");
                AppCore.DatabaseUpdated();
            }
        }

        /// <summary>
        /// CUEシートを解析する
        /// </summary>
        /// <param name="file_name">ファイル名</param>
        /// <param name="lastModifySTMT">modifyを取得するプリペアドステートメント</param>
        private void AnalyzeCUE(string file_name, SQLite3DB.STMT lastModifySTMT)
        {
            if (AlreadyAnalyzedFiles.ContainsKey(file_name)) return;
            lock (AlreadyAnalyzedFiles)
            {
                AlreadyAnalyzedFiles[file_name] = true;
            }
            CD cd = CUEparser.fromFile(file_name, true);
            if (cd == null) return;

            string lastCheckedFilename = null;
            bool   lastCheckedFileShouldSkip = false;
            bool modifyed = !(LastModifyDatetime(lastModifySTMT, file_name) > new System.IO.FileInfo(file_name).LastWriteTimeUtc);
            foreach (CD.Track tr in cd.tracks)
            {
                if (tr.file_name_CUESheet == "") continue;

                // 実体ストリームが存在しない、またはCUESHEETが埋め込まれているなら、.cueファイルとしてのインポートはスキップする。
                if (lastCheckedFilename != tr.file_name_CUESheet)
                {
                    lastCheckedFilename = tr.file_name_CUESheet;
                    lastCheckedFileShouldSkip = false; // 変数初期化

                    // 実体ファイルが存在しないならスキップ
                    if (!System.IO.File.Exists(lastCheckedFilename))
                    {
                        lastCheckedFileShouldSkip = true;
                    }

                    // CUEシートが埋め込まれているならスキップ
                    var tagInRealStream = MetaTag.readTagByFilename(tr.file_name_CUESheet, false);
                    if (tagInRealStream != null)
                    {
                        if(tagInRealStream.Find((e) => e.Key == "CUESHEET").Value != null){
                            lastCheckedFileShouldSkip = true;
                        }
                    }
                }
                // ストリームのタグにCUESHEETがある時はなにもしない
                if (lastCheckedFileShouldSkip)
                {
                    continue;
                }
                var trackIndex = tr.tag.Find((match) => match.Key == "TRACK" ? true : false);
                if (tr.getTagValue("ARTIST") == null) tr.tag.Add(new KeyValuePair<string, object>("ARTIST", cd.artist));
                tr.tag.Add(new KeyValuePair<string, object>("GENRE", cd.genre));
                tr.tag.Add(new KeyValuePair<string, object>("DATE", cd.date));

                lock (AlreadyAnalyzedFiles)
                {
                    foreach (CD.Track track in cd.tracks)
                    {
                        AlreadyAnalyzedFiles[track.file_name_CUESheet] = true;
                    }
                }
                if (modifyed || !IsFastMode)
                {
                    lock (ToBeImportTracks)
                    {
                        ToBeImportTracks.Add(tr);
                    }
                }
            }
            return;
        }

        /// <summary>
        /// 単体の音楽ファイルを解析する
        /// </summary>
        /// <param name="file_name">ファイル名</param>
        /// <param name="threadLocalResultQueue">スレッド固有の解析結果</param>
        /// <param name="lastModifySTMT">modifyを取得するプリペアドステートメント</param>
        private void AnalyzeStreamFile(string file_name, List<LuteaAudioTrack> threadLocalResultQueue, SQLite3DB.STMT lastModifySTMT)
        {
            if (AlreadyAnalyzedFiles.ContainsKey(file_name)) return;
            lock (AlreadyAnalyzedFiles)
            {
                AlreadyAnalyzedFiles[file_name] = true;
            }
            if (LastModifyDatetime(lastModifySTMT, file_name) > new System.IO.FileInfo(file_name).LastWriteTimeUtc && IsFastMode)
            {
                return;
            }
            List<KeyValuePair<string, object>> tag = MetaTag.readTagByFilename(file_name, false);
            if (tag == null) return;
            KeyValuePair<string, object> cue = tag.Find((match) => match.Key.ToUpper() == "CUESHEET" ? true : false);
            if (cue.Key != null)
            {
                CD cd = InternalCUE.Read(file_name);
                if (cd == null) return;
                lock (ToBeImportTracks)
                {
                    ToBeImportTracks.AddRange((IEnumerable<LuteaAudioTrack>)cd.tracks);
                }
            }
            else
            {
                LuteaAudioTrack tr = new LuteaAudioTrack();
                tr.file_name = file_name;
                tr.file_size = new System.IO.FileInfo(file_name).Length;
                if (tag.Exists((_) => _.Key == "__X-LUTEA-CHANS__") && tag.Exists((_) => _.Key == "__X-LUTEA-FREQ__") && tag.Exists((_) => _.Key == "__X-LUTEA-DURATION__"))
                {
                    tr.duration = int.Parse(tag.Find((_) => _.Key == "__X-LUTEA-DURATION__").Value.ToString());
                    tr.channels = int.Parse(tag.Find((_) => _.Key == "__X-LUTEA-CHANS__").Value.ToString());
                    tr.freq = int.Parse(tag.Find((_) => _.Key == "__X-LUTEA-FREQ__").Value.ToString());
                }
                else
                {
                    try
                    {
                        using (var strm = new BASS.FileStream(file_name,BASS.Stream.StreamFlag.BASS_STREAM_DECODE))
                        {
                            tr.duration = (int)strm.length;
                            tr.channels = (int)strm.Info.chans;
                            tr.freq = (int)strm.Info.freq;
                        }
                    }
                    catch(Exception ex) {
                        Logger.Error("cannot open file (by BASS)" + file_name);
                        Logger.Debug(ex);
                    }
                }
                tr.tag = tag;
                threadLocalResultQueue.Add(tr);
            }
        }

        /// <summary>
        /// 解析処理のワーカスレッド。解析対象ディレクトリのキューに対するコンシューマ
        /// </summary>
        private void ConsumeToBeAnalyzeQueueProc()
        {
            // BASSをno deviceで使用
            BASS.BASS_SetDevice(0);
            var libraryDB = Controller.GetDBConnection();
            using (var selectModifySTMT = libraryDB.Prepare(SelectModifySTMT))
            {
                while (ToBeAnalyzeDirectories.Count > 0)
                {
                    string directory_name;
                    List<LuteaAudioTrack> threadLocalResults = new List<LuteaAudioTrack>();
                    lock (ToBeAnalyzeDirectories)
                    {
                        if (ToBeAnalyzeDirectories.Count == 0) continue;
                        directory_name = ToBeAnalyzeDirectories.Dequeue();
                        Message(directory_name);
                        Step_read();
                    }

                    // CUEファイルを処理
                    try
                    {
                        var cuefiles = System.IO.Directory.GetFiles(directory_name, "*.CUE", System.IO.SearchOption.TopDirectoryOnly);
                        foreach (var cuefile in cuefiles)
                        {
                            AnalyzeCUE(cuefile, selectModifySTMT);
                        }
                    }
                    catch (System.IO.IOException ex) { Logger.Error(ex.ToString()); }

                    // 全てのファイルを処理
                    try
                    {
                        var allfiles = System.IO.Directory.GetFiles(directory_name, "*.*", System.IO.SearchOption.TopDirectoryOnly).Where((e) => supportedExtensions.Contains(System.IO.Path.GetExtension(e).ToUpper()));
                        foreach (var file in allfiles)
                        {
                            AnalyzeStreamFile(file, threadLocalResults, selectModifySTMT);
                        }
                        lock (ToBeImportTracks)
                        {
                            ToBeImportTracks.AddRange(threadLocalResults);
                        }
                    }
                    catch (System.IO.IOException ex) { Logger.Error(ex.ToString()); }
                }
            }
        }

        /// <summary>
        /// 実行中のワーカースレッドがあれば全て中断する
        /// </summary>
        private void AbortWorkers()
        {
            if (Workers != null)
            {
                foreach (var worker in Workers)
                {
                    worker.Abort();
                }
            }
            Workers = null;
        }

        /// <summary>
        /// ディレクトリのインポートを行う
        /// </summary>
        private void ImportDirectoryThreadProc()
        {
            lock (LOCKOBJ)
            {
                AbortWorkers();
                AlreadyAnalyzedFiles.Clear();

                Message("ディレクトリを検索しています");
                string[] directories = null;
                try
                {
                    directories = System.IO.Directory.GetDirectories(ToBeImportPath, "*", System.IO.SearchOption.AllDirectories);
                }
                catch (Exception e) { Message(e.ToString()); return; }
                SetMaximum_read(directories.Length);

                Workers = new List<Thread>();
                ToBeAnalyzeDirectories = new Queue<string>(directories);
                ToBeAnalyzeDirectories.Enqueue(ToBeImportPath);
                DoAnalysisByWorkerThreads();
                WriteToDB(true);
            }
        }

        /// <summary>
        /// ファイルのインポートを行う
        /// </summary>
        private void ImportMultipleFilesThreadProc()
        {
            var libraryDB = Controller.GetDBConnection();
            using (var selectModifySTMT = libraryDB.Prepare(SelectModifySTMT))
            {
                lock (LOCKOBJ)
                {
                    AbortWorkers();
                    AlreadyAnalyzedFiles.Clear();

                    var filenames = ToBeImportFilenames.Select((_) => _.Trim()).Distinct().ToArray();
                    foreach (var filename in filenames)
                    {
                        if (System.IO.File.Exists(filename))
                        {
                            try
                            {
                                if (System.IO.Path.GetExtension(filename).ToUpper() == ".CUE")
                                {
                                    AnalyzeCUE(filename, selectModifySTMT);
                                }
                                else
                                {
                                    // シングルスレッドで回すのでスレッドローカルのキューは不要（スレッドローカルキューとしてSQLQueを与える）
                                    AnalyzeStreamFile(filename, ToBeImportTracks, selectModifySTMT);
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e);
                            }
                        }
                    }
                    WriteToDB(false);
                }
            }
        }

        /// <summary>
        /// ワーカスレッドでファイルの解析を行う
        /// 全て完了するまでここで待つ
        /// </summary>
        private void DoAnalysisByWorkerThreads()
        {
            for (int i = 0; i < WORKER_THREADS_N; i++)
            {
                Thread th = new Thread(ConsumeToBeAnalyzeQueueProc);
                th.Priority = ThreadPriority.BelowNormal;
                th.IsBackground = true;
                th.Start();
                Workers.Add(th);
            }
            foreach (var th in Workers)
            {
                th.Join();
            }

            Workers = null;
        }

        /// <summary>
        /// LibraryDBへのインポートと後処理を行う
        /// </summary>
        /// <param name="OptimizeDB"></param>
        private void WriteToDB(bool OptimizeDB = true)
        {
            Message("インポート中");
            RunInsertUpdateQuery(OptimizeDB);
            AlreadyAnalyzedFiles.Clear();
            this.AbortWorkers();
            Complete();
        }

        /// <summary>
        /// 現在のLibraryDB向けのINSERT文のプリペアドステートメントを生成する
        /// </summary>
        /// <param name="db">LibraryDB</param>
        /// <returns>INSERT文のプリペアドステートメント</returns>
        private SQLite3DB.STMT GetInsertPreparedStatement(SQLite3DB db)
        {
            string[] cols = GetToBeImportColumn().Select(_ => _.Name).ToArray();
            string insertFormat = "INSERT INTO list ( " + String.Join(",", cols) + ") VALUES(" + String.Join(",", cols.Select(_ => "?").ToArray()) + ");";
            return db.Prepare(insertFormat);
        }

        /// <summary>
        /// 現在のLibraryDB向けのUPDATE文のプリペアドステートメントを生成する
        /// </summary>
        /// <param name="db">LibraryDB</param>
        /// <returns>UPDATE文のプリペアドステートメント</returns>
        private SQLite3DB.STMT GetUpdatePreparedStatement(SQLite3DB db)
        {
            string[] cols = GetToBeImportColumn().Select(_ => _.Name + " = ?").ToArray();
            string updateFormat = "UPDATE list SET " + String.Join(",", cols) + " WHERE file_name = ?1;";
            return db.Prepare(updateFormat);
        }

        /// <summary>
        /// LibraryDBのカラムのうち、インポート時に値を設定する必要があるものを返す
        /// </summary>
        /// <returns></returns>
        private IEnumerable<Column> GetToBeImportColumn()
        {
            return Controller.Columns.Where(_ => !_.OmitOnImport);
        }

        /// <summary>
        /// ライブラリのfile_nameカラムがfile_nameまたはfile_name+" "に一致するもののmodifyの値をDateTime型で返す。
        /// file_nameがライブラリにない場合はDateTime(0)を返す。
        /// </summary>
        /// <param name="lastModifySTMT"></param>
        /// <param name="file_name"></param>
        /// <returns></returns>
        private DateTime LastModifyDatetime(SQLite3DB.STMT lastModifySTMT, string file_name)
        {
            // SQLiteがDBがロックされてたとかで失敗する場合があるのでリトライする
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    lastModifySTMT.Reset();
                    lastModifySTMT.Bind(1, file_name);
                    lastModifySTMT.Bind(2, file_name + " ");
                    var result = lastModifySTMT.EvaluateAll();
                    if (result.Length == 0) return new DateTime(0);
                    return Util.Util.timestamp2DateTime(long.Parse(result[0][0].ToString()));
                }
                catch (SQLite3DB.SQLite3Exception)
                {
                    Thread.Sleep(1);
                }
            }
            return new DateTime(0);
        }

        /// <summary>
        /// 日時っぽい文字列をYYYY/MM/DD形式に正規化する
        /// </summary>
        /// <param name="datestr"></param>
        /// <returns>正規化した日時の文字列またはnull</returns>
        private string RegulateTagDate(string datestr)
        {
            try
            {
                var result2 = regex_date.Match(datestr);
                if (result2.Success)
                {
                    var month = int.Parse(result2.Groups[2].Value);
                    var day = int.Parse(result2.Groups[3].Value);
                    return result2.Groups[1].Value + "/" + (month > 9 ? month.ToString() : "0" + month.ToString()) + "/" + (day > 9 ? day.ToString() : "0" + day.ToString());
                }

                var result1 = regex_year.Match(datestr);
                if (result1.Success)
                {
                    return result1.Groups[1].Value;
                }
            }
            catch { };
            return null;
        }
    }
}
