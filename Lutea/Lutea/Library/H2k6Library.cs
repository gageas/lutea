using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Gageas.Wrapper.SQLite3;
using Gageas.Lutea.Util;
using Gageas.Lutea.Library;
using KaoriYa.Migemo;

namespace Gageas.Lutea
{
    public enum H2k6Codec { NA, MP1, MP2, MP3, OGG, WAV, WMA, AAC, MP4, ALAC };
    class LuteaAudioTrack
    {
        public int channels;
        public int freq;
        public object getTagValue(string key)
        {
            var frame = tag.Find((e) => e.Key == key);
            return frame.Value;
        }
        public LuteaAudioTrack()
        {
            this.tag = new List<KeyValuePair<string, object>>();
        }
        public string file_title
        {
            get
            {
                if (file_name == "") return "";
                return System.IO.Path.GetFileNameWithoutExtension(file_name);
            }
        }
        public string file_ext
        {
            get
            {
                return System.IO.Path.GetExtension(file_name).Trim().Substring(1).ToUpper();
            }
        }
        private string _file_name = "";
        public string file_name
        {
            get { return _file_name; }
            set
            {
                _file_name = value;
            }
        }
        private long _file_size = 0;
        public long file_size
        {
            set
            {
                _file_size = value;
            }
            get
            {
                return _file_size;
            }
        }

        public int rating = 0;
        public int playcount = 0;
        public long lastplayed = 0;
        public long modify;
        private int _bitrate = 0;
        public int bitrate
        {
            set
            {
                _bitrate = value;
            }
            get
            {
                if (_bitrate == 0)
                {
                    long bits = file_size * 8;
                    return (int)(bits / duration);
                }
                else
                {
                    return _bitrate;
                }
            }
        }
        public double duration = 0;
        public H2k6Codec codec
        {
            get
            {
                switch (file_ext.ToUpper())
                {
                    case "MP3":
                        return H2k6Codec.MP3;
                    case "AAC":
                        return H2k6Codec.AAC;
                    case "M4A":
                    case "MP4":
                        if (this.bitrate > 350)
                        { // たぶん320だけど、若干余裕もたす
                            return H2k6Codec.ALAC;
                        }
                        else
                        {
                            return H2k6Codec.AAC; // このへんは微妙なのでAACにしてしまう
                        }
                    case "OGG":
                        return H2k6Codec.OGG;
                    case "WAV":
                        return H2k6Codec.WAV;
                    case "WMA":
                    case "ASF":
                        return H2k6Codec.WMA;
                    case "FLAC":
                        //                    case "FLA":
                        return H2k6Codec.WAV;
                    case "APE":
                    case "WV":
                    case "TTA":
                    case "TAK":
                        return H2k6Codec.WAV;
                }
                return H2k6Codec.NA;
            }
        }
        public List<KeyValuePair<string, object>> tag;
    }

    public class MusicLibrary
    {
        public static String GetCreateSchema(Column[] columns)
        {
            return "CREATE TABLE IF NOT EXISTS list(" + String.Join(" , ", columns.Select(_ =>
            {
                switch (_.Type)
                {
                    case LibraryColumnType.FileName:
                        return _.Name + " TEXT UNIQUE";
                    case LibraryColumnType.Integer:
                    case LibraryColumnType.Bitrate:
                    case LibraryColumnType.Rating:
                    case LibraryColumnType.Time:
                    case LibraryColumnType.FileSize:
                    case LibraryColumnType.Timestamp64:
                        return _.Name + " INTEGER DEFAULT 0";
                    case LibraryColumnType.Text:
                    case LibraryColumnType.TrackNumber:
                    default:
                        return _.Name + " TEXT";
                }
            }).ToArray()) +
            " , PRIMARY KEY(" + String.Join(",", columns.Where(_ => _.PrimaryKey).Select(_ => _.Name).ToArray()) + "));";
        }

        public static String GetCreateLibraryDefinitionSchema()
        {
            return "CREATE TABLE IF NOT EXISTS library_definition( column_name TEXT UNIQUE, localized_name TEXT, type INTEGER, is_primary INTEGER, mapped_tag_field TEXT, is_text_search_target TEXT, omit_on_import INTEGER);";
        }

