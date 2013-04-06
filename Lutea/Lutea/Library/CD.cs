using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Gageas.Lutea;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.Library
{
    class CD
    {
        public class Track : LuteaAudioTrack
        {
            public int start;
            public int end;

            public int Start
            {
                set
                {
                    start = value;
                    duration = (int)(end > start ? ((end - start) / 75) : 0);
                    file_size = (int)((long)bitrate * (end - start) / 75.0 / 8);
                }
                get
                {
                    return start;
                }
            }

            public int End
            {
                set
                {
                    end = value;
                    duration = (int)(end > start ? ((end - start) / 75) : 0);
                    file_size = (int)((long)bitrate * (end - start) / 75.0 / 8); 
                }
                get 
                {
                    return end;
                }
            }
            // <summary>
            // 実体streamのfilename
            // </summary>
            public string file_name_CUESheet
            {
                get;
                set;
            }

            /// <summary>
            /// 実体streamのextension
            /// </summary>
            public string file_ext_CUESheet
            {
                get
                {
                    return System.IO.Path.GetExtension(file_name_CUESheet).Trim().Substring(1).ToUpper();
                }
            }

            public void AddTag(string key, object value)
            {
                if (value != null)
                {
                    this.tag.Add(new KeyValuePair<string, object>(key, value.ToString()));
                }
            }
        }

        // <summary>
        // libraryに入れるfilename
        // </summary>
        public TimeSpan length;
        public List<Track> tracks = new List<Track>();

        public CD()
        {
        }
    }
}
