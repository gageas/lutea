using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.Tags
{
    class InternalCUE
    {
        public static CD Read(string filename)
        {
            var tag = MetaTag.readTagByFilename(filename, false);
            CD cd = CUEparser.fromString(tag.Find((e) => e.Key == "CUESHEET").Value.ToString(),filename,true);
            if (cd == null) return null;
            long bits = (new FileInfo(filename)).Length * 8;
            int bitrate = 1410*1000;
            try
            {
                using (var strm = new BASS.FileStream(filename, BASS.Stream.StreamFlag.BASS_STREAM_DECODE))
                {
                    bitrate = (int)(bits / strm.length);
                }
            }
            catch (Exception ex) {
                Logger.Error("cannot open file (by BASS)" + filename);
                Logger.Debug(ex);
            }

            foreach (var tr in cd.tracks)
            {
                // トラック番号取得
                int trackindex = 0;
                var trackIndex = tr.tag.Find((match) => match.Key == "TRACK" ? true : false);
                trackindex = int.Parse(trackIndex.Value.ToString());

                // InCUEの拡張タグ（？）をトラックのタグに付加
                var customColumns = tag.FindAll((e) => e.Key.IndexOf(string.Format("CUE_TRACK{0:00}_", trackindex)) == 0);
                foreach (var col in customColumns)
                {
                    string key = new Regex(@"^CUE_TRACK\d\d_(?<1>.*)$").Match(col.Key).Groups[1].Value;
                    tr.tag.Insert(0,new KeyValuePair<string, object>(key, col.Value));
                }

                // その他、ディスク全体のタグ情報をまとめてぶっこむ
                foreach (var disctag in tag)
                {
                    tr.tag.Add(disctag);
                }

                // InCUE内のFILE名が実体と異なっている場合があるため、ビットレートを付加しなおす
                tr.bitrate = bitrate;
            }
            return cd;
        }
    }
}
