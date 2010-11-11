using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Gageas.Wrapper.BASS;
using Gageas.Wrapper.SQLite3;
using Gageas.Lutea.Tags;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.Library
{
    public sealed class Importer
    {
        private static object lockobj = new object();
        private readonly string[] supportedExtensions = new string[] { ".MP3", ".MP2", ".M4A", ".MP4", ".TAK", ".FLAC", ".TTA", ".OGG", ".APE", ".WV", ".WMA", ".ASF"};

        string importPath;
        Thread th;
        Queue<H2k6LibraryTrack> SQLQue = new Queue<H2k6LibraryTrack>();

        public event Controller.VOIDINT SetMaximum_read = new Controller.VOIDINT((i) => { });
        public event Controller.VOIDVOID Step_read = new Controller.VOIDVOID(() => { });
        public event Controller.VOIDINT SetMaximum_import = new Controller.VOIDINT((i) => { });
        public event Controller.VOIDVOID Step_import = new Controller.VOIDVOID(() => { });
        public event Controller.VOIDVOID Complete = new Controller.VOIDVOID(() => { });
        public delegate void Message_event(string msg);
        public event Message_event Message = new Message_event((s) => { });

        public Importer(string path)
        {
            this.importPath = path;

            th = new Thread(importThreadProc);
            th.Priority = ThreadPriority.BelowNormal;
            th.IsBackground = true;
        }
        public void Start() {
            th.Start();
        }
        private void BindTrackInfo(SQLite3DB.STMT stmt, H2k6LibraryTrack track)
        {
            stmt.Reset();
            string[] basicColumnValue = { track.file_name, track.file_title, track.file_ext, track.file_size.ToString() };
            for (int i = 0; i < H2k6Library.basicColumn.Length; i++)
            {
                stmt.Bind(i + 1, basicColumnValue[i]);
            }

            for (int i = H2k6Library.basicColumn.Length; i < AppCore.Library.Columns.Length; i++)
            {
                string colstring = AppCore.Library.Columns[i];
                KeyValuePair<string, object> tagEntry = track.tag.Find((e) => { return e.Key == colstring; });
                if (tagEntry.Key != null)
                {
                    stmt.Bind(i + 1, tagEntry.Value.ToString());
                }
                else
                {
                    stmt.Bind(i + 1, "");
                }
            }
            string[] stats = { track.duration.ToString(), track.channels.ToString(), track.freq.ToString(), track.bitrate.ToString(), ((int)track.codec).ToString(), track.file_ext.ToUpper(), H2k6Library.currentTimestamp.ToString() };
            for (int i = AppCore.Library.Columns.Length; i < AppCore.Library.Columns.Length + stats.Length; i++)
            {
                string stat = stats[i - AppCore.Library.Columns.Length];
                if (stat != null)
                {
                    stmt.Bind(i + 1, stat);
                }
                else
                {
                    stmt.Bind(i + 1, "");
                }
            }

        }
        private SQLite3DB.STMT prepareInsert(SQLite3DB db)
        {

            string insertFormat = "INSERT INTO list (" + 
                "file_name,file_title,file_ext,file_size," +
                "tagTitle, tagArtist, tagAlbum, tagGenre, tagDate, tagComment, tagTracknumber, tagAPIC, tagLyrics," +
                "statDuration, statChannels, statSamplingrate, statBitrate, statVBR," +
                "infoCodec, infoCodec_sub, infoTagtype,modify" + 
                ") VALUES(?,?,?,?," + // file_name, file_title, file_ext, file_size,
                "?,?,?,?,?,?,?,'',?," + // tagTitle, tagArtist, tagAlbum, tagGenre, tagDate, tagComment, tagTracknumber, tagAPIC, tagLyrics,
                "?,?,?,?,0," + // statDuration, statChannels, statSamplingrate, statBitrate, statVBR,
                "?,?,32,?);" // infoCodec, infoCodec_sub, infoTagtype,modify
                // , gain, rating, playcount, lastplayed, 
                ;
            return db.Prepare(insertFormat);
        }

        private SQLite3DB.STMT prepareUpdate(SQLite3DB db)
        {
            string updateFormat = "UPDATE list SET file_name = ?, file_title = ?, file_ext = ? , file_size = ?," +
                "tagTitle = ?, tagArtist = ?, tagAlbum = ?, tagGenre = ?, tagDate = ?, tagComment = ?, tagTracknumber = ?, tagLyrics = ?," +
                "statDuration = ?, statChannels = ?, statSamplingrate = ?, statBitrate = ?," +
                "infoCodec = ?, infoCodec_sub = ?, modify = ? WHERE file_name = ?1";
            return db.Prepare(updateFormat);
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
            H2k6LibraryTrack currentCD;

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
            KeyValuePair<string, bool> tagHasCUECache = new KeyValuePair<string, bool>();
            foreach (CD.Track tr in cd.tracks)
            {
                if (tr.file_name_CUESheet == "") continue;

                // 実体ストリームにCUESHEETが埋め込まれているかどうかをキャッシュする
                if (tagHasCUECache.Key != tr.file_name_CUESheet)
                {
                    var tagInRealStream = MetaTag.readTagByFilename(tr.file_name_CUESheet, false);
                    if (tagInRealStream == null)
                    {
                        tagHasCUECache = new KeyValuePair<string, bool>(tr.file_name_CUESheet, false);
                    }
                    else
                    {
                        tagHasCUECache = new KeyValuePair<string, bool>(tr.file_name_CUESheet, tagInRealStream.Find((e) => e.Key == "CUESHEET").Value != null);
                    }
                }
                // ストリームのタグにCUESHEETがある時はなにもしない
                if (tagHasCUECache.Value)
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

        private void importFileReadThreadProc_Stream(string file_name, Queue<H2k6LibraryTrack> localQueue)
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
                H2k6LibraryTrack tr = new H2k6LibraryTrack();
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
            while (importFilenameQueue.Count > 0)
            {
                string directory_name;
                Queue<H2k6LibraryTrack> localQueue = new Queue<H2k6LibraryTrack>();
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
                        foreach (var e in localQueue)
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
                //                this.Invoke((MethodInvoker)(() => { progressBar1.Value = 0; progressBar1.Step = 1; }));
                Message("ディレクトリを検索しています");

                processedFile.Clear();

                var directories = System.IO.Directory.GetDirectories(importPath, "*", System.IO.SearchOption.AllDirectories);
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

                workers = null;

                Message("インポート中");
                runImport();
                processedFile.Clear();
                this.AbortWorkers();
                Complete();
            }
        }
    }
}
