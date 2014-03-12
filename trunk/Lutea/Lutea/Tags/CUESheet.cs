using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.Tags
{
    /// <summary>
    /// CUEシートを表現するクラス
    /// </summary>
    public class CUESheet
    {
        #region 内部型の定義
        /// <summary>
        /// TRACKのタイプ
        /// </summary>
        public enum TrackType { AUDIO, BINARY };

        /// <summary>
        /// Min, Sec, Frame(1/75sec)による時刻表現
        /// </summary>
        public class MSFTime{
            public int Min;
            public int Sec;
            public int Frame;
            public MSFTime(int min, int sec, int frame)
            {
                if (this.Sec >= 60) throw new ArgumentOutOfRangeException();
                if (this.Frame >= 75) throw new ArgumentOutOfRangeException();
                this.Min = min;
                this.Sec = sec;
                this.Frame = frame;
            }
            public double ToSecDouble
            {
                get
                {
                    return (this.Min * 60) + this.Sec + (this.Frame / 75.0);
                }
            }
            public int ToFrames
            {
                get
                {
                    return ((this.Min * 60) + this.Sec) * 75 + this.Frame;
                }
            }
        }

        /// <summary>
        /// 各トラック
        /// </summary>
        public class Track
        {
            public TrackType Type;
            public string Title;
            public string Performer;
            public string Comment;
            public string Filename;
            public double? Peak = null;
            public double? Gain = null;
            public MSFTime Index00;
            public MSFTime Index01;
            public MSFTime Index02;
            public string Isrc;
        }
        #endregion
        public string Title;
        public string Performer;
        public string Genre;
        public string Comment;
        public string Date;
        public string Discid;
        public string Catalog;
        public double? Gain = null;
        public double? Peak = null;
        public IList<Track> Tracks = new List<Track>();
    }
}
