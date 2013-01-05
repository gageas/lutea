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
    [LuteaComponentInfo("Core", "Gageas", 0.140, "アプリケーションのコア")]
    public class CoreComponent : LuteaComponentInterface
    {
        private const string PseudoDeviceNameForDefaultOutput = "(Default)";
        public class Preference
        {
            bool enableReplayGain = AppCore.EnableReplayGain;
            [Category("ReplayGain")]
            [Description("Replaygainを有効にする")]
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
            double replaygainGainBoost = AppCore.ReplaygainGainBoost;
            [Category("ReplayGain")]
            [Description("Replaygainが付与されたトラックのプリアンプ(dB)")]
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
            double noreplaygainGainBoost = AppCore.NoReplaygainGainBoost;
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

            bool enableWASAPIExclusive = AppCore.enableWASAPIExclusive;
            [Category("Output")]
            [Description("WASAPIで排他モードを使用する\n※ 停止後に反映されます")]
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
            bool fadeInOutOnSkip = AppCore.fadeInOutOnSkip;
            [Category("Output")]
            [Description("曲間のプチノイズ対策にフェードインを利用する\n曲間のノイズが気になる場合有効にしてください(非WASAPI時のみ有効)")]
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

            string preferredDeviceName = AppCore.preferredDeviceName;
            string[] devlist = GetDeviceNameListForSetting();
            [TypeConverter(typeof(OutputDeviceConverter))]
            [Category("Output")]
            [Description("出力デバイス選択")]
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
            bool useMigemo = AppCore.UseMigemo;
            [Category("Query")]
            [Description("あいまい検索にMigemoを使う（検索のレスポンスが遅くなります）")]
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
        private static string[] GetDeviceNameListForSetting() {
            return BASS.GetDevices().Select(((_, i) => i == 0 ? PseudoDeviceNameForDefaultOutput : _.Name)).ToArray();
        }

        private void ParseSetting(Dictionary<string,object> setting)
        {
            Util.Util.TryAll(new Controller.VOIDVOID[] { 
                ()=>AppCore.PlaylistSortColumn = (string)setting["PlaylistSortColumn"],
                ()=>AppCore.PlaylistSortOrder = (Controller.SortOrders)setting["PlaylistSortOrder"],
                ()=>Controller.playbackOrder = (Controller.PlaybackOrder)setting["PlaybackOrder"],
                ()=>AppCore.volume = (float)setting["Volume"],
                ()=>AppCore.ReplaygainGainBoost = (double)setting["ReplaygainGainBoost"],
                ()=>AppCore.NoReplaygainGainBoost = (double)setting["NoReplaygainGainBoost"],
                ()=>AppCore.createPlaylist((string)setting["latestPlaylistQuery"]),
                ()=>AppCore.enableWASAPIExclusive = (bool)setting["enableWASAPIExclusive"],
                ()=>AppCore.fadeInOutOnSkip = (bool)setting["fadeInOutOnSkip"],
                ()=>AppCore.preferredDeviceName = (string)setting["preferredDeviceName"],
                ()=>AppCore.UseMigemo = (bool)setting["useMigemo"],
            }, null);
        }
        public void Init(object setting)
        {
            if (setting != null)
            {
                ParseSetting((Dictionary<string, object>)setting);
            }
        }

        public object GetSetting()
        {
            var setting = new Dictionary<string, object>();
            setting["PlaylistSortColumn"] = AppCore.PlaylistSortColumn;
            setting["PlaylistSortOrder"] = AppCore.PlaylistSortOrder;
            setting["PlaybackOrder"] = AppCore.playbackOrder;
            setting["Volume"] = AppCore.volume;
            setting["ReplaygainGainBoost"] = AppCore.ReplaygainGainBoost;
            setting["NoReplaygainGainBoost"] = AppCore.NoReplaygainGainBoost;
            setting["latestPlaylistQuery"] = AppCore.latestPlaylistQuery;
            setting["enableWASAPIExclusive"] = AppCore.enableWASAPIExclusive;
            setting["fadeInOutOnSkip"] = AppCore.fadeInOutOnSkip;
            setting["preferredDeviceName"] = AppCore.preferredDeviceName;
            setting["useMigemo"] = AppCore.UseMigemo;
            return setting;
        }


        public object GetPreferenceObject()
        {
            return new Preference();
        }

        public void SetPreferenceObject(object _pref)
        {
            Preference pref = (Preference)_pref;
            AppCore.EnableReplayGain = pref.EnableReplayGain;
            AppCore.ReplaygainGainBoost = pref.ReplaygainGainBoost;
            AppCore.NoReplaygainGainBoost = pref.NoReplaygainGainBoost;
            AppCore.enableWASAPIExclusive = pref.EnableWASAPIExclusive;
            AppCore.fadeInOutOnSkip = pref.FadeInOutOnSkip;
            AppCore.UseMigemo = pref.UseMigemo;
            AppCore.preferredDeviceName = pref.PreferredDeviceName == PseudoDeviceNameForDefaultOutput ? "" : pref.PreferredDeviceName;
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
