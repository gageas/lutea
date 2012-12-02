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

    public class DefaultUIPreference
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
        bool _FFTLogarithmic;

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
        /// PropertyGridに行間調整用intを表示するTypeConverter
        /// </summary>
        class LineHeightAdjustmentConverter : Int32Converter
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

        private SpectrumModes spectrumMode;
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

        [Description("スペクトラムアナライザで横軸を対数にする")]
        [DefaultValue(false)]
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

        private FFTNum _FFTNum;
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

        private Color color1;
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

        private Color color2;
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
                font_trackInfoView = value;
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
                font_playlistView = value;
            }
        }

        private int playlistViewLineHeightAdjustment;
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

        private Boolean showCoverArtInPlaylistView;
        [Description("プレイリストにカバーアートを表示する")]
        [DefaultValue(true)]
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


        private int coverArtSizeInPlaylistView;
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
                coverArtSizeInPlaylistView = value;
            }
        }


        private bool coloredAlbum;
        [Description("アルバムごとに色分けする\n適当")]
        [DefaultValue(true)]
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

        private bool useMediaKey;
        [Description("マルチメディアキーを使用する")]
        [DefaultValue(false)]
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

        private Keys hotkey_NextTrack;
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

        private Keys hotkey_PrevTrack;
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

        private Keys hotkey_PlayPause;
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

        private Keys hotkey_Stop;
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
        private DefaultUIForm form;
        public DefaultUIPreference(DefaultUI.DefaultUIForm form)
        {
            this.form = form;
        }
    }
}
