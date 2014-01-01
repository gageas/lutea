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
        [Flags]
        public enum ImportableTypes
        {
            MP2 = (1 << 0),
            MP3 = (1 << 1),
            MP4 = (1 << 2),
            M4A = (1 << 3),
            M4AiTunes = (1 << 4),
            OGG = (1 << 5),
            WMA = (1 << 6),
            ASF = (1 << 7),
            FLAC = (1 << 8),
            TTA = (1 << 9),
            APE = (1 << 10),
            WV = (1 << 11),
            TAK = (1 << 12),
            CUE = (1 << 13),
        };
        public const ImportableTypes AllImportableTypes
            = ImportableTypes.MP2 | ImportableTypes.MP3 | ImportableTypes.MP4
            | ImportableTypes.M4A | ImportableTypes.M4AiTunes
            | ImportableTypes.OGG | ImportableTypes.WMA | ImportableTypes.ASF
            | ImportableTypes.FLAC | ImportableTypes.TTA | ImportableTypes.APE
            | ImportableTypes.WV | ImportableTypes.TAK | ImportableTypes.CUE;
        private const int WORKER_THREADS_N = 8;
        private const string SelectModifySTMT = "SELECT modify FROM list WHERE file_name = ? OR file_name = ?;";
        private static object LOCKOBJ = new object();
        private Dictionary<ImportableTypes, string> type2ext = new Dictionary<ImportableTypes, string>() { 
            { ImportableTypes.MP2, ".MP2" } ,
            { ImportableTypes.MP3, ".MP3" } ,
            { ImportableTypes.MP4, ".MP4" } ,
            { ImportableTypes.M4A, ".M4A" } ,
            { ImportableTypes.M4AiTunes, ".M4A" } ,
            { ImportableTypes.OGG, ".OGG" } ,
            { ImportableTypes.WMA, ".WMA" } ,
            { ImportableTypes.ASF, ".ASF" } ,
            { ImportableTypes.FLAC, ".FLAC" } ,
            { ImportableTypes.TTA, ".TTA" } ,
            { ImportableTypes.APE, ".APE" } ,
            { ImportableTypes.WV, ".WV" } ,
            { ImportableTypes.TAK, ".TAK" } ,
            { ImportableTypes.CUE, ".CUE" } 
        };
        private static readonly Regex regex_year = new Regex(@"(?<1>\d{4})");
        private static readonly Regex regex_date = new Regex(@"(?<1>\d{4})[\-\/\.](?<2>\d+)[\-\/\.](?<3>\d+)");

        private string ToBeImportPath;
        private IEnumerable<string> ToBeImportFilenames;
        private Thread ImporterThread; // Importerのメインスレッド
        private List<Thread> Workers = new List<Thread>(); // Importerのワーカスレッド
        private Queue<string> ToBeAnalyzeDirectories;
        private List<LuteaAudioTrack> ToBeImportTracks = new List<LuteaAudioTrack>();
        private List<string> AlreadyAnalyzedFiles = new List<string>();
        private bool IsFastMode; // ファイルのタイムスタンプを見て省略するモード
        private ImportableTypes TypesToImport;

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
            TypesToImport = AppCore.TypesToImport;
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
        /// <param name="stmt">BINDするSTMT</param>
        /// <param name="track">トラック情報</param>
        /// <param name="cols">データベースのカラムのリスト</param>
        private void BindTrackInfo(SQLite3DB.STMT stmt, LuteaAudioTrack track, Column[] cols)
        {
            stmt.Reset();
            string extension = (((track.file_ext == "CUE") && (track is CD.Track)) ? ((CD.Track)track).file_ext_CUESheet : track.file_ext).ToUpper();
            var values = cols
                .Select(col =>
                {
                    switch (col.Name)
                    {
                        case LibraryDBColumnTextMinimum.file_name: return track.file_name;
                        case LibraryDBColumnTextMinimum.file_title: return track.file_title;
                        case LibraryDBColumnTextMinimum.file_ext: return extension;
                        case LibraryDBColumnTextMinimum.file_size: return track.file_size.ToString();

                        case LibraryDBColumnTextMinimum.statDuration: return ((int)track.duration).ToString();
                        case LibraryDBColumnTextMinimum.statChannels: return track.channels.ToString();
                        case LibraryDBColumnTextMinimum.statSamplingrate: return track.freq.ToString();
                        case LibraryDBColumnTextMinimum.statBitrate: return track.bitrate.ToString();
                        case LibraryDBColumnTextMinimum.statVBR: return "0";

                        case LibraryDBColumnTextMinimum.infoCodec: return track.codec.ToString();
                        case LibraryDBColumnTextMinimum.infoCodec_sub: return extension;
                        case LibraryDBColumnTextMinimum.infoTagtype: return "0";

                        case LibraryDBColumnTextMinimum.gain: return "0";
                        case LibraryDBColumnTextMinimum.modify: return MusicLibrary.currentTimestamp.ToString();
                        default:
                            if (!track.tag.Exists((e) => e.Key == col.MappedTagField)) return "";
                            var tagValue = track.tag.First((e) => e.Key == col.MappedTagField).Value.ToString();
                            // DATEの表現形式を正規化して格納する
                            return col.MappedTagField == "DATE"
                                ? normalizeDateString(tagValue) ?? tagValue
                                : tagValue;
                    }
                });
            for (int i = 0; i < cols.Length; i++)
            {
                stmt.Bind(i + 1, values.ElementAt(i));
            }
        }

        /// <summary>
        /// SQLQueのLibraryDBへのインポート処理を行う
        /// </summary>
        /// <param name="OptimizeDB">完了後にデータベースの最適化を実施するかどうか</param>
        private void RunInsertUpdateQuery(bool OptimizeDB)
        {
            using (var libraryDB = AppCore.Library.Connect(true))
            using (var stmt_insert = GetInsertPreparedStatement(libraryDB))
            using (var stmt_update = GetUpdatePreparedStatement(libraryDB))
            using (var stmt_test = libraryDB.Prepare("SELECT rowid FROM list WHERE file_name = ?;"))
            {
                SetMaximum_import(ToBeImportTracks.Count);
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
                var colsToImport = GetToBeImportColumn().ToArray();
                lock (ToBeImportTracks)
                {
                    ToBeImportTracks
                        .Where(_ => _.duration != 0)
                        .OrderBy(_ => "" + _.getTagValue("ALBUM") + (("" + _.getTagValue("TRACK")).PadLeft(5, '0')))
                        .ToList()
                        .ForEach(track =>
                        {
                            Step_import();
                            // Titleが無かったらfile_titleを付与
                            if (!track.tag.Exists(_ => _.Key == "TITLE")) track.tag.Add(new KeyValuePair<string, object>("TITLE", track.file_title));
                            try
                            {
                                // データベースに既に存在しているトラックかテスト
                                stmt_test.Reset();
                                stmt_test.Bind(1, track.file_name);
                                var stmtToUse = stmt_test.EvaluateAll().Length > 0 ? stmt_update : stmt_insert;
                                stmtToUse.Reset();
                                BindTrackInfo(stmtToUse, track, colsToImport);
                                stmtToUse.Evaluate(null);
                            }
                            catch (SQLite3DB.SQLite3Exception e)
                            {
                                Logger.Error(e);
                            }
                        });
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
            }
            Logger.Log("library.dbへのインポートが完了しました");
            AppCore.DatabaseUpdated();
        }

        /// <summary>
        /// CUEシートを解析する
        /// </summary>
        /// <param name="file_name">ファイル名</param>
        /// <param name="lastModifySTMT">modifyを取得するプリペアドステートメント</param>
        private void AnalyzeCUE(string file_name, List<LuteaAudioTrack> threadLocalResults, SQLite3DB.STMT lastModifySTMT)
        {
            // 既に処理済みの場合はreturn
            if (AlreadyAnalyzedFiles.Contains(file_name)) return;

            // 処理済みファイルに追加
            lock (AlreadyAnalyzedFiles)
            {
                AlreadyAnalyzedFiles.Add(file_name);
            }

            // ファイルを解析
            var cd = CUEReader.ReadFromFile(file_name, true);
            if (cd == null) return;
            
            string lastCheckedFilename = null;
            bool lastCheckedFileShouldSkip = false;
            bool modifyed = LastModifyDatetime(lastModifySTMT, file_name) <= new System.IO.FileInfo(file_name).LastWriteTime;
            foreach (CD.Track tr in cd.tracks)
            {
                if (tr.file_name_CUESheet == "") continue;

                // 実体ストリームが存在しない、またはCUESHEETが埋め込まれているなら、.cueファイルとしてのインポートはスキップする。
                if (lastCheckedFilename != tr.file_name_CUESheet)
                {
                    lastCheckedFilename = tr.file_name_CUESheet;
                    lastCheckedFileShouldSkip = false;

                    if (!System.IO.File.Exists(tr.file_name_CUESheet))
                    {
                        lastCheckedFileShouldSkip = true;
                    }

                    // CUEシートが埋め込まれているならスキップ
                    var tagInRealStream = MetaTag.readTagByFilename(tr.file_name_CUESheet, false);
                    if (tagInRealStream != null && (tagInRealStream.Exists(e => e.Key == "CUESHEET")))
                    {
                        lastCheckedFileShouldSkip = true;
                    }
                }
                // ストリームのタグにCUESHEETがある時はなにもしない
                if (lastCheckedFileShouldSkip) continue;

                lock (AlreadyAnalyzedFiles)
                {
                    AlreadyAnalyzedFiles.AddRange(cd.tracks.Select(_ => _.file_name_CUESheet));
                }
                if (modifyed || !IsFastMode)
                {
                    threadLocalResults.Add(tr);
                }
            }
        }

        /// <summary>
        /// 単体の音楽ファイルを解析する
        /// </summary>
        /// <param name="file_name">ファイル名</param>
        /// <param name="threadLocalResultQueue">スレッド固有の解析結果</param>
        /// <param name="lastModifySTMT">modifyを取得するプリペアドステートメント</param>
        private void AnalyzeStreamFile(string file_name, List<LuteaAudioTrack> threadLocalResultQueue, SQLite3DB.STMT lastModifySTMT)
        {
            // 既に処理済みの場合はreturn
            if (AlreadyAnalyzedFiles.Contains(file_name)) return;

            // 処理済みファイルに追加
            lock (AlreadyAnalyzedFiles)
            {
                AlreadyAnalyzedFiles.Add(file_name);
            }
            if (LastModifyDatetime(lastModifySTMT, file_name) > new System.IO.FileInfo(file_name).LastWriteTime && IsFastMode) return;
            var tag = MetaTag.readTagByFilename(file_name, false);
            if (tag == null) return;
            var cue = tag.Find(match => match.Key.ToUpper() == "CUESHEET");
            if (cue.Key != null)
            {
                var cd = InternalCUEReader.Read(file_name, true);
                if (cd == null)
                {
                    Logger.Error("CUESHEET is embedded. But, it has error. " + file_name);
                    return;
                }
                threadLocalResultQueue.AddRange(cd.tracks.Cast<LuteaAudioTrack>());
            }
            else
            {
                var tr = new LuteaAudioTrack() { file_name = file_name, file_size = new System.IO.FileInfo(file_name).Length };
                if (tag.Exists(_ => _.Key == "__X-LUTEA-CHANS__") && tag.Exists(_ => _.Key == "__X-LUTEA-FREQ__") && tag.Exists(_ => _.Key == "__X-LUTEA-DURATION__"))
                {
                    tr.duration = int.Parse(tag.Find(_ => _.Key == "__X-LUTEA-DURATION__").Value.ToString());
                    tr.channels = int.Parse(tag.Find(_ => _.Key == "__X-LUTEA-CHANS__").Value.ToString());
                    tr.freq = int.Parse(tag.Find(_ => _.Key == "__X-LUTEA-FREQ__").Value.ToString());
                }
                else
                {
                    try
                    {
                        using (var strm = new BASS.FileStream(file_name, BASS.Stream.StreamFlag.BASS_STREAM_DECODE))
                        {
                            tr.duration = (int)strm.length;
                            tr.channels = (int)strm.Info.Chans;
                            tr.freq = (int)strm.Info.Freq;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("cannot open file (by BASS)" + file_name);
                        Logger.Debug(ex);
                    }
                }
                tr.tag = tag;
                if (tr.file_ext == "M4A")
                {
                    if (tag.Exists(_ => (_.Key == "PURCHASE DATE") || (_.Key == "PURCHASED")))
                    {
                        if ((TypesToImport & ImportableTypes.M4AiTunes) == 0) return;
                    }
                    else
                    {
                        if ((TypesToImport & ImportableTypes.M4A) == 0) return;
                    }
                }
                threadLocalResultQueue.Add(tr);
            }
        }

        /// <summary>
        /// CUEとCUE以外を順に解析を行う
        /// </summary>
        /// <param name="filenameOfCUEs"></param>
        /// <param name="filenameOfOthers"></param>
        /// <param name="threadLocalResults"></param>
        /// <param name="selectModifySTMT"></param>
        private void DoAnalyze(IEnumerable<string> filenameOfCUEs, IEnumerable<string> filenameOfOthers, List<LuteaAudioTrack> threadLocalResults, SQLite3DB.STMT selectModifySTMT)
        {
            // CUEファイルを処理
            foreach (var cuefile in filenameOfCUEs)
            {
                try
                {
                    AnalyzeCUE(cuefile, threadLocalResults, selectModifySTMT);
                }
                catch (System.IO.IOException ex) { Logger.Error(ex.ToString()); }
            }

            // 全てのファイルを処理
            foreach (var file in filenameOfOthers)
            {
                try
                {
                    AnalyzeStreamFile(file, threadLocalResults, selectModifySTMT);
                }
                catch (System.IO.IOException ex) { Logger.Error(ex.ToString()); }
            }
        }
        /// <summary>
        /// 解析処理のワーカスレッド。解析対象ディレクトリのキューに対するコンシューマ
        /// </summary>
        private void ConsumeToBeAnalyzeQueueProc()
        {
            // BASSをno deviceで使用
            BASS.BASS_SetDevice(0);
            using (var libraryDB = Controller.GetDBConnection())
            using (var selectModifySTMT = libraryDB.Prepare(SelectModifySTMT))
            {
                var threadLocalResults = new List<LuteaAudioTrack>();
                while (ToBeAnalyzeDirectories.Count > 0)
                {
                    string directory_name;
                    lock (ToBeAnalyzeDirectories)
                    {
                        if (ToBeAnalyzeDirectories.Count == 0) continue;
                        directory_name = ToBeAnalyzeDirectories.Dequeue();
                    }
                    Message(directory_name);
                    Step_read();

                    var cuefiles = System.IO.Directory.GetFiles(directory_name, "*.CUE", System.IO.SearchOption.TopDirectoryOnly);
                    var otherfiles = System.IO.Directory.GetFiles(directory_name, "*.*", System.IO.SearchOption.TopDirectoryOnly)
                        .Where(e => type2ext.Where(_=>TypesToImport.HasFlag(_.Key)).Select(_=>_.Value).Distinct().Contains(System.IO.Path.GetExtension(e).ToUpper()));
                    DoAnalyze(cuefiles, otherfiles, threadLocalResults, selectModifySTMT);
                }
                lock (ToBeImportTracks)
                {
                    ToBeImportTracks.AddRange(threadLocalResults);
                }
            }
        }

        /// <summary>
        /// 実行中のワーカースレッドがあれば全て中断する
        /// </summary>
        private void AbortWorkers()
        {
            if (Workers.Count == 0) return;
            foreach (var worker in Workers)
            {
                worker.Abort();
            }
            Workers.Clear();
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
                try
                {
                    var directories = System.IO.Directory.GetDirectories(ToBeImportPath, "*", System.IO.SearchOption.AllDirectories);
                    SetMaximum_read(directories.Length);
                    ToBeAnalyzeDirectories = new Queue<string>(directories);
                    ToBeAnalyzeDirectories.Enqueue(ToBeImportPath);
                    DoAnalysisByWorkerThreads();
                    WriteToDB(true);
                }
                catch (Exception e)
                {
                    Message(e.ToString());
                }
            }
        }

        /// <summary>
        /// ファイルのインポートを行う
        /// </summary>
        private void ImportMultipleFilesThreadProc()
        {
            using (var libraryDB = Controller.GetDBConnection())
            using (var selectModifySTMT = libraryDB.Prepare(SelectModifySTMT))
            {
                lock (LOCKOBJ)
                {
                    AbortWorkers();
                    AlreadyAnalyzedFiles.Clear();
                    var filenames = ToBeImportFilenames
                        .Select(_ => _.Trim())
                        .Distinct()
                        .Where(_ => System.IO.File.Exists(_));
                    IEnumerable<string> filenameOfCUEs = new List<string>();
                    if (TypesToImport.HasFlag(ImportableTypes.CUE))
                    {
                        filenameOfCUEs = filenames.Where(_ => System.IO.Path.GetExtension(_).ToUpper() == ".CUE");
                    }
                    var filenameOthers = filenames.Except(filenameOfCUEs);
                    // シングルスレッドで回すのでスレッドローカルのキューは不要（スレッドローカルキューとしてToBeImportTracksを与える）
                    DoAnalyze(filenameOfCUEs, filenameOthers, ToBeImportTracks, selectModifySTMT);
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

            Workers.Clear();
        }

        /// <summary>
        /// LibraryDBへのインポートと後処理を行う
        /// </summary>
        /// <param name="OptimizeDB"></param>
        private void WriteToDB(bool OptimizeDB = true)
        {
            AlreadyAnalyzedFiles.Clear();
            AbortWorkers();
            if (ToBeImportTracks.Count == 0)
            {
                Message("インポートするファイルがありません");
            }
            else
            {
                Message("インポート中");
                RunInsertUpdateQuery(OptimizeDB);
            }
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
        private string normalizeDateString(string datestr)
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
