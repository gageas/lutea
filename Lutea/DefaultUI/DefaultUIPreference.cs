using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{

    public class DefaultUIPreference : LuteaPreference
    {
        public enum FFTNum
        {
            FFT256 = 256,
            FFT512 = 512,
            FFT1024 = 1024,
            FFT2048 = 2048,
            FFT4096 = 4096,
            FFT8192 = 8192,
        }

        public enum SpectrumModes
        {
            None = -1,
            Mode0 = 0,
            Mode1 = 1,
            Mode2 = 2,
            Mode3 = 3,
            Mode4 = 4
        }
        
        /// <summary>
        /// トラックナンバーの書式
        /// </summary>
        public enum TrackNumberFormats { 
            Nothing = -1,
            N = 0,
            NofM = 1 
        }
        
        /// <summary>
        /// PropertyGridに行間調整用intを表示するTypeConverter
        /// </summary>
        public class LineHeightAdjustmentConverter : Int32Converter
        {
            readonly int[] intlist = {-15,-14,-13,-12,-11,-10,-9,-8,-7,-6,-5,-4,-3,-2,-1,0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15};

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return true;
            }

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(intlist);
            }

            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
            {
                return true;
            }
        }

        public class TrackNumberFormatConverter : EnumConverter
        {
            private Dictionary<TrackNumberFormats, string> dict = new Dictionary<TrackNumberFormats, string>() { 
                {TrackNumberFormats.Nothing, ""},
                {TrackNumberFormats.N, "1, 2, 3, ..."}, 
                {TrackNumberFormats.NofM, "1/3, 2/3, 3/3, ..."} ,
            };
            public TrackNumberFormatConverter(Type type) : base(type) { }

            public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
            {
                if (destinationType == typeof(string))
                {
                    return dict[(TrackNumberFormats)value];
                }
                return base.ConvertTo(context, culture, value, destinationType);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                if (value is string)
                {
                    return dict.First(_ => _.Value == (string)value).Key;
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        private SpectrumModes spectrumMode = SpectrumModes.Mode0;
        [Description("スペクトラムアナライザ描画モード\n0～4")]
        [DefaultValue(SpectrumModes.Mode0)]
        [Category("Spectrum Analyzer")]
        public SpectrumModes SpectrumMode
        {
            get
            {
                return spectrumMode;
            }
            set
            {
                spectrumMode = value;
            }
        }

        private bool _FFTLogarithmic = false;
        [Description("スペクトラムアナライザで横軸を対数にする")]
        [DefaultValue(false)]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [Category("Spectrum Analyzer")]
        public bool FFTLogarithmic
        {
            get
            {
                return _FFTLogarithmic;
            }
            set
            {
                _FFTLogarithmic = value;
            }
        }

        private FFTNum _FFTNum = FFTNum.FFT1024;
        [Description("FFTの細かさ")]
        [DefaultValue(FFTNum.FFT1024)]
        [Category("Spectrum Analyzer")]
        public FFTNum FFTNumber
        {
            get
            {
                return _FFTNum;
            }
            set
            {
                _FFTNum = value;
            }
        }

        private Color color1 = Color.Orange;
        [Description("Color1")]
        [Category("Spectrum Analyzer")]
        public Color SpectrumColor1
        {
            get
            {
                return color1;
            }
            set
            {
                color1 = value;
            }
        }

        private Color color2 = SystemColors.Control;
        [Description("Color2")]
        [Category("Spectrum Analyzer")]
        public Color SpectrumColor2
        {
            get
            {
                return color2;
            }
            set
            {
                color2 = value;
            }
        }


        private Font font_trackInfoView;
        [Description("再生中の曲名のフォント")]
        [Category("TrackInfo View")]
        public Font Font_trackInfoView
        {
            get
            {
                return font_trackInfoView;
            }
            set
            {
                if (value != null)
                {
                    font_trackInfoView = value;
                }
            }
        }

        private Font font_playlistView;
        [Description("プレイリストのフォント")]
        [Category("Playlist View")]
        public Font Font_playlistView
        {
            get
            {
                return font_playlistView;
            }
            set
            {
                if (value != null)
                {
                    font_playlistView = value;
                }
            }
        }

        private int playlistViewLineHeightAdjustment = 0;
        [Description("プレイリストの行間調整")]
        [DefaultValue(0)]
        [TypeConverter(typeof(LineHeightAdjustmentConverter))]
        [Category("Playlist View")]
        public int PlaylistViewLineHeightAdjustment
        {
            get
            {
                return playlistViewLineHeightAdjustment;
            }
            set
            {
                playlistViewLineHeightAdjustment = value;
            }
        }

        private Boolean showCoverArtInPlaylistView = true;
        [Description("プレイリストにカバーアートを表示する")]
        [DefaultValue(true)]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [Category("Playlist View")]
        public Boolean ShowCoverArtInPlaylistView
        {
            get
            {
                return showCoverArtInPlaylistView;
            }
            set
            {
                showCoverArtInPlaylistView = value;
            }
        }

        private Boolean showGroup = true;
        [Description("グループ表示")]
        [DefaultValue(true)]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [Category("Playlist View")]
        public Boolean ShowGroup
        {
            get
            {
                return showGroup;
            }
            set
            {
                showGroup = value;
            }
        }

        private Boolean showVerticalGrid = true;
        [Description("カラム区切りを表示")]
        [DefaultValue(true)]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [Category("Playlist View")]
        public Boolean ShowVerticalGrid
        {
            get
            {
                return showVerticalGrid;
            }
            set
            {
                showVerticalGrid = value;
            }
        }

        private int coverArtSizeInPlaylistView = 80;
        [Description("プレイリストに表示するカバーアートのサイズ")]
        [DefaultValue(80)]
        [Category("Playlist View")]
        public int CoverArtSizeInPlaylistView
        {
            get
            {
                return coverArtSizeInPlaylistView;
            }
            set
            {
                if (value < 0) value = 0;
                if (value > 200) value = 200;
                coverArtSizeInPlaylistView = value;
            }
        }


        private bool coloredAlbum = true;
        [Description("アルバムごとに色分けする\n適当")]
        [DefaultValue(true)]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [Category("Playlist View")]
        public bool ColoredAlbum
        {
            get
            {
                return coloredAlbum;
            }
            set
            {
                coloredAlbum = value;
            }
        }

        private TrackNumberFormats trackNumberFormat = TrackNumberFormats.NofM;
        [Description("トラックナンバーの書式")]
        [DefaultValue(TrackNumberFormats.NofM)]
        [Category("Playlist View")]
        [TypeConverter(typeof(TrackNumberFormatConverter))]
        public TrackNumberFormats TrackNumberFormat
        {
            get { return trackNumberFormat; }
            set { trackNumberFormat = value; }
        }

        private bool useMediaKey = false;
        [Description("マルチメディアキーを使用する")]
        [DefaultValue(false)]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [Category("Hotkey")]
        public bool UseMediaKey
        {
            get
            {
                return useMediaKey;
            }
            set
            {
                useMediaKey = value;
            }
        }

        private Keys hotkey_NextTrack = Keys.None;
        [Description("次の曲")]
        [DefaultValue(Keys.None)]
        [Category("Hotkey")]
        public Keys Hotkey_NextTrack
        {
            get
            {
                return hotkey_NextTrack;
            }
            set
            {
                hotkey_NextTrack = value;
            }
        }

        private Keys hotkey_PrevTrack = Keys.None;
        [Description("前の曲")]
        [DefaultValue(Keys.None)]
        [Category("Hotkey")]
        public Keys Hotkey_PrevTrack
        {
            get
            {
                return hotkey_PrevTrack;
            }
            set
            {
                hotkey_PrevTrack = value;
            }
        }

        private Keys hotkey_PlayPause = Keys.None;
        [Description("再生/一時停止")]
        [DefaultValue(Keys.None)]
        [Category("Hotkey")]
        public Keys Hotkey_PlayPause
        {
            get
            {
                return hotkey_PlayPause;
            }
            set
            {
                hotkey_PlayPause = value;
            }
        }

        private Keys hotkey_Stop = Keys.None;
        [Description("停止")]
        [DefaultValue(Keys.None)]
        [Category("Hotkey")]
        public Keys Hotkey_Stop
        {
            get
            {
                return hotkey_Stop;
            }
            set
            {
                hotkey_Stop = value;
            }
        }

        private string nowPlayingFormat;
        [Description("Now Playingツイートの書式")]
        [DefaultValue(DefaultUIForm.DefaultNowPlayingFormat)]
        public string NowPlayingFormat
        {
            get { return nowPlayingFormat; }
            set { nowPlayingFormat = string.IsNullOrEmpty(value) ? DefaultUIForm.DefaultNowPlayingFormat : value; }
        }

        private bool hideIntoTrayOnMinimize = false;
        [Description("最小化時にタスクトレイに収納する")]
        [DefaultValue(false)]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [Category("TaskTray")]
        public bool HideIntoTrayOnMinimize
        {
            get { return hideIntoTrayOnMinimize; }
            set { hideIntoTrayOnMinimize = value; }
        }

        private bool showNotifyBalloon = true;
        [Browsable(false)]
        public bool ShowNotifyBalloon
        {
            get { return showNotifyBalloon; }
            set { showNotifyBalloon = value; }
        }

        /// <summary>
        /// playlistviewに表示するcolumnを定義
        /// </summary>
        private string[] displayColumns = null; // DBCol.infoCodec, DBCol.infoCodec_sub, DBCol.modify, DBCol.statChannels, DBCol.statSamplingrate
        [Browsable(false)]
        public string[] DisplayColumns
        {
            get { return displayColumns; }
            set { displayColumns = value; }
        }

        private Dictionary<string, int> playlistViewColumnOrder = new Dictionary<string, int>();
        [Browsable(false)]
        public Dictionary<string, int> PlaylistViewColumnOrder {
            get { return playlistViewColumnOrder; }
            set { playlistViewColumnOrder = value; }
        }

        private Dictionary<string, int> playlistViewColumnWidth = new Dictionary<string, int>();
        [Browsable(false)]
        public Dictionary<string, int> PlaylistViewColumnWidth
        {
            get { return playlistViewColumnWidth; }
            set { playlistViewColumnWidth = value; }
        }

        [Browsable(false)]
        public Point WindowLocation { get; set; }

        [Browsable(false)]
        public Size WindowSize { get; set; }

        [Browsable(false)]
        public FormWindowState WindowState { get; set; }

        [Browsable(false)]
        public int? splitContainer1_SplitterDistance { get; set; }

        [Browsable(false)]
        public int? splitContainer2_SplitterDistance { get; set; }

        private int splitContainer3_SplitterDistance = 120;
        [Browsable(false)]
        public int SplitContainer3_SplitterDistance {
            get { return splitContainer3_SplitterDistance; }
            set { splitContainer3_SplitterDistance = value; }
        }

        [Browsable(false)]
        public string LibraryLatestDir { get; set; }

        public DefaultUIPreference(Dictionary<string, object> setting)
            : base(setting)
        {
        }

        public DefaultUIPreference()
        {
        }
    }
}
