using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

using System.Runtime.InteropServices;

namespace Gageas.Lutea.Util
{
    static class ByteArrayUtil
    {
        public static int IndexOf(this byte[] buffer, int offset, byte search){
            for (int i = offset; i < buffer.Length; i++)
            {
                if (buffer[i] == search) return i;
            }
            return -1;
        }
    }

    public static class Util
    {

        public static DateTime timestamp2DateTime(Int64 timestamp)
        {
            return MusicLibrary.timestamp2DateTime(timestamp);
        }

        public static String Repeat(this string src, int count)
        {
            if (count > 0)
            {
                return String.Join(src, new string[1 + count]);
            }
            return "";
        }
        public static String getMinSec(int second)
        {
            return String.Format("{0:00}:{1:00}", second / 60, second % 60);
        }

        public static String getMinSec(double second)
        {
            int roundedsec = (int)(second + 0.5);
            return getMinSec(roundedsec);
        }

        private static readonly System.Text.RegularExpressions.Regex TimetagPattern = new System.Text.RegularExpressions.Regex(@"(\[[\d-:]+\])|(\\([^_]|(_.)))");
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

        public static IEnumerable<int> IntegerCounterIterator(int start, int end, int step = 1)
        {
            for (int i = start; i <= end; i += 1) yield return i;
            yield break;
        }

        // プリペアドが使えないときはエスケープするしかないよね
        public static String EscapeSingleQuotSQL(this string source)
        {
            return source.Replace("'", "''");
        }

        public static String FormatIfExists(this string format, params string[] value) { 
            if(value.Any((x)=>!string.IsNullOrEmpty(x))){ // 非emptyなvalueが一つでもあれば
                return String.Format(format,value);
            }
            return "";
        }
        // 全角->半角変換
        public static String LCMapZen2Han(this string source)
        {
            return LCMapString(source, MapFlags.HALFWIDTH);
        }

        // FIXME?: H2k6のLCMapUpperのパラメータ調べてないので適当です
        public static String LCMapUpper(this string source)
        {
            return LCMapString(source, MapFlags.HALFWIDTH|MapFlags.HIRAGANA|MapFlags.UPPERCASE);
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
            if(source == null)return null;
            char[] buffer = new char[source.Length * 2];
            //StringBuilder buffer = new StringBuilder(source.Length*2);
            int len = _LCMapString(System.Threading.Thread.CurrentThread.CurrentCulture.LCID, (uint)mapFlags, source, source.Length, buffer, buffer.Length);
            if (len < 0)
            {
                throw new ArgumentException("\"LCMAP_SORTKEY\" is not support ");
            }

            return new String(buffer, 0, len);
        }

        private static readonly System.Text.RegularExpressions.Regex intRe = new System.Text.RegularExpressions.Regex(@"(\+|-)?\d+");
        private static readonly System.Text.RegularExpressions.Regex doubleRe = new System.Text.RegularExpressions.Regex(@"(\+|-)?\d+(\.)?(\d+)?");
        public static double parseDouble(string src)
        {
            var match = doubleRe.Match(src);
            if (match.Success)
            {
                return double.Parse(match.Value);
            }
            throw new System.FormatException();
        }

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

        [DllImport("kernel32.dll",EntryPoint = "LCMapString", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern int _LCMapString(int locale, UInt32 dwMapFlags, string src, int srclen, [MarshalAs(UnmanagedType.LPArray)] char[] dest, int destlen);
        public enum MapFlags : uint
        {

            LOWERCASE = 0x00000100,  // lower case letters
            UPPERCASE = 0x00000200,  // upper case letters
            //SORTKEY = 0x00000400,  // WC sort key (normalize)
            BYTEREV = 0x00000800,  // byte reversal

            HIRAGANA = 0x00100000,  // map katakana to hiragana
            KATAKANA = 0x00200000,  // map hiragana to katakana
            HALFWIDTH = 0x00400000,  // map double byte to single byte
            FULLWIDTH = 0x00800000,  // map single byte to double byte

            LINGUISTIC_CASING = 0x01000000,  // use linguistic rules for casing

            SIMPLIFIED_CHINESE = 0x02000000,  // map traditional chinese to simplified chinese
            TRADITIONAL_CHINESE = 0x04000000,  // map simplified chinese to traditional chinese


            IGNORECASE = 0x00000001,  // ignore case
            IGNORENONSPACE = 0x00000002,  // ignore nonspacing chars
            IGNORESYMBOLS = 0x00000004,  // ignore symbols

            IGNOREKANATYPE = 0x00010000,  // ignore kanatype
            IGNOREWIDTH = 0x00020000,  // ignore width

            STRINGSORT = 0x00001000  // use string sort method
        }
    }
}