        public static String GetCreateIndexSchema(Column[] columns)
        {
            return String.Join(" ", columns.Where(_ => _.Name == LibraryDBColumnTextMinimum.rating || _.IsTextSearchTarget).Select(_ => "CREATE INDEX " + _.Name + "_index ON list(" + _.Name + ");").ToArray());
        }

        private static readonly Column[] H2k6CompatColumns = 
        {
           new Column(type:LibraryColumnType.FileName, Name:"file_name", LocalText:"ファイルパス", IsPrimaryKey:true),
           new Column(type:LibraryColumnType.Text, Name:"file_title", LocalText:"ファイル名", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, Name:"file_ext", LocalText:"拡張子", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"file_size", LocalText:"ファイルサイズ", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, Name:"tagTitle", LocalText:"タイトル", IsPrimaryKey:false,IsTextSearchTarget:true, MappedTagField:"TITLE"),
           new Column(type:LibraryColumnType.Text, Name:"tagArtist", LocalText:"アーティスト", IsPrimaryKey:false,IsTextSearchTarget:true, MappedTagField:"ARTIST"),
           new Column(type:LibraryColumnType.Text, Name:"tagAlbum", LocalText:"アルバム", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"ALBUM"),
           new Column(type:LibraryColumnType.Text, Name:"tagGenre", LocalText:"ジャンル", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"GENRE"),
           new Column(type:LibraryColumnType.Text, Name:"tagDate", LocalText:"年", IsPrimaryKey:false, MappedTagField:"DATE"),
           new Column(type:LibraryColumnType.Text, Name:"tagComment", LocalText:"コメント", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"COMMENT"),
           new Column(type:LibraryColumnType.TrackNumber, Name:"tagTracknumber", LocalText:"No", IsPrimaryKey:false, MappedTagField:"TRACK"),
           new Column(type:LibraryColumnType.Text, Name:"tagAPIC", LocalText:"カバーアート(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, Name:"tagLyrics", LocalText:"歌詞(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Time, Name:"statDuration", LocalText:"長さ", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"statChannels", LocalText:"チャンネル", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"statSamplingrate", LocalText:"サンプリング周波数", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Bitrate, Name:"statBitrate", LocalText:"ビットレート", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"statVBR", LocalText:"VBRフラグ(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"infoCodec", LocalText:"コーデック", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, Name:"infoCodec_sub", LocalText:"コーデック2", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"infoTagtype", LocalText:"タグ形式(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"gain", LocalText:"ゲイン(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Rating, Name:"rating", LocalText:"評価", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Integer, Name:"playcount", LocalText:"再生回数", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Timestamp64, Name:"lastplayed", LocalText:"最終再生日", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Timestamp64, Name:"modify", LocalText:"最終更新日", IsPrimaryKey:false),
        };

        private static readonly Column[] LuteaMinimumColumns = 
        {
           new Column(type:LibraryColumnType.FileName, Name:"file_name", LocalText:"ファイルパス", IsPrimaryKey:true),
           new Column(type:LibraryColumnType.Text, Name:"file_title", LocalText:"ファイル名", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, Name:"file_ext", LocalText:"拡張子", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"file_size", LocalText:"ファイルサイズ", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Time, Name:"statDuration", LocalText:"長さ", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"statChannels", LocalText:"チャンネル", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, Name:"statSamplingrate", LocalText:"サンプリング周波数", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Bitrate, Name:"statBitrate", LocalText:"ビットレート", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, Name:"infoCodec_sub", LocalText:"コーデック", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Rating, Name:"rating", LocalText:"評価", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Integer, Name:"playcount", LocalText:"再生回数", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Timestamp64, Name:"lastplayed", LocalText:"最終再生日", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Timestamp64, Name:"modify", LocalText:"最終更新日", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, Name:"tagTitle", LocalText:"タイトル", IsPrimaryKey:false,IsTextSearchTarget:true, MappedTagField:"TITLE"),
           new Column(type:LibraryColumnType.Text, Name:"tagArtist", LocalText:"アーティスト", IsPrimaryKey:false,IsTextSearchTarget:true, MappedTagField:"ARTIST"),
           new Column(type:LibraryColumnType.Text, Name:"tagAlbum", LocalText:"アルバム", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"ALBUM"),
           new Column(type:LibraryColumnType.Text, Name:"tagGenre", LocalText:"ジャンル", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"GENRE"),
           new Column(type:LibraryColumnType.Text, Name:"tagDate", LocalText:"年", IsPrimaryKey:false, MappedTagField:"DATE"),
           new Column(type:LibraryColumnType.TrackNumber, Name:"tagTracknumber", LocalText:"No", IsPrimaryKey:false, MappedTagField:"TRACK"),
        };

        private static readonly Column[] LuteaDefaultExtraColumns = 
        {
           new Column(type:LibraryColumnType.Text, Name:"tagComment", LocalText:"コメント", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"COMMENT"),
           new Column(type:LibraryColumnType.Text, Name:"tagAlbumArtist", LocalText:"アルバムアーティスト", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"ALBUM ARTIST"),
           new Column(type:LibraryColumnType.Text, Name:"tagPerformer", LocalText:"演奏者", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"PERFORMER"),
        };

        internal Column[] GetExtraColumns()
        {
            if (Columns == H2k6CompatColumns)
            {
                return null;
            }

            // FIXME:なんかExpectがうまくうごかないので
            return Columns.Where(_ => { return !LuteaMinimumColumns.Select((e) => e.Name).Contains(_.Name); }).ToArray();
//            return Columns.Except(LuteaMinimumColumns).ToArray();
        }

        public readonly Column[] Columns = null;

        class KVPool<K,V>{
            public delegate V ValueGenerator(K src);

            private Dictionary<K,V> pool = new Dictionary<K,V>();
            private int poolLimit;
            private ValueGenerator valueGenerator;

            public KVPool(ValueGenerator valueGenerator,int poolLimit = 16) // TODO: poolが一定数を超えたら全クリアだが、ちゃんと古いものから消すとかするといいかもね
            {
                this.poolLimit = poolLimit;
                this.valueGenerator = valueGenerator;
            }
            public V Get(K src){
                V value = default(V);
                if (pool.ContainsKey(src))
                {
                    value = pool[src];
                }
                else
                {
                    try
                    {
                        value = valueGenerator(src);
                    }
                    catch
                    {
                    }
                    if (pool.Count > poolLimit) pool.Clear();
                    pool[src] = value;
                }
                return value;
            }
        }
        private static System.DateTime UnixEpoch = new System.DateTime(1970, 1, 1, 0, 0, 0);
        public bool MigemoAvailable
        {
            get
            {
                return !(migemo == null);
            }
        }
        public static Int64 currentTimestamp
        {
            get
            {
                return ((System.DateTime.Now.ToFileTime() - UnixEpoch.ToFileTime()) / 10000000);
            }
        }
        public static DateTime timestamp2DateTime(Int64 timestamp)
        {
            return DateTime.FromFileTime((timestamp * 10000000 + UnixEpoch.ToFileTime()));
        }

        private string dbPath;
        public MusicLibrary(string dbPath)
        {
            try
            {
                migemo = new Migemo(@"dict\migemo-dict");
                migemoRePool = new KVPool<string, Regex>((src) => migemo.GetRegex(src, RegexOptions.IgnoreCase | RegexOptions.Multiline));
            }
            catch { }
            this.dbPath = dbPath;

            if (!File.Exists(dbPath))
            {
                try
                {
                    using (var db = this.Connect(false))
                    {
                        Columns = LuteaMinimumColumns.Concat(LuteaDefaultExtraColumns).ToArray();
                        InitializeLibraryDB(db, Columns);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e.ToString());
                }
            }else{
                try
                {
                    Columns = LoadColumnDefinitionFromDB();
                }catch(Exception e){
                    Logger.Error(e.ToString());
                };

                if (Columns == null)
                {
                    Columns = H2k6CompatColumns;
                }
            }
        }

        private void InitializeLibraryDB(SQLite3DB db ,Column[] columns)
        {
            db.Exec(GetCreateLibraryDefinitionSchema());
            using (var stmt = db.Prepare("INSERT INTO library_definition ( column_name, localized_name, type, is_primary, mapped_tag_field, is_text_search_target, omit_on_import) VALUES(?,?,?,?,?,?,?);"))
            {
                foreach (var col in columns)
                {
                    stmt.Reset();
                    stmt.Bind(1, col.Name);
                    stmt.Bind(2, col.LocalText);
                    stmt.Bind(3, ((int)col.Type).ToString());
                    stmt.Bind(4, col.PrimaryKey ? "1" : "0");
                    stmt.Bind(5, col.MappedTagField == null ? "" : col.MappedTagField);
                    stmt.Bind(6, col.IsTextSearchTarget ? "1" : "0");
                    stmt.Bind(7, col.OmitOnImport ? "1" : "0");
                    stmt.Evaluate(null);
                }
            }
            db.Exec(GetCreateSchema(columns));
            db.Exec(GetCreateIndexSchema(columns));
        }

        private Column[] LoadColumnDefinitionFromDB()
        {
            using (var db = this.Connect(false))
            {
                var colCount = 0;
                using (SQLite3DB.STMT tmt2 = db.Prepare("SELECT COUNT(*) FROM library_definition ;"))
                {
                    tmt2.Evaluate((o) => colCount = int.Parse(o[0].ToString()));
                }
                if (colCount > 0)
                {
                    var coldefs = new object[colCount][];
                    db.FetchRowRange("library_definition", 0, colCount, coldefs);
                    var columns = new List<Column>();
                    foreach (var coldef in coldefs)
                    {
                        columns.Add(new Column(
                            Name: coldef[0].ToString(),
                            LocalText: coldef[1].ToString(),
                            type: (LibraryColumnType)int.Parse(coldef[2].ToString()),
                            IsPrimaryKey: coldef[3].ToString() == "1",
                            MappedTagField: coldef[4].ToString(),
                            IsTextSearchTarget: coldef[5].ToString() == "1",
                            OmitOnImport: coldef[6].ToString() == "1"));
                    }
                    return columns.ToArray();
                }
            }
            return null;
        }

        public SQLite3DB Connect(bool lockable){
            SQLite3DB db = new SQLite3DB(dbPath, lockable);
            db.EnableLoadExtension = true;
            db.Exec("PRAGMA temp_store = MEMORY;");
            db.Exec("PRAGMA encoding = \"UTF-8\"; ");
            db.createFunction("regexp", 2, SQLite3.TextEncoding.SQLITE_UTF16, sqlite_regexp);
            db.createFunction("current_timestamp64", 0, SQLite3.TextEncoding.SQLITE_ANY, (o) => currentTimestamp);
            db.createFunction("LCMapUpper", 1, SQLite3.TextEncoding.SQLITE_UTF16, (o) => o[0] == null ? "" : o[0].ToString().LCMapUpper());
            if (migemo != null)
            {
                db.createFunction("migemo", 2, SQLite3.TextEncoding.SQLITE_UTF16, sqlite_migemo);
            }
            return db;
        }

        public SQLite3DB.STMT PrepareForInsert()
        {
            SQLite3DB db = this.Connect(true);
            return db.Prepare("");
        }

        // SQLiteから使えるmigemo関数の定義
        private Migemo migemo = null;
        KVPool<string,Regex> migemoRePool;
        public object sqlite_migemo(string[] args)
        {
            if (args[0] == null || args[1] == null) return 0;
            if (args[1].LCMapUpper().Contains(args[0].LCMapUpper())) return 1;
            if (migemo == null) return 0;
            var re = migemoRePool.Get(args[0]);
            if (re == null) return 0;
            return re.Match(args[1]).Success ? 1 : 0;
        }


        // SQLiteから使えるregexp関数の定義
        KVPool<string, Regex> regexRePool = new KVPool<string, Regex>((src) => {
            try
            {
                Match match = new Regex(@"^\/(?<1>.+)\/(?<2>[a-z]*)$").Match(src);
                RegexOptions op = RegexOptions.Multiline;
                if (match.Groups[2].Value.IndexOf('i') >= 0)
                {
                    op |= RegexOptions.IgnoreCase;
                }
                return new Regex(match.Groups[1].Value,op);
            }catch(Exception){}
            return null;
        });
        public object sqlite_regexp(string[] args)
        {
            if (args[0] == null || args[1] == null) return 0;
            var re = regexRePool.Get(args[0]);
            if (re == null) return 0;
            return re.Match(args[1]).Success ? 1 : 0;
        }
    }
}
