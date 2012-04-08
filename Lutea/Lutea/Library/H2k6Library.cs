using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gageas.Wrapper.SQLite3;
using Gageas.Lutea.Util;
using Gageas.Lutea.Library;
using KaoriYa.Migemo;

namespace Gageas.Lutea
{
    public enum H2k6Codec { NA, MP1, MP2, MP3, OGG, WAV, WMA, AAC, MP4, ALAC };
    class H2k6LibraryTrack
    {
        public int channels;
        public int freq;
        public object getTagValue(string key)
        {
            var frame = tag.Find((e) => e.Key == key);
            return frame.Value;
        }
        public H2k6LibraryTrack()
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

    public class H2k6Library
    {
        public static readonly Column[] Columns = 
        {
           new Column(type:LibraryColumnType.FileName, DBText:"file_name", LocalText:"ファイルパス", IsPrimaryKey:true),
           new Column(type:LibraryColumnType.Text, DBText:"file_title", LocalText:"ファイル名", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, DBText:"file_ext", LocalText:"拡張子", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, DBText:"file_size", LocalText:"ファイルサイズ", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, DBText:"tagTitle", LocalText:"タイトル", IsPrimaryKey:false,IsTextSearchTarget:true, MappedTagField:"TITLE"),
           new Column(type:LibraryColumnType.Text, DBText:"tagArtist", LocalText:"アーティスト", IsPrimaryKey:false,IsTextSearchTarget:true, MappedTagField:"ARTIST"),
           new Column(type:LibraryColumnType.Text, DBText:"tagAlbum", LocalText:"アルバム", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"ALBUM"),
           new Column(type:LibraryColumnType.Text, DBText:"tagGenre", LocalText:"ジャンル", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"GENRE"),
           new Column(type:LibraryColumnType.Text, DBText:"tagDate", LocalText:"年", IsPrimaryKey:false, MappedTagField:"DATE"),
           new Column(type:LibraryColumnType.Text, DBText:"tagComment", LocalText:"コメント", IsPrimaryKey:false, IsTextSearchTarget:true, MappedTagField:"COMMENT"),
           new Column(type:LibraryColumnType.TrackNumber, DBText:"tagTracknumber", LocalText:"No", IsPrimaryKey:false, MappedTagField:"TRACK"),
           new Column(type:LibraryColumnType.Text, DBText:"tagAPIC", LocalText:"カバーアート(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, DBText:"tagLyrics", LocalText:"歌詞(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Time, DBText:"statDuration", LocalText:"長さ", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, DBText:"statChannels", LocalText:"チャンネル", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, DBText:"statSamplingrate", LocalText:"サンプリング周波数", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Bitrate, DBText:"statBitrate", LocalText:"ビットレート", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, DBText:"statVBR", LocalText:"VBRフラグ(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, DBText:"infoCodec", LocalText:"コーデック", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Text, DBText:"infoCodec_sub", LocalText:"コーデック2", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, DBText:"infoTagtype", LocalText:"タグ形式(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Integer, DBText:"gain", LocalText:"ゲイン(未使用)", IsPrimaryKey:false),
           new Column(type:LibraryColumnType.Rating, DBText:"rating", LocalText:"評価", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Integer, DBText:"playcount", LocalText:"再生回数", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Timestamp64, DBText:"lastplayed", LocalText:"最終再生日", IsPrimaryKey:false, OmitOnImport:true),
           new Column(type:LibraryColumnType.Timestamp64, DBText:"modify", LocalText:"最終更新日", IsPrimaryKey:false),
        };

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
        public bool MigemoEnabled
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
        public H2k6Library(ICollection<string> customColumns,string dbPath)
        {
            try
            {
                migemo = new Migemo(@"dict\migemo-dict");
                migemoRePool = new KVPool<string,Regex>((src) => migemo.GetRegex(src,RegexOptions.IgnoreCase|RegexOptions.Multiline));
            }
            catch { }
            this.dbPath = dbPath;
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
