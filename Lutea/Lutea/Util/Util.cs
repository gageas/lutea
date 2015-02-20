using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

using System.Runtime.InteropServices;

namespace Gageas.Lutea.Util
{
    /// <summary>
    /// バイト配列に対するユーティリティ
    /// </summary>
    static class ByteArrayUtil
    {
        /// <summary>
        /// 値を検索し，インデックスを取得
        /// </summary>
        /// <param name="buffer">バイト配列</param>
        /// <param name="offset">検索開始位置</param>
        /// <param name="search">検索するバイト値</param>
        /// <returns>値と一致する位置のインデックス。見つからなければ-1</returns>
        public static int IndexOf(this byte[] buffer, int offset, byte search)
        {
            for (int i = offset; i < buffer.Length; i++)
            {
                if (buffer[i] == search) return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// ユーティリティ
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// 歌詞タイムタグを表す正規表現
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex TimetagPattern = new System.Text.RegularExpressions.Regex(@"(\[[\d-:]+\])|(\\([^_]|(_.)))");

        /// <summary>
        /// 整数を表す正規表現
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex intRe = new System.Text.RegularExpressions.Regex(@"(\+|-)?\d+");

        /// <summary>
        /// 実数を表す正規表現
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex doubleRe = new System.Text.RegularExpressions.Regex(@"(\+|-)?\d+(\.)?(\d+)?");

        /// <summary>
        /// データベースのタイムスタンプから日時(DateTime型)に変換
        /// </summary>
        /// <param name="timestamp">タイムスタンプ</param>
        /// <returns></returns>
        public static DateTime timestamp2DateTime(Int64 timestamp)
        {
            return MusicLibrary.timestamp2DateTime(timestamp);
        }

        /// <summary>
        /// 秒数からmm:ss形式の文字列に変更
        /// </summary>
        /// <param name="second">秒数</param>
        /// <returns>mm:ssの文字列</returns>
        public static String getMinSec(int second)
        {
            return String.Format("{0:00}:{1:00}", second / 60, second % 60);
        }

        /// <summary>
        /// 秒数からmm:ss形式の文字列に変更
        /// </summary>
        /// <param name="second">秒数</param>
        /// <returns>mm:ssの文字列</returns>
        public static String getMinSec(double second)
        {
            int roundedsec = (int)(second + 0.5);
            return getMinSec(roundedsec);
        }

        /// <summary>
        /// 歌詞文字列からタイムタグを除去
        /// </summary>
        /// <param name="lyrics">タイムタグを含む歌詞文字列</param>
        /// <returns>タイムタグを除去した歌詞文字列</returns>
        public static String[] StripLyricsTimetag(string[] lyrics)
        {
            if (lyrics == null) return null;

            return lyrics.SelectMany(_ =>
            {
                if (_.Length > 0 && _[0] == '@')
                {
                    return new string[] { };
                }
                return new string[] { TimetagPattern.Replace(_, "") };
            }).ToArray();
        }

        /// <summary>
        /// SQLのためにSingle-Quotをエスケープする
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static String EscapeSingleQuotSQL(this string source)
        {
            return source.Replace("'", "''");
        }

        /// <summary>
        /// valueがすべてnullまたは空文字列ならば空文字列を返すフォーマッタ
        /// </summary>
        /// <param name="format">フォーマット文字列</param>
        /// <param name="value">値</param>
        /// <returns></returns>
        public static String FormatIfExists(this string format, params string[] value)
        {
            if (value.All((x) => string.IsNullOrEmpty(x))) return "";
            return String.Format(format, value);
        }

        /// <summary>
        /// 全角→半角変換
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static String LCMapZen2Han(this string source)
        {
            return LCMapString(source, MapFlags.HALFWIDTH);
        }

        /// <summary>
        /// 適当に文字列を正規化(H2k6のLCMapUpper風)
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static String LCMapUpper(this string source)
        {
            return LCMapString(source, MapFlags.HALFWIDTH | MapFlags.HIRAGANA | MapFlags.UPPERCASE);
        }

        /// <summary>
        /// LCMapStringのwrapper
        /// ref. http://d.hatena.ne.jp/deraw/20060831/1156992224
        /// sourceがnullのとき、nullを返す
        /// </summary>
        /// <param name="source">ソース文字列</param>
        /// <param name="mapFlags">変換フラグ</param>
        /// <returns>変換後の文字列</returns>
        public static String LCMapString(string source, MapFlags mapFlags)
        {
            if (source == null) return null;
            char[] buffer = new char[source.Length * 2];
            int len = _LCMapString(System.Threading.Thread.CurrentThread.CurrentCulture.LCID, (uint)mapFlags, source, source.Length, buffer, buffer.Length);
            if (len < 0)
            {
                throw new ArgumentException("\"LCMAP_SORTKEY\" is not support ");
            }

            return new String(buffer, 0, len);
        }

        /// <summary>
        /// 入力文字列に含まれる実数をパース
        /// </summary>
        /// <param name="src">入力文字列</param>
        /// <exception cref="System.FormatException"></exception>
        /// <returns>実数値</returns>
        public static double parseDouble(string src)
        {
            var match = doubleRe.Match(src);
            if (match.Success)
            {
                return double.Parse(match.Value);
            }
            throw new System.FormatException();
        }

        /// <summary>
        /// 入力文字列に含まれる整数をパース
        /// </summary>
        /// <param name="src">入力文字列</param>
        /// <param name="result">結果の整数</param>
        /// <returns>成否</returns>
        public static bool tryParseInt(string src, ref int result)
        {
            if (src == null) return false;
            var match = intRe.Match(src);
            if (match.Success)
            {
                return int.TryParse(match.Value, out result);
            }
            return false;
        }

        /// <summary>
        /// 文字列形式のトラックナンバーをintにパースする
        /// </summary>
        /// <param name="src">文字列形式のトラックナンバー</param>
        /// <param name="defaultValue">パースできなかった場合に返す値</param>
        /// <returns></returns>
        public static int GetTrackNumberInt(string src, int defaultValue = -1)
        {
            int ret = defaultValue;
            tryParseInt(src, ref ret);
            return ret;
        }

        [DllImport("kernel32.dll", EntryPoint = "LCMapString", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int _LCMapString(int locale, UInt32 dwMapFlags, string src, int srclen, [MarshalAs(UnmanagedType.LPArray)] char[] dest, int destlen);
        /// <summary>
        /// LCMapStringのフラグ
        /// </summary>
        public enum MapFlags : uint
        {

            /// <summary>
            /// lower case letters
            /// </summary>
            LOWERCASE = 0x00000100,

            /// <summary>
            /// 
            /// </summary>upper case letters
            UPPERCASE = 0x00000200,

            /// <summary>
            /// WC sort key (normalize)
            /// </summary>
            SORTKEY = 0x00000400,

            /// <summary>
            /// byte reversal
            /// </summary>
            BYTEREV = 0x00000800,

            /// <summary>
            /// map katakana to hiragana
            /// </summary>
            HIRAGANA = 0x00100000,

            /// <summary>
            /// map hiragana to katakana
            /// </summary>
            KATAKANA = 0x00200000,


            /// <summary>
            /// map double byte to single byte
            /// </summary>
            HALFWIDTH = 0x00400000,

            /// <summary>
            /// map single byte to double byte
            /// </summary>
            FULLWIDTH = 0x00800000,

            /// <summary>
            /// use linguistic rules for casing
            /// </summary>
            LINGUISTIC_CASING = 0x01000000, 

            /// <summary>
            /// map traditional chinese to simplified chinese
            /// </summary>
            SIMPLIFIED_CHINESE = 0x02000000,

            /// <summary>
            /// map simplified chinese to traditional chinese
            /// </summary>
            TRADITIONAL_CHINESE = 0x04000000,


            /// <summary>
            /// ignore case
            /// </summary>
            IGNORECASE = 0x00000001,

            /// <summary>
            /// ignore nonspacing chars
            /// </summary>
            IGNORENONSPACE = 0x00000002,

            /// <summary>
            /// ignore symbols
            /// </summary>
            IGNORESYMBOLS = 0x00000004,

            /// <summary>
            /// ignore kanatype
            /// </summary>
            IGNOREKANATYPE = 0x00010000,

            /// <summary>
            /// ignore width
            /// </summary>
            IGNOREWIDTH = 0x00020000,

            /// <summary>
            /// use string sort method
            /// </summary>
            STRINGSORT = 0x00001000
        }
    }
}
