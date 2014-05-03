using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Gageas.Lutea.Library;
using Gageas.Lutea.Tags;

namespace Gageas.Lutea.SoundStream
{
    class DecodeStreamFactory
    {
        private static PullSoundStreamBase ApplyTrackRange(PullSoundStreamBase self, CD.Track track)
        {
            var RangeOffset = GetFrame2Sample(self, track.Start);
            var RangeLength = track.End > track.Start
                ? GetFrame2Sample(self, track.End - track.Start)
                : self.LengthSample - RangeOffset;

            var gain = track.getTagValue("ALBUM GAIN");
            if (gain != null)
            {
                self = new ReplayGainOverrideFilter(self, Util.Util.parseDouble(gain.ToString()));
            }
            self = new RangeFilter(self, RangeOffset, RangeLength);
            return self;
        }

        /// <summary>
        /// CDのフレームからサンプル数に変換する
        /// </summary>
        /// <param name="frames">フレーム数</param>
        /// <returns>サンプル数</returns>
        private static ulong GetFrame2Sample(PullSoundStreamBase llStream, int frames)
        {
            return (ulong)frames * (llStream.Freq / 75);
        }

        /// <summary>
        /// CUEシートのTrack情報からストリームを生成
        /// </summary>
        /// <param name="track">CUEのTrack情報</param>
        /// <param name="preScan">preScanを行うかどうか</param>
        /// <returns></returns>
        private static PullSoundStreamBase CreateStreamCue(CD.Track track, bool preScan)
        {
            String streamFullPath = System.IO.Path.IsPathRooted(track.file_name_CUESheet)
                ? track.file_name_CUESheet
                : Path.GetDirectoryName(track.file_name) + Path.DirectorySeparatorChar + track.file_name_CUESheet;

            PullSoundStreamBase self = CreateFileStreamPrimitive(streamFullPath, preScan);
            self = ApplyTrackRange(self, track);
            return self;
        }

        public static PullSoundStreamBase CreateFileStream(string filename, int tracknumber, bool preScan, List<KeyValuePair<string, object>> tag)
        {
            Logger.Log(String.Format("Trying to open file {0}", filename));

            filename = filename.Trim();

            PullSoundStreamBase self;
            if (Path.GetExtension(filename).ToUpper() == ".CUE")
            {
                // case for CUE sheet
                CD cd = CUEReader.ReadFromFile(filename, false);
                self = CreateStreamCue(cd.tracks[tracknumber - 1], preScan);
            }
            else
            {
                self = CreateFileStreamPrimitive(filename, preScan);

                // case for Internal CUESheet
                if (tracknumber > 0)
                {
                    if (tag == null)
                    {
                        tag = Tags.MetaTag.readTagByFilename(filename, false);
                    }
                    if (tag == null) throw new FormatException();

                    KeyValuePair<string, object> cue = tag.Find((match) => match.Key == "CUESHEET");

                    if (cue.Key != null)
                    {
                        CD cd = CUEReader.ReadFromString(cue.Value.ToString(), filename, false);
                        self = ApplyTrackRange(self, cd.tracks[tracknumber - 1]);
                    }
                }
            }
            return self;
        }

        /// <summary>
        /// 正確なオフセットとレングス情報を取得して補正値に設定する
        /// </summary>
        /// <param name="tag"></param>
        private static Tuple<ulong, ulong> RetrieveAccurateRange(string filename, ulong totalLength, List<KeyValuePair<string, object>> tag)
        {
            if (tag != null)
            {
                KeyValuePair<string, object> iTunSMPB = tag.Find((match) => match.Key.ToUpper() == "ITUNSMPB");
                if (iTunSMPB.Value != null)
                {
                    var smpbs = iTunSMPB.Value.ToString().Trim().Split(new char[] { ' ' }).Select(_ => System.Convert.ToUInt64(_, 16)).ToArray();
                    // ref. http://nyaochi.sakura.ne.jp/archives/2006/09/15/itunes-v70070%E3%81%AE%E3%82%AE%E3%83%A3%E3%83%83%E3%83%97%E3%83%AC%E3%82%B9%E5%87%A6%E7%90%86/
                    return new Tuple<ulong, ulong>((smpbs[1] + smpbs[2]), (smpbs[3]));
                }
            }
            var lametag = Lametag.Read(filename);
            if (lametag != null)
            {
                return new Tuple<ulong, ulong>((ulong)(lametag.delay), totalLength - (ulong)(lametag.delay + lametag.padding));
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="System.ArgumentException"></exception>
        /// <param name="filename"></param>
        /// <param name="preScan"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static PullSoundStreamBase CreateFileStreamPrimitive(string filename, bool preScan = false, List<KeyValuePair<string, object>> tag = null)
        {
            PullSoundStreamBase self = new BASSDecodeStreamAdapter(filename, true, preScan);

            if (tag == null)
            {
                tag = Tags.MetaTag.readTagByFilename(filename, false);
            }

            if (tag != null)
            {
                KeyValuePair<string, object> gain = tag.Find((match) => match.Key == "REPLAYGAIN_ALBUM_GAIN");
                if (gain.Value != null)
                {
                    self = new ReplayGainOverrideFilter(self, Util.Util.parseDouble(gain.ToString()));
                }
            }

            var range = RetrieveAccurateRange(filename, self.LengthSample, tag);
            if ((range != null) && self.LengthSample > (range.Item1 + range.Item2))
            {
                self = new RangeFilter(self, range.Item1, range.Item2);
            }

            return self;
        }
    }
}
