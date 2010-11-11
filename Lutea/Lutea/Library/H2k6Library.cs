using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Gageas.Wrapper.SQLite3;
using Gageas.Lutea.Util;
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
//                return (new Regex(@"\.[^\.]+$")).Replace(this._file_name, "");
                if (file_name == "") return "";
                return System.IO.Path.GetFileNameWithoutExtension(file_name);
            }
        }
        public string file_ext
        {
            get
            {
//                return (new Regex(@"\.(?<1>[^\.]+)$")).Match(this._file_name).Groups[1].Value.Trim();
                if (file_name == "") return "";
                return System.IO.Path.GetExtension(file_name).Trim().Substring(1);
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
        class KVPool<K,V>{
            public delegate V ValueGenerator(K src);

            private Dictionary<K,V> pool = new Dictionary<K,V>();
//            private RegexOptions options;
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
//                        value = new Regex(src, options);
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
        public static string[] basicColumn = { "file_name", "file_title", "file_ext", "file_size" };
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

        public string[] Columns;
        private string dbPath;
        public H2k6Library(ICollection<string> customColumns,string dbPath)
        {
            try
            {
                migemo = new Migemo(@"dict\migemo-dict");
                migemoRePool = new KVPool<string,Regex>((src) => migemo.GetRegex(src,RegexOptions.IgnoreCase|RegexOptions.Multiline));
            }
            catch { }
            Columns = basicColumn.Concat(customColumns).ToArray<string>();
            this.dbPath = dbPath;
        }
        public SQLite3DB Connect(bool lockable){
            SQLite3DB db = new SQLite3DB(dbPath, lockable);
            db.EnableLoadExtension = true;
            db.createFunction("regexp", 2, SQLite3.TextEncoding.SQLITE_UTF16, sqlite_regexp);
            db.createFunction("current_timestamp64", 0, SQLite3.TextEncoding.SQLITE_ANY, (o) => currentTimestamp);
            db.createFunction("LCMapUpper", 1, SQLite3.TextEncoding.SQLITE_UTF16, (o) => o[0].LCMapUpper());
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
            var re = regexRePool.Get(args[0]);
            if (re == null) return 0;
            return re.Match(args[1]).Success ? 1 : 0;
        }
    }
}
