﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Gageas.Lutea.Tags;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.Library
{
    class InternalCUEReader
    {
        public static CD Read(string filename, bool readStream)
        {
            var tag = MetaTag.readTagByFilename(filename, false);
            CD cd = CUEReader.ReadFromString(tag.Find((e) => e.Key == "CUESHEET").Value.ToString(), filename, readStream);
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

                // その他、ディスク全体のタグ情報をまとめてぶっこむ
                foreach (var disctag in tag)
                {
                    tr.tag.RemoveAll(_ => _.Key == disctag.Key);
                    tr.tag.Add(disctag);
                }

                // InCUEの拡張タグ（？）をトラックのタグに付加
                var customColumns = tag.FindAll((e) => e.Key.IndexOf(string.Format("CUE_TRACK{0:00}_", trackindex)) == 0);
                foreach (var col in customColumns)
                {
                    string key = new Regex(@"^CUE_TRACK\d\d_(?<1>.*)$").Match(col.Key).Groups[1].Value;
                    tr.tag.RemoveAll(_ => _.Key == key);
                    tr.tag.Insert(0,new KeyValuePair<string, object>(key, col.Value));
                }

                // PERFORMERがないとき、ARTISTをPERFORMERとして扱う
                if (tr.tag.Find((e) => { return e.Key == "PERFORMER"; }).Value == null)
                {
                    var artist = tr.tag.Find((e) => { return e.Key == "ARTIST"; });
                    if (artist.Value != null)
                    {
                        tr.tag.Add(new KeyValuePair<string, object>("PERFORMER", artist.Value));
                    }
                }

                // InCUE内のFILE名が実体と異なっている場合があるため、ビットレートを付加しなおす
                tr.bitrate = bitrate;
            }
            return cd;
        }
    }
}
