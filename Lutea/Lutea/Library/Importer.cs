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
        private static object lockobj = new object();
        private readonly string[] supportedExtensions = new string[] { ".MP3", ".MP2", ".M4A", ".MP4", ".TAK", ".FLAC", ".TTA", ".OGG", ".APE", ".WV", ".WMA", ".ASF"};

        string importPath;
        Thread th;
        Queue<LuteaAudioTrack> SQLQue = new Queue<LuteaAudioTrack>();

        public event Controller.VOIDINT SetMaximum_read = new Controller.VOIDINT((i) => { });
        public event Controller.VOIDVOID Step_read = new Controller.VOIDVOID(() => { });
        public event Controller.VOIDINT SetMaximum_import = new Controller.VOIDINT((i) => { });
        public event Controller.VOIDVOID Step_import = new Controller.VOIDVOID(() => { });
        public event Controller.VOIDVOID Complete = new Controller.VOIDVOID(() => { });
        public delegate void Message_event(string msg);
        public event Message_event Message = new Message_event((s) => { });

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">インポート処理の検索対象</param>
        public Importer(string path)
        {
            this.importPath = path.Trim();

            th = new Thread(importThreadProc);
            th.Priority = ThreadPriority.BelowNormal;
            th.IsBackground = true;
        }

        /// <summary>
        /// インポート処理を開始
        /// </summary>
        public void Start() {
            th.Start();
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
        private SQLite3DB.STMT prepareInsert(SQLite3DB db)
        {
            string[] cols = GetToBeImportColumn().Select(_ => _.Name).ToArray();
            string insertFormat = "INSERT INTO list ( " + String.Join(",", cols) + ") VALUES(" + String.Join(",", cols.Select(_=>"?").ToArray()) + ");";
            return db.Prepare(insertFormat);
        }

        private SQLite3DB.STMT prepareUpdate(SQLite3DB db)
        {
            string[] cols = GetToBeImportColumn().Select(_ => _.Name + " = ?").ToArray();
            string updateFormat = "UPDATE list SET " + String.Join(",", cols) + " WHERE file_name = ?1;";
            return db.Prepare(updateFormat);
        }

        private IEnumerable<Column> GetToBeImportColumn()
        {
            return Controller.Columns.Where(_ => !_.OmitOnImport);
        }

        private void runImport() // import thread
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
            LuteaAudioTrack currentCD;

            SetMaximum_import(SQLQue.Count);

            lock (SQLQue)
            {
                using (var stmt_insert = prepareInsert(libraryDB))
                using (var stmt_update = prepareUpdate(libraryDB))
                using (var stmt_test = libraryDB.Prepare("SELECT rowid FROM list WHERE file_name = ?;"))
                {
                    while (SQLQue.Count > 0)
                    {
                        currentCD = SQLQue.Dequeue();
                        Step_import();

                        if (currentCD.duration == 0) continue;
                        // Titleが無かったらfile_titleを付与
                        if (currentCD.tag.Find((e) => e.Key == "TITLE").Key == null) currentCD.tag.Add(new KeyValuePair<string, object>("TITLE", currentCD.file_title));
                        try
                        {
                            stmt_test.Reset();
                            stmt_test.Bind(1, currentCD.file_name);
                            if (stmt_test.EvaluateAll().Length > 0)
                            {
                                stmt_update.Reset();
                                BindTrackInfo(stmt_update, currentCD);
                                stmt_update.Evaluate(null);
                            }
                            else
                            {
                                stmt_insert.Reset();
                                BindTrackInfo(stmt_insert, currentCD);
                                stmt_insert.Evaluate(null);
                            }
                        }
                        catch (SQLite3DB.SQLite3Exception e)
                        {
                            Logger.Error(e.ToString());
                        }
                    }
                }
                try
                {
                    Message("ライブラリを最適化しています");
                    libraryDB.Exec("COMMIT;");
                    libraryDB.Exec("VACUUM;");
                    libraryDB.Exec("REINDEX;");
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

        private void importFileReadThreadProc_CUE(string file_name)
        {
            if (processedFile.ContainsKey(file_name)) return;
            lock (processedFile)
            {
                processedFile[file_name] = true;
            }
            CD cd = CUEparser.fromFile(file_name, true);
            if (cd == null) return;

            string lastCheckedFilename = null;
            bool   lastCheckedFileShouldSkip = false;
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
                lock (SQLQue)
                {
                    SQLQue.Enqueue(tr);
                }
                lock (processedFile)
                {
                    foreach (CD.Track track in cd.tracks)
                    {
                        processedFile[track.file_name_CUESheet] = true;
                    }
                }
            }
            return;
        }

        private void importFileReadThreadProc_Stream(string file_name, Queue<LuteaAudioTrack> localQueue)
        {
            if (processedFile.ContainsKey(file_name)) return;
            lock (processedFile)
            {
                processedFile[file_name] = true;
            }
            List<KeyValuePair<string, object>> tag = MetaTag.readTagByFilename(file_name, false);
            if (tag == null) return;
            KeyValuePair<string, object> cue = tag.Find((match) => match.Key.ToUpper() == "CUESHEET" ? true : false);
            if (cue.Key != null)
            {
                CD cd = InternalCUE.Read(file_name);
                if (cd == null) return;
                lock (SQLQue)
                {
                    foreach (CD.Track tr in cd.tracks)
                    {
                        SQLQue.Enqueue(tr);
                    }
                }
            }
            else
            {
                LuteaAudioTrack tr = new LuteaAudioTrack();
                tr.file_name = file_name;
                tr.file_size = new System.IO.FileInfo(file_name).Length;
                try
                {
                    using (var strm = new BASS.FileStream(file_name,BASS.Stream.StreamFlag.BASS_STREAM_DECODE))
                    {
                        tr.duration = (int)strm.length;
                        tr.channels = (int)strm.Info.chans;
                        tr.freq = (int)strm.Info.freq;
                    }
                }
                catch { }
                tr.tag = tag;
                localQueue.Enqueue(tr);
            }
        }

        private Queue<string> importFilenameQueue;
        private void importFileReadThreadProc()
        {
            // BASSをno deviceで使用
            BASS.BASS_SetDevice(0);
            while (importFilenameQueue.Count > 0)
            {
                string directory_name;
                Queue<LuteaAudioTrack> localQueue = new Queue<LuteaAudioTrack>();
                lock (importFilenameQueue)
                {
                    if (importFilenameQueue.Count == 0) continue;
                    directory_name = importFilenameQueue.Dequeue();
                    Message(directory_name);
                    Step_read();
                }

                // CUEファイルを処理
                try
                {
                    var cuefiles = System.IO.Directory.GetFiles(directory_name, "*.CUE", System.IO.SearchOption.TopDirectoryOnly);
                    foreach (var cuefile in cuefiles)
                    {
                        importFileReadThreadProc_CUE(cuefile);
                    }
                }
                catch (System.IO.IOException ex) { Logger.Error(ex.ToString()); }

                // 全てのファイルを処理
                try
                {
                    var allfiles = System.IO.Directory.GetFiles(directory_name, "*.*", System.IO.SearchOption.TopDirectoryOnly).Where((e) => supportedExtensions.Contains(System.IO.Path.GetExtension(e).ToUpper()));
                    foreach (var file in allfiles)
                    {
                        importFileReadThreadProc_Stream(file, localQueue);
                    }
                    lock (SQLQue)
                    {
                        var sortedQueue = localQueue.ToList();
                        sortedQueue.Sort((x, y) => x.file_name.CompareTo(y.file_name));
                        foreach (var e in sortedQueue)
                        {
                            SQLQue.Enqueue(e);
                        }
                    }
                }
                catch (System.IO.IOException ex) { Logger.Error(ex.ToString()); }
            }
        }

        public void Abort()
        {
            th.Abort();
            AbortWorkers();
        }

        public void AbortWorkers()
        {
            if (workers != null)
            {
                foreach (var worker in workers)
                {
                    worker.Abort();
                }
            }
        }

        private Dictionary<string, bool> processedFile = new Dictionary<string, bool>();
        List<Thread> workers;
        private void importThreadProc()
        {
            int N = 8;
            lock (lockobj)
            {
                if (workers != null)
                {
                    foreach (var worker in workers)
                    {
                        worker.Abort();
                    }
                }

                processedFile.Clear();

                if (System.IO.File.Exists(importPath))
                {
                    if (System.IO.Path.GetExtension(importPath).ToUpper() == ".CUE")
                    {
                        importFileReadThreadProc_CUE(importPath);
                    }
                    else
                    {
                        var localQueue = new Queue<LuteaAudioTrack>();
                        importFileReadThreadProc_Stream(importPath, localQueue);
                        while (localQueue.Count() > 0)
                        {
                            SQLQue.Enqueue(localQueue.Dequeue());
                        }
                    }
                }
                else
                {

                    Message("ディレクトリを検索しています");
                    string[] directories = null;
                    try
                    {
                        directories = System.IO.Directory.GetDirectories(importPath, "*", System.IO.SearchOption.AllDirectories);
                    }
                    catch (Exception e) { Message(e.ToString()); return; }
                    SetMaximum_read(directories.Length);

                    workers = new List<Thread>();
                    importFilenameQueue = new Queue<string>(directories);
                    importFilenameQueue.Enqueue(importPath);
                    for (int i = 0; i < N; i++)
                    {
                        Thread th = new Thread(importFileReadThreadProc);
                        th.Priority = ThreadPriority.BelowNormal;
                        th.IsBackground = true;
                        th.Start();
                        workers.Add(th);
                    }
                    foreach (var th in workers)
                    {
                        th.Join();
                    }
                }

                workers = null;

                Message("インポート中");
                runImport();
                processedFile.Clear();
                this.AbortWorkers();
                Complete();
            }
        }

        private static readonly Regex regex_year = new Regex(@"(?<1>\d{4})");
        private static readonly Regex regex_date = new Regex(@"(?<1>\d{4})[\-\/\.](?<2>\d+?)[\-\/\.](?<3>\d+?)");
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
