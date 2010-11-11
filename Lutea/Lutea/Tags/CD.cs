using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Gageas.Lutea;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.Tags
{
    class CD
    {
        public class Track : H2k6LibraryTrack
        {
            public int _start;
            public int _end;

            public int start
            {
                set
                {
                    _start = value;
                    duration = (int)(this.end > this.start ? ((this.end - this.start) / 75) : 0);
                }
                get
                {
                    return _start;
                }
            }

            public int end
            {
                set
                {
                    _end = value;
                    duration = (int)(this.end > this.start ? ((this.end - this.start) / 75) : 0);
                }
                get
                {
                    return _end;
                }
            }
            private string _file_name_CUESheet = "";
            // <summary>
            // 実体streamのfilename
            // </summary>
            public string file_name_CUESheet
            {
                get
                {
                    return _file_name_CUESheet;
                }
                set
                {
                    _file_name_CUESheet = value;
                }
            }
        }

        // <summary>
        // libraryに入れるfilename
        // </summary>
        public string filename;
        public string artist;
        public string album;
        public string date;
        public TimeSpan length;
        public float flength;
        public string genre;
        public string comment;
        public double? AlbumGain;
        public double? AlbumPeak;

        public List<Track> tracks = null;

        public CD()
        {
            filename = "";
            tracks = new List<Track>();
            artist = "";
            album = "";
            date = "";
            length = new TimeSpan(0);
            flength = 0;
            genre = "";
            comment = "";
        }
        public CD(string filename)
        {
        }
        private string escapeSinglequot(string str)
        {
            if (str == null) return "";
            return (new Regex("'")).Replace(str, "''");
        }
    }

    class CUEparser
    {
        private static Regex[] res = 
            {
                new Regex(@"(?<KEY>TRACK) (?<1>.*) AUDIO"),
                new Regex(@"(?<KEY>INDEX 01) (?<1>.*):(?<2>.*):(?<3>.*)"),
                new Regex(@"(?<KEY>TITLE) ""(?<1>.*)"""),
                new Regex(@"(?<KEY>TITLE) (?<1>.*)"),
                new Regex(@"(?<KEY>PERFORMER) ""(?<1>.*)"""),
                new Regex(@"(?<KEY>PERFORMER) (?<1>.*)"),
                new Regex(@"(?<KEY>FILE) ""(?<1>.*)"""),
                new Regex(@"(?<KEY>FILE) (?<1>.*)"),
                new Regex(@"(?<KEY>REM COMMENT) ""(?<1>.*)"""),
                new Regex(@"(?<KEY>REM COMMENT) (?<1>.*)"),
                new Regex(@"(?<KEY>REM GENRE) ""(?<1>.*)"""),
                new Regex(@"(?<KEY>REM GENRE) (?<1>.*)"),
                new Regex(@"(?<KEY>REM DATE) ""(?<1>.*)"""),
                new Regex(@"(?<KEY>REM DATE) (?<1>.*)"),
                new Regex(@"(?<KEY>REM REPLAYGAIN_TRACK_GAIN) (?<1>(\+|-)?(\d|\.)+)"), //                    REM REPLAYGAIN_TRACK_GAIN -11.20 dB
                new Regex(@"(?<KEY>REM REPLAYGAIN_TRACK_PEAK) (?<1>(\+|-)?(\d|\.)+)"),//    REM REPLAYGAIN_TRACK_PEAK 1.000000
                new Regex(@"(?<KEY>REM REPLAYGAIN_ALBUM_GAIN) (?<1>(\+|-)?(\d|\.)+)"), //                    REM REPLAYGAIN_TRACK_GAIN -11.20 dB
                new Regex(@"(?<KEY>REM REPLAYGAIN_ALBUM_PEAK) (?<1>(\+|-)?(\d|\.)+)"),//    REM REPLAYGAIN_TRACK_PEAK 1.000000
            };

        public static CD fromString(string[] cueStringLines, string filename, bool readStreamFile)
        {
            CD cd = new CD();
            bool isGlobal = true;
            string last_filename = "";
            int last_length = 0;
            int last_bitrate = 0; // bits per sec
            int last_channels = 0;
            int last_freq = 0;

            cd.filename = filename;
            int lastIndex = 0; //末尾インデックスのキャッシュ
            for (int i = 0; i < cueStringLines.Length; i++)
            {
                string line = cueStringLines[i];
                for (int j = 0; j < res.Length; j++)
                {
                    Match m = res[j].Match(line);
                    if (m.Success)
                    {
                        switch (m.Groups["KEY"].Value)
                        {
                            case "FILE": // FILEのとき
                                last_filename = m.Groups[1].Value;
                                if (!Path.IsPathRooted(last_filename))
                                {
                                    last_filename = Path.GetDirectoryName(filename) + Path.DirectorySeparatorChar + last_filename;
                                }
                                double sec = 0;
                                long bits = 0;
                                if (readStreamFile)
                                {
                                    try
                                    {
                                        using (var strm = new BASS.FileStream(last_filename,BASS.Stream.StreamFlag.BASS_STREAM_DECODE))
                                        {
                                            sec = strm.length;
                                            last_channels = (int)strm.Info.chans;
                                            last_freq = (int)strm.Info.freq;
                                            bits = (new FileInfo(last_filename)).Length * 8;
                                        }
                                    }
                                    catch {
                                        try
                                        {
                                            using (var strm2 = new BASS.FileStream(filename,BASS.Stream.StreamFlag.BASS_STREAM_DECODE))
                                            {
                                                sec = strm2.length;
                                                last_channels = (int)strm2.Info.chans;
                                                last_freq = (int)strm2.Info.freq;
                                                bits = (new FileInfo(filename)).Length * 8;
                                            }
                                        }
                                        catch { }
                                    };
                                }
                                cd.length = cd.length.Add(new TimeSpan(0, 0, 0, (int)sec, (int)(sec * 1000) % 1000));
                                last_length = (int)(sec * 75);
                                if (sec > 0)
                                {
                                    last_bitrate = (int)(bits / sec);
                                }
                                else
                                {
//                                    last_filename = "";
                                    last_bitrate = 1410*1000;
                                }
                                break;
                            case "PERFORMER": // PERFORMERのとき
                                if (isGlobal)
                                {
                                    cd.artist = m.Groups[1].Value;
                                }
                                else
                                {
                                    cd.tracks[lastIndex].tag.Add(new KeyValuePair<string, object>("ARTIST", m.Groups[1].Value));
                                }
                                break;
                            case "TITLE": // TITLEのとき
                                if (isGlobal)
                                {
                                    cd.album = m.Groups[1].Value;
                                }
                                else
                                {
                                    cd.tracks[lastIndex].tag.Add(new KeyValuePair<string, object>("TITLE", m.Groups[1].Value));
                                }
                                break;
                            case "REM GENRE": // REM GENREのとき
                                cd.genre = m.Groups[1].Value;
                                break;
                            case "REM DATE": // REM DATEのとき
                                cd.date = m.Groups[1].Value;
                                break;
                            case "REM REPLAYGAIN_TRACK_GAIN":
                                //                                cd.tracks[lastIndex].TrackGain = double.Parse(m.Groups[1].Value);
                                cd.tracks[lastIndex].tag.Add(new KeyValuePair<string, object>("TRACK GAIN", double.Parse(m.Groups[1].Value)));
                                break;
                            case "REM REPLAYGAIN_TRACK_PEAK":
                                //                                cd.tracks[lastIndex].TrackPeak = double.Parse(m.Groups[1].Value);
                                cd.tracks[lastIndex].tag.Add(new KeyValuePair<string, object>("TRACK PEAK", double.Parse(m.Groups[1].Value)));
                                break;
                            case "REM REPLAYGAIN_ALBUM_GAIN":
                                cd.AlbumGain = double.Parse(m.Groups[1].Value);
                                break;
                            case "REM REPLAYGAIN_ALBUM_PEAK":
                                cd.AlbumPeak = double.Parse(m.Groups[1].Value);
                                break;
                            case "REM COMMENT": // REM COMMENTのとき
                                if (isGlobal)
                                {
                                    cd.comment = m.Groups[1].Value;
                                }
                                else
                                {
                                    cd.tracks[lastIndex].tag.Add(new KeyValuePair<string, object>("COMMENT", m.Groups[1].Value));
                                }
                                break;
                            case "TRACK": // TRACK nn AUDIO のとき
                                cd.tracks.Add(new CD.Track());
                                lastIndex = cd.tracks.Count - 1;
                                //                                cd.tracks[lastIndex].file_name = last_filename;
                                cd.tracks[lastIndex].file_name_CUESheet = last_filename;
                                cd.tracks[lastIndex].bitrate = last_bitrate;
                                cd.tracks[lastIndex].end = last_length;
                                cd.tracks[lastIndex].modify = H2k6Library.currentTimestamp;
                                cd.tracks[lastIndex].tag.Add(new KeyValuePair<string, object>("ALBUM", cd.album));
                                //                                cd.tracks[lastIndex].tag.Add(new KeyValuePair<string, object>("ARTIST", cd.artist));
                                cd.tracks[lastIndex].tag.Add(new KeyValuePair<string, object>("TRACK", lastIndex + 1));
                                cd.tracks[lastIndex].channels = last_channels;
                                cd.tracks[lastIndex].freq = last_freq;
                                isGlobal = false;
                                break;
                            case "INDEX 01": // INDEX 01 00:00:00のとき
                                if (cd.tracks.Count == 0) break;
                                cd.tracks[lastIndex].start = (Convert.ToInt32(m.Groups[1].Value) * 60 + Convert.ToInt32(m.Groups[2].Value)) * 75 + Convert.ToInt32(m.Groups[3].Value);
                                if (lastIndex > 0)
                                {
                                    if (cd.tracks[lastIndex - 1].file_name_CUESheet == last_filename)
                                    {
                                        cd.tracks[lastIndex - 1].end = cd.tracks[lastIndex].start;
                                    }
                                }
                                break;
                        }
                        break;
                    }
                }
            }
            // ミリ秒をカット
            cd.length = cd.length.Subtract(new TimeSpan(0, 0, 0, 0, cd.length.Milliseconds));
            if (cd.tracks.Count == 0) return null;
            foreach (var tr in cd.tracks)
            {
                int trackindex = 0;
                var trackIndex = tr.tag.Find((match) => match.Key == "TRACK" ? true : false);
                trackindex = int.Parse(trackIndex.Value.ToString());
                tr.file_name = filename + new String(' ', trackindex);
                tr.tag.Add(new KeyValuePair<string, object>("ARTIST", cd.artist));
                tr.tag.Add(new KeyValuePair<string, object>("GENRE", cd.genre));
                tr.tag.Add(new KeyValuePair<string, object>("DATE", cd.date));
                tr.tag.Add(new KeyValuePair<string, object>("COMMENT", cd.comment));
                if (cd.AlbumGain != null) { tr.tag.Add(new KeyValuePair<string, object>("ALBUM GAIN", cd.AlbumGain)); }
                if (cd.AlbumPeak != null) { tr.tag.Add(new KeyValuePair<string, object>("ALBUM PEAK", cd.AlbumPeak)); }
            }
            return cd;
        }
        public static CD fromString(string cueString, string filename, bool readStreamFile)
        {
            string[] lines = cueString.Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            return (fromString(lines, filename, readStreamFile));
        }

        public static CD fromFile(string filename, bool readStreamFile)
        { // このめそっどきもい
            string[] cueStringLines = File.ReadAllLines(filename, Encoding.Default);
            CD cd = fromString(cueStringLines, filename, readStreamFile);
            if (cd == null) return null;
            cd.filename = filename;
            return cd;
        }
    }
}
