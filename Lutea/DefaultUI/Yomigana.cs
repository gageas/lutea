using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.DefaultUI
{
    public class Yomigana : IDisposable
    {
        private static readonly Dictionary<char, char> DakutenMAP = new Dictionary<char, char>()
        {
            {'ゔ','う'},
            {'が','か'}, {'ぎ','き'}, {'ぐ','く'}, {'げ','け'}, {'ご','こ'},
            {'ざ','さ'}, {'じ','し'}, {'ず','す'}, {'ぜ','せ'}, {'ぞ','そ'},
            {'だ','た'}, {'ぢ','ち'}, {'づ','つ'}, {'で','て'}, {'ど','と'},
            {'ば','は'}, {'び','ひ'}, {'ぶ','ふ'}, {'べ','へ'}, {'ぼ','ほ'},
            {'ぱ','は'}, {'ぴ','ひ'}, {'ぷ','ふ'}, {'ぺ','へ'}, {'ぽ','ほ'},

            {'ぁ','あ'}, {'ぃ','い'}, {'ぅ','う'}, {'ぇ','え'}, {'ぉ','お'},
            {'っ','つ'},
            {'ゃ','や'}, {'ゅ','ゆ'}, {'ょ','よ'},
            {'ゎ','わ'},
        };
        private string cacheFileName;
        private emanual.IME.ImeLanguage ime = null;
        private DefaultUIForm form;
        private Dictionary<string, char> yomiCache = null;
        public Yomigana(string cacheFilename, DefaultUIForm form)
        {
            this.form = form;
            this.cacheFileName = cacheFilename;
            try
            {
                form.Invoke((MethodInvoker)(() => { ime = new emanual.IME.ImeLanguage(); }));
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
            }
            LoadFile();
        }

        /* 左下、フィルタビュー関連
 * 方針
 * 　☆ 英数、かな、記号類は逆変換不要なので最初の1文字を返す
 * 　☆ 先頭が上記以外（きっと漢字）の場合、正確に逆変換するため数文字返す
 * 　　・先頭が漢字で2文字目も漢字のとき -> 先頭2文字を返す。
 * 　　　　理由：大抵の漢字熟語は2文字で切れる
 * 　　　　　　　人名の苗字も2文字が多いため、キャッシュヒット率向上が期待できる（佐藤○○と佐藤××が1回の逆変換ですませられる）
 * 　　　　問題：五十嵐など3文字の苗字等が正しく出せない
 * 　　　　
 * 　　・先頭が漢字で2文字目が漢字以外のとき -> 先頭5文字を返す。
 * 　　　　理由：2文字目以降は漢字の送り仮名になっていると考えられる。
 * 　　　　　　　正確な逆変換のため、少し多めに取る
 */
        public string GetLeadingChars(string src)
        {
            // これらの記号は完全に読み捨て
            while (src.Length > 0 && (
                src[0] == '　' ||
                src[0] == ' ' ||
                src[0] == '”' ||
                src[0] == '“' ||
                src[0] == '‘' ||
                src[0] == '"' ||
                src[0] == '\'' ||
                src[0] == '「' ||
                src[0] == '『' ||
                src[0] == '【' ||
                src[0] == '（' ||
                src[0] == '(' ||
                src[0] == '［' ||
                src[0] == '[' ||
                src[0] == '－' ||
                src[0] == '-' ||
                src[0] == '～'
                ))
            {
                src = src.Substring(1);
            }

            // 空の時
            if (string.IsNullOrEmpty(src)) return "\0";
            src = src.LCMapUpper();
            
            // 普通の文字
            if (char.IsLetterOrDigit(src[0]))
            {
                char first = src[0];

                // 先頭が英数、かな
                if ((first >= 'A' && first <= 'Z') ||
                    (first >= '0' && first <= '9') ||
                    (first >= 'あ' && first <= 'ん')
                    )
                {
                    return first.ToString();
                }
                else // 先頭はたぶん漢字
                {
                    if (src.Length == 1) return src + "　"; // 漢字1文字の場合、末尾に"　"を付けて2文字にしますorz
                    char second = src[1];
                    if (second >= 'あ' && second <= 'ん')
                    {
                        return src.Substring(0, (src.Length < 5 ? src.Length : 5));
                    }
                    else
                    {
                        return src.Substring(0, 2);
                    }
                }
            }
            return src;
        }

        public char GetFirst(string name)
        {
            char first = ' ';
            string leading = GetLeadingChars(name);

            if (leading.Length == 1) //
            {
                first = leading[0];
            }
            else
            {
                if (!yomiCache.ContainsKey(leading))
                {
                    if (ime == null)
                    {
                        yomiCache[leading] = leading[0];
                    }
                    else
                    {
                        form.Invoke((MethodInvoker)(() => { yomiCache[leading] = ime.GetYomi(leading)[0]; }));
                    }
                }
                first = yomiCache[leading];
            }
            if (DakutenMAP.ContainsKey(first)) first = DakutenMAP[first];
            return first;
        }

        public void Correct(string src, string correct)
        {
            if (string.IsNullOrEmpty(src)) return;
            yomiCache[GetLeadingChars(src)] = correct.LCMapUpper()[0];
            Flush();
            form.refreshFilter(null,src);
        }

        public void LoadFile()
        {
            try
            {
                using (var fs = new System.IO.FileStream(cacheFileName, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    yomiCache = (Dictionary<string, char>)(new BinaryFormatter()).Deserialize(fs);
                }
            }
            catch { }
            if (yomiCache == null) yomiCache = new Dictionary<string, char>();
        }

        public void Flush()
        {
            using (var fs = new System.IO.FileStream(cacheFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
            {
                (new BinaryFormatter()).Serialize(fs, yomiCache);
            }

        }

        public void Dispose()
        {
            if (ime != null)
            {
                ime.Dispose();
            }
        }
    }
}
