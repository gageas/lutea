﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using System.ComponentModel;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// Coreの設定を保持するためのダミーComponent
    /// </summary>
    [GuidAttribute("2A21DCC8-B023-4989-A663-061319D25E26")]
    [LuteaComponentInfo("Core", "Gageas", 1.110, "アプリケーションのコア")]
    public class CoreComponent : LuteaComponentInterface
    {
        private const string PseudoDeviceNameForDefaultOutput = "(Default)";

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
        /// バッファサイズ
        /// </summary>
        uint bufferLength = 0;
        [Category("Output")]
        [Description("出力バッファサイズ(ms)\nWASAPI使用時に適用されます。\n0で自動設定になります\n※ 停止後に反映されます")]
        [DefaultValue(0)]
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


        bool usePrescan;
        [TypeConverter(typeof(BooleanYesNoTypeConverter))]
        [Category("Input")]
        [Description("MP2, MP3, 一部のOGGファイルのシークの精度を向上します。\nファイルの読み込みが多少遅くなります。\nギャップレス再生がうまく繋がらず気になる場合のみ有効にしてみてください。")]
        [DefaultValue(false)]
        public bool UsePrescan
        {
            get
            {
                return usePrescan;
            }
            set
            {
                usePrescan = value;
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

        private string playlistSortColumn;
        [Browsable(false)]
        public string PlaylistSortColumn
        {
            get
            {
                if (Controller.GetColumnIndexByName(playlistSortColumn) == -1)
                {
                    return null;
                }
                else
                {
                    return playlistSortColumn;
                }
            }
            set
            {
                if (Controller.GetColumnIndexByName(value) == -1)
                {
                    playlistSortColumn = null;
                }
                else
                {
                    playlistSortColumn = value;
                }
            }
        }

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
        public float Volume
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

        private Library.Importer.ImportableTypes importTypes = Library.Importer.AllImportableTypes;
        [Description("ライブラリに取り込むファイル種別")]
        [Category("Library")]
        [Editor(typeof(FileTypeUITypeEditor), typeof(UITypeEditor))]
        [DefaultValue(Library.Importer.AllImportableTypes)]
        public Library.Importer.ImportableTypes ImportTypes
        {
            get
            {
                return importTypes;
            }
            set
            {
                importTypes = value;
            }
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

        public class FileTypeUITypeEditor : UITypeEditor
        {
            public override System.Drawing.Design.UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            {
                return System.Drawing.Design.UITypeEditorEditStyle.DropDown;
            }
            public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
            {
                IWindowsFormsEditorService edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
                if (edSvc == null) return value;
                EnumFlagsUITypeEdotorEditControl mpc = new EnumFlagsUITypeEdotorEditControl(typeof(Library.Importer.ImportableTypes), (int)value);
                edSvc.DropDownControl(mpc);
                return mpc.Value;
            }
        }
    }
}
