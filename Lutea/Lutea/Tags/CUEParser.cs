using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace Gageas.Lutea.Tags
{
    /// <summary>
    /// CUEシートのパーサ
    /// </summary>
    class CUEParser
    {
        /// <summary>
        /// パーサのコンテキストを保持
        /// </summary>
        private class ParserContext
        {
            public CUESheet cueSheet;
            public CUESheet.Track latestTrack = null;
            public CUESheet.Track prevTrack = null;
            public string latestFilename;
            public ParserContext(CUESheet cueSheet)
            {
                this.cueSheet = cueSheet;
            }
        }

        #region CUEシート各行の正規表現
        /// <summary>
        /// TRACK
        /// </summary>
        private static readonly Regex CueParseRe_TRACK = new Regex(@"TRACK (?<1>.*) (?<2>AUDIO|BINARY)");
        /// <summary>
        /// INDEX
        /// </summary>
        private static readonly Regex CueParseRe_INDEX = new Regex(@"INDEX 0(?<TYPE>[012]) (?<1>.*):(?<2>.*):(?<3>.*)");
        /// <summary>
        /// 一般のテキスト情報
        /// </summary>
        private static readonly Regex CueParseRe_GeneralInfo = new Regex(@"(?<KEY>(TITLE|PERFORMER|FILE|CATALOG|ISRC|(REM (COMMENT|GENRE|DATE|DISCID)))) (?<QUOT>""?)(?<1>.*)\k<QUOT>");
        /// <summary>
        /// RG情報
        /// </summary>
        private static readonly Regex CueParseRe_ReplayGainInfo = new Regex(@"REM REPLAYGAIN_(?<INFOTYPE>(TRACK|ALBUM)_(GAIN|PEAK)) (?<1>(\+|-)?(\d|\.)+)");
        #endregion

        #region CUEシートの行の種類別マッチング関数群
        /// <summary>
        /// INDEX命令へのマッチング
        /// </summary>
        /// <param name="line">CUEシートの行</param>
        /// <param name="ctx">パーサコンテキスト</param>
        /// <returns>マッチしたかどうか</returns>
        private static bool MatchProc_INDEX(string line, ParserContext ctx)
        {
            if (ctx.latestTrack == null) return false;
            Match m;
            if (!(m = CueParseRe_INDEX.Match(line)).Success) return false;
            switch (m.Groups["TYPE"].Value)
            {
                case "0":
                    ctx.cueSheet.Tracks.Last().Index00 = new CUESheet.MSFTime(Convert.ToInt32(m.Groups[1].Value), Convert.ToInt32(m.Groups[2].Value), Convert.ToInt32(m.Groups[3].Value));
                    break;
                case "1":
                    ctx.cueSheet.Tracks.Last().Index01 = new CUESheet.MSFTime(Convert.ToInt32(m.Groups[1].Value), Convert.ToInt32(m.Groups[2].Value), Convert.ToInt32(m.Groups[3].Value));
                    ctx.latestTrack.Filename = ctx.latestFilename;
                    break;
                case "2":
                    ctx.cueSheet.Tracks.Last().Index02 = new CUESheet.MSFTime(Convert.ToInt32(m.Groups[1].Value), Convert.ToInt32(m.Groups[2].Value), Convert.ToInt32(m.Groups[3].Value));
                    break;
            }
            return true;
        }

        /// <summary>
        /// TRACK命令へのマッチング
        /// </summary>
        /// <param name="line">CUEシートの行</param>
        /// <param name="ctx">パーサコンテキスト</param>
        /// <returns>マッチしたかどうか</returns>
        private static bool MatchProc_TRACK(string line, ParserContext ctx)
        {
            Match m;
            if (!(m = CueParseRe_TRACK.Match(line)).Success) return false;
            ctx.prevTrack = ctx.latestTrack;
            ctx.latestTrack = new CUESheet.Track();
            ctx.cueSheet.Tracks.Add(ctx.latestTrack);
            ctx.latestTrack.Type = m.Groups[2].Value.ToUpper() == "AUDIO" ? CUESheet.TrackType.AUDIO : CUESheet.TrackType.BINARY;
            ctx.latestTrack.Filename = ctx.latestFilename;
            return true;
        }

        /// <summary>
        /// 一般的なテキスト情報へのマッチング
        /// </summary>
        /// <param name="line">CUEシートの行</param>
        /// <param name="ctx">パーサコンテキスト</param>
        /// <returns>マッチしたかどうか</returns>
        private static bool MatchProc_GeneralInfo(string line, ParserContext ctx)
        {
            Match m;
            if (!(m = CueParseRe_GeneralInfo.Match(line)).Success) return false;
            switch (m.Groups["KEY"].Value)
            {
                case "FILE": // FILEのとき
                    ctx.latestFilename = m.Groups[1].Value;
                    break;
                case "PERFORMER": // PERFORMERのとき
                    if (ctx.latestTrack == null)
                    {
                        ctx.cueSheet.Performer = m.Groups[1].Value;
                    }
                    else
                    {
                        ctx.latestTrack.Performer = m.Groups[1].Value;
                    }
                    break;
                case "TITLE": // TITLEのとき
                    if (ctx.latestTrack == null)
                    {
                        ctx.cueSheet.Title = m.Groups[1].Value;
                    }
                    else
                    {
                        ctx.latestTrack.Title = m.Groups[1].Value;
                    }
                    break;
                case "CATALOG":
                    ctx.cueSheet.Catalog = m.Groups[1].Value;
                    break;
                case "REM GENRE": // REM GENREのとき
                    ctx.cueSheet.Genre = m.Groups[1].Value;
                    break;
                case "REM DATE": // REM DATEのとき
                    ctx.cueSheet.Date = m.Groups[1].Value;
                    break;
                case "REM COMMENT": // REM COMMENTのとき
                    if (ctx.latestTrack == null)
                    {
                        ctx.cueSheet.Comment = m.Groups[1].Value;
                    }
                    else
                    {
                        ctx.latestTrack.Comment = m.Groups[1].Value;
                    }
                    break;
                case "ISRC":
                    if (ctx.latestTrack == null) break;
                    ctx.latestTrack.Isrc = m.Groups[1].Value.ToUpper();
                    break;
            }
            return true;
        }

        /// <summary>
        /// RG情報へのマッチング
        /// </summary>
        /// <param name="line">CUEシートの行</param>
        /// <param name="ctx">パーサコンテキスト</param>
        /// <returns>マッチしたかどうか</returns>
        private static bool MatchProc_ReplayGainInfo(string line, ParserContext ctx)
        {
            Match m;
            if (!(m = CueParseRe_ReplayGainInfo.Match(line)).Success) return false;
            switch (m.Groups["INFOTYPE"].Value)
            {
                case "TRACK_GAIN":
                    ctx.latestTrack.Gain = double.Parse(m.Groups[1].Value);
                    break;
                case "TRACK_PEAK":
                    ctx.latestTrack.Peak = double.Parse(m.Groups[1].Value);
                    break;
                case "ALBUM_GAIN":
                    ctx.cueSheet.Gain = double.Parse(m.Groups[1].Value);
                    break;
                case "ALBUM_PEAK":
                    ctx.cueSheet.Peak = double.Parse(m.Groups[1].Value);
                    break;
            }
            return true;
        }
        #endregion

        /// <summary>
        /// CUEシート各行の文字列からの解析
        /// </summary>
        /// <param name="cueStringLines">CUEシートの行ごとの文字列</param>
        /// <returns>CUESheetオブジェクトまたはnull</returns>
        public static CUESheet FromString(string[] cueStringLines)
        {
            CUESheet cueSheet = new CUESheet();
            var ctx = new ParserContext(cueSheet);

            foreach (string line in cueStringLines)
            {
                if (MatchProc_INDEX(line, ctx)) continue;
                if (MatchProc_TRACK(line, ctx)) continue;
                if (MatchProc_GeneralInfo(line, ctx)) continue;
                if (MatchProc_ReplayGainInfo(line, ctx)) continue;
            }
            if (cueSheet.Tracks.Count == 0) return null;
            return cueSheet;
        }

        /// <summary>
        /// CUEシート全体の文字列からの解析
        /// </summary>
        /// <param name="cueString">CUEシート全体文字列</param>
        /// <returns>CUESheetオブジェクトまたはnull</returns>
        public static CUESheet FromString(string cueString)
        {
            string[] lines = cueString.Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            return (FromString(lines));
        }

        /// <summary>
        /// CUEファイルからの解析
        /// </summary>
        /// <param name="filename">CUEファイル名</param>
        /// <returns>CUESheetオブジェクトまたはnull</returns>
        public static CUESheet FromFile(string filename)
        {
            string[] cueStringLines = File.ReadAllLines(filename, Encoding.Default);
            CUESheet cueSheet = FromString(cueStringLines);
            if (cueSheet == null) return null;
            return cueSheet;
        }
    }
}
