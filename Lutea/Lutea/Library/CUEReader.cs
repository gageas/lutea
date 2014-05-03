using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Gageas.Lutea.SoundStream;
using Gageas.Lutea.Tags;

namespace Gageas.Lutea.Library
{
    class CUEReader
    {
        /// <summary>
        /// FILE命令の対象ストリームの解析結果
        /// </summary>
        private struct StreamInfo{
            public int Channels;
            public TimeSpan Length;
            public int Freq;
            public int Bitrate;
            public double LengthInSec
            {
                get
                {
                    return Length.TotalMilliseconds / 1000.0;
                }
            }
        }

        /// <summary>
        /// 文字列からCUEシートを読んでCDオブジェクトを返す
        /// </summary>
        /// <param name="cueSheet">CUEシートの文字列</param>
        /// <param name="alternativeFilename">FILE命令の代替とする音源のファイル名。埋め込みCUEの場合、埋め込み先のファイル名を指定する</param>
        /// <param name="readStream">音源のファイルまで読んで解析するかどうか。trueでないとビットレートや最終トラックの長さが取得できない</param>
        /// <returns>CDオブジェクトまたはnull</returns>
        public static CD ReadFromString(string cueSheet, string alternativeFilename, bool readStream)
        {
            var cue = Tags.CUEParser.FromString(cueSheet);
            if (cue == null || cue.Tracks == null || cue.Tracks.Count == 0) return null;
            return ConvertCue2CD(cue, alternativeFilename, alternativeFilename, readStream);
        }

        /// <summary>
        /// .CUEファイルからCUEシートを読んでCDオブジェクトを返す
        /// </summary>
        /// <param name="cueFilename">CUEファイル名</param>
        /// <param name="readStream">音源のファイルまで読んで解析するかどうか。trueでないとビットレートや最終トラックの長さが取得できない</param>
        /// <returns>CDオブジェクトまたはnull</returns>
        public static CD ReadFromFile(string cueFilename, bool readStream)
        {
            var cue = Tags.CUEParser.FromFile(cueFilename);
            if (cue == null || cue.Tracks == null || cue.Tracks.Count == 0) return null;
            return ConvertCue2CD(cue, cueFilename, null, readStream);
        }

        /// <summary>
        /// CUEからCDオブジェクトを生成
        /// </summary>
        /// <param name="cue">CUEシート</param>
        /// <param name="cueFilename">CUEシートのファイル名。埋め込みの場合は音源ファイル名。</param>
        /// <param name="alternativeFilename">FILE命令の代替とする音源のファイル名。</param>
        /// <param name="readStream">音源のファイルまで読んで解析するかどうか。trueでないとビットレートや最終トラックの長さが取得できない</param>
        /// <returns>CDオブジェクト</returns>
        private static CD ConvertCue2CD(CUESheet cue, string cueFilename, string alternativeFilename = null, bool readStream = true)
        {
            var cd = new CD();
            var info = new StreamInfo();
            string lastFilename = null;

            CD.Track prevTrack = null, currentTrack = null;
            for (int i = 0; i < cue.Tracks.Count; i++)
            {
                var cueTr = cue.Tracks[i];
                if (cueTr.Type != Tags.CUESheet.TrackType.AUDIO) continue;
                currentTrack = new CD.Track();
                var rootedFilename = (!Path.IsPathRooted(cueTr.Filename) ? Path.GetDirectoryName(cueFilename) + Path.DirectorySeparatorChar : "") + cueTr.Filename;
                if (rootedFilename != lastFilename)
                {
                    lastFilename = rootedFilename;
                    if (readStream)
                    {
                        info = GetStreamInfo(alternativeFilename ?? rootedFilename);
                        cd.length = cd.length.Add(info.Length);
                    }
                }
                else if (prevTrack != null)
                {
                    prevTrack.End = cueTr.Index01.ToFrames;
                }
                /* Set Stream info */
                currentTrack.bitrate = info.Bitrate;
                currentTrack.channels = info.Channels;
                currentTrack.freq = info.Freq;
                
                currentTrack.Start = cueTr.Index01.ToFrames; 
                currentTrack.End = (int)(info.LengthInSec * 75); 
                currentTrack.file_name = cueFilename + new String(' ', (i + 1));
                currentTrack.file_name_CUESheet = rootedFilename;

                /* Set track info to Tag */
                currentTrack.AddTag("TRACK", (i + 1).ToString());
                currentTrack.AddTag("TITLE", cueTr.Title);
                currentTrack.AddTag("ARTIST", cueTr.Performer);
                currentTrack.AddTag("COMMENT", cueTr.Comment);
                currentTrack.AddTag("ISRC", cueTr.Isrc);
                currentTrack.AddTag("TRACK GAIN", cueTr.Gain);
                currentTrack.AddTag("TRACK PEAK", cueTr.Peak);

                /* Set album info to Tag */
                currentTrack.AddTag("ALBUM", cue.Title);
                currentTrack.AddTag("ARTIST", cue.Performer);
                currentTrack.AddTag("ALBUM ARTIST", cue.Performer);
                currentTrack.AddTag("GENRE", cue.Genre);
                currentTrack.AddTag("DATE", cue.Date);
                currentTrack.AddTag("COMMENT", cue.Comment);
                currentTrack.AddTag("ALBUM GAIN", cue.Gain);
                currentTrack.AddTag("ALBUM PEAK", cue.Peak);

                cd.tracks.Add(currentTrack);
                prevTrack = currentTrack;
            }
            return cd;
        }

        /// <summary>
        /// 音源ファイルを解析して各種情報を返す
        /// </summary>
        /// <param name="filename">音源ファイル名</param>
        /// <returns>StreamInfo構造体</returns>
        static StreamInfo GetStreamInfo(string filename)
        {
            StreamInfo info = new StreamInfo();
            if (!File.Exists(filename))
            {
                Logger.Error("File does not exist. " + filename);
                return info;
            }
            double sec = 0;
            long bits = 0;
            try
            {
                using (var strm = SoundStream.DecodeStreamFactory.CreateFileStreamPrimitive(filename))
                {
                    sec = strm.LengthSec;
                    info.Channels = (int)strm.Chans;
                    info.Freq = (int)strm.Freq;
                    bits = (new FileInfo(filename)).Length * 8;
                }
            }
            catch(ArgumentException e)
            {
                Logger.Error(filename + "\n" + e);
            }
            info.Length = new TimeSpan(0, 0, 0, (int)sec, (int)(sec * 1000) % 1000);
            info.Bitrate = ((sec > 0) ? (int)(bits / sec) : 1410 * 1000);
            return info;
        }
    }
}
