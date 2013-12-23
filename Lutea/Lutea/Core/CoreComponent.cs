using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

using System.ComponentModel;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// Coreの設定を保持するためのダミーComponent
    /// </summary>
    [GuidAttribute("2A21DCC8-B023-4989-A663-061319D25E26")]
    [LuteaComponentInfo("Core", "Gageas", 0.160, "アプリケーションのコア")]
    public class CoreComponent : LuteaComponentInterface
    {
        private const string PseudoDeviceNameForDefaultOutput = "(Default)";

        [Serializable]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public class ImportTypeSelector
        {
            public override string ToString()
            {
                var enabledProps = this.GetType().GetProperties().Where(prop => (bool)prop.GetValue(this, null));
                if(enabledProps.Count() == 0){
                    return "";
                }else{
                    return enabledProps.Select(_ => _.Name).Aggregate((x, y) => x + ", " + y);
                }
            }

            class ImportableTypeMappingAttr : Attribute {
                public Library.Importer.ImportableTypes map;
                public ImportableTypeMappingAttr(Library.Importer.ImportableTypes map)
                {
                    this.map = map;
                }
            }

            public ImportTypeSelector()
            {
                var props = this.GetType().GetProperties();
                foreach (var prop in props)
                {
                    var defVal = (DefaultValueAttribute[])prop.GetCustomAttributes(typeof(DefaultValueAttribute), false);
                    prop.SetValue(this, defVal[0].Value, null);
                }
            }

            public Library.Importer.ImportableTypes ToEnum()
            {
                Library.Importer.ImportableTypes result = 0;
                var props = this.GetType().GetProperties();
                foreach (var prop in props)
                {
                    var value = (bool)prop.GetValue(this, null);
                    if (value)
                    {
                        var mapVal = (ImportableTypeMappingAttr[])prop.GetCustomAttributes(typeof(ImportableTypeMappingAttr), false);
                        result |= mapVal[0].map;
                    }
                }
                return result;
            }

            public void FromEnum(Library.Importer.ImportableTypes typeEnum)
            {
                var props = this.GetType().GetProperties();
                foreach (var prop in props)
                {
                    var mapVal = (ImportableTypeMappingAttr[])prop.GetCustomAttributes(typeof(ImportableTypeMappingAttr), false);
                    prop.SetValue(this, (typeEnum & mapVal[0].map) != 0, null);
                }
            }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.MP2)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool mp2 { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.MP3)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool mp3 { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.MP4)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool mp4 { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.M4A)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [Description("一般のm4aファイル")]
            [DefaultValue(true)]
            public bool m4a_others { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.M4AiTunes)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [Description("iTunes Storeで購入したm4aファイル")]
            [DefaultValue(true)]
            public bool m4a_iTunesStore { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.OGG)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool ogg { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.WMA)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool wma { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.ASF)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool asf { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.FLAC)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool flac { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.TTA)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool tta { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.APE)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool ape { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.WV)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool wv { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.TAK)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool tak { get; set; }

            [ImportableTypeMappingAttr(Library.Importer.ImportableTypes.CUE)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [DefaultValue(true)]
            public bool cue { get; set; }
        }

        bool enableReplayGain = true;
        [Category("ReplayGain")]
        [Description("Replaygainを有効にする")]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [DefaultValue(true)]
        public bool EnableReplayGain
        {
            get
            {
                return enableReplayGain;
            }
            set
            {
                enableReplayGain = value;
            }
        }
        /// <summary>
        /// Replaygainが付与されたトラックのプリアンプ
        /// </summary>
        double replaygainGainBoost = 5.0;
        [Category("ReplayGain")]
        [Description("Replaygainが付与されたトラックのプリアンプ(dB)")]
        [DefaultValue(5.0)]
        public double ReplaygainGainBoost
        {
            get
            {
                return replaygainGainBoost;
            }
            set
            {
                replaygainGainBoost = value;
            }
        }

        /// <summary>
        /// Replaygainが付与されていないトラックのプリアンプ
        /// </summary>
        double noreplaygainGainBoost = 0.0;
        [Category("ReplayGain")]
        [Description("Replaygainが付与されていないトラックのプリアンプ(dB)")]
        [DefaultValue(0.0)]
        public double NoReplaygainGainBoost
        {
            get
            {
                return noreplaygainGainBoost;
            }
            set
            {
                noreplaygainGainBoost = value;
            }
        }

        bool enableWASAPIExclusive = true;
        [Category("Output")]
        [Description("WASAPIで排他モードを使用する\n※ 停止後に反映されます")]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [DefaultValue(true)]
        public bool EnableWASAPIExclusive
        {
            get
            {
                return enableWASAPIExclusive;
            }
            set
            {
                enableWASAPIExclusive = value;
            }
        }

        /// <summary>
        /// 曲間プチノイズ対策
        /// </summary>
        bool fadeInOutOnSkip = false;
        [Category("Output")]
        [Description("曲間のプチノイズ対策にフェードインを利用する\n曲間のノイズが気になる場合有効にしてください(非WASAPI時のみ有効)")]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [DefaultValue(false)]
        public bool FadeInOutOnSkip
        {
            get
            {
                return fadeInOutOnSkip;
            }
            set
            {
                fadeInOutOnSkip = value;
            }
        }

        /// <summary>
        /// バッファサイズ
        /// </summary>
        uint bufferLength = 500;
        [Category("Output")]
        [Description("出力バッファサイズ(ms)\nWASAPI使用時に適用されます。\n0で自動設定になります\n※ 停止後に反映されます")]
        [DefaultValue(500)]
        public int BufferLength
        {
            get
            {
                return (int)bufferLength;
            }
            set
            {
                if ((value < 0) || (value > 1000 * 10))
                {
                    bufferLength = 0;
                    return;
                }
                bufferLength = (uint)value;
            }
        }

        string preferredDeviceName = "";
        string[] devlist = GetDeviceNameListForSetting();
        [TypeConverter(typeof(OutputDeviceConverter))]
        [Category("Output")]
        [Description("出力デバイス選択")]
        [DefaultValue("(Default)")]
        public String PreferredDeviceName
        {
            get
            {
                if (devlist.Any(_ => _ == preferredDeviceName))
                {
                    return devlist.First(_ => _ == preferredDeviceName);
                }
                else
                {
                    return devlist[0];
                }
            }
            set
            {
                if (devlist.Any(_ => _ == value))
                {
                    preferredDeviceName = devlist.First(_ => _ == value);
                }
                else
                {
                    preferredDeviceName = devlist[0];
                }
            }
        }

        /// <summary>
        /// Migemo有効・無効
        /// </summary>
        bool useMigemo = true;
        [Category("Query")]
        [Description("あいまい検索にMigemoを使う（検索のレスポンスが遅くなります）")]
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [DefaultValue(true)]
        public bool UseMigemo
        {
            get
            {
                return useMigemo;
            }
            set
            {
                useMigemo = value;
            }
        }

        [Browsable(false)]
        public string PlaylistSortColumn { get; set; }

        private Controller.SortOrders playlistSortOrder = Controller.SortOrders.Asc;
        [Browsable(false)]
        public Controller.SortOrders PlaylistSortOrder
        {
            get
            {
                return playlistSortOrder;
            }
            set
            {
                playlistSortOrder = value;
            }
        }

        [Browsable(false)]
        public Controller.PlaybackOrder PlaybackOrder { get; set; }

        float volume = 1.0F;
        [Browsable(false)]
        internal float Volume
        {
            get
            {
                return volume;
            }
            set
            {
                volume = value;
            }
        }

        string latestPlaylistQuery = "SELECT * FROM list;";
        [Browsable(false)]
        public string LatestPlaylistQuery
        {
            get
            {
                return latestPlaylistQuery;
            }
            set
            {
                latestPlaylistQuery = value;
            }
        }

        ImportTypeSelector importTypes = new ImportTypeSelector();
        [Description("ライブラリに取り込むファイル種別")]
        [Category("Library")]
        public ImportTypeSelector ImportTypes
        {
            get { return importTypes; }
            set { if(value != null)importTypes = value; }
        }

        /// <summary>
        /// PropertyGridにデバイス一覧を表示するためのConverter
        /// </summary>
        class OutputDeviceConverter : StringConverter
        {
            private string[] devlist;
            public OutputDeviceConverter()
            {
                devlist = GetDeviceNameListForSetting();
            }

            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return true;
            }
            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(devlist);
            }
            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
            {
                return true;
            }
        }

        /// <summary>
        /// 出力デバイスとして選択可能なデバイスの名前のリストを取得する
        /// </summary>
        /// <returns></returns>
        private static string[] GetDeviceNameListForSetting()
        {
            return BASS.GetDevices().Select(((_, i) => i == 0 ? PseudoDeviceNameForDefaultOutput : _.Name)).ToArray();
        }

        private void ParseSetting(List<KeyValuePair<string, object>> setting, bool BrowsableOnly = false)
        {
            var props = this.GetType().GetProperties().Where(_ => _.CanRead && _.CanWrite).Where(_ => setting.Exists(__ => __.Key == _.Name));

            foreach (var prop in props)
            {
                if (BrowsableOnly)
                {
                    var browsableAttr = prop.GetCustomAttributes(typeof(BrowsableAttribute), false);
                    if (browsableAttr.Length > 0)
                    {
                        if (!((BrowsableAttribute)(browsableAttr[0])).Browsable)
                        {
                            continue;
                        }
                    }
                }

                try
                {
                    prop.SetValue(this, setting.First(_ => _.Key == prop.Name).Value, null);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        public void Init(object setting)
        {
            if (setting != null)
            {
                ParseSetting((List<KeyValuePair<string, object>>)setting);
            }
        }

        private List<KeyValuePair<string, object>> ToSettingsList()
        {
            return this.GetType().GetProperties().Where(_ => _.CanRead && _.CanWrite).Select(_ => new KeyValuePair<string, object>(_.Name, _.GetValue(this, null))).ToList();
        }

        public object GetSetting()
        {
            return this.ToSettingsList();
        }

        public object GetPreferenceObject()
        {
            var tmp = new CoreComponent();
            tmp.SetPreferenceObject(this);
            return tmp;
        }

        public void SetPreferenceObject(object _pref)
        {
            if (_pref != null && _pref is CoreComponent)
            {
                var pref = (CoreComponent)_pref;
                this.ParseSetting(pref.ToSettingsList(), true);
            }
        }

        public void Quit()
        {
        }

        public bool CanSetEnable()
        {
            return false;
        }

        public void SetEnable(bool enable)
        {
        }

        public bool GetEnable()
        {
            return true;
        }
    }
}
