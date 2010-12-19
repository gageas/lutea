using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

using System.ComponentModel;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// Coreの設定を保持するためのダミーComponent
    /// </summary>
    [GuidAttribute("2A21DCC8-B023-4989-A663-061319D25E26")]
    [LuteaComponentInfo("Core","Gageas",0.08, "アプリケーションのコア")]
    public class CoreComponent : LuteaComponentInterface
    {
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

            bool enableWASAPIVolume = AppCore.enableWASAPIVolume;
            [Category("Output")]
            [Description("WASAPIでボリュームコントロールを使用する\n※ OSのボリューム設定を操作してしまいワケ分からなくなるのでfalse推奨")]
            [DefaultValue(false)]
            public bool EnableWASAPIVolume
            {
                get
                {
                    return enableWASAPIVolume;
                }
                set
                {
                    enableWASAPIVolume = value;
                }
            }

            public enum Freqs : uint
            {
                Freq_44100 = 44100,
                Freq_48000 = 48000,
                Freq_96000 = 96000,
                Freq_192000 = 192000
            }
            /// <summary>
            /// 出力周波数
            /// </summary>
            uint outputFreq = AppCore.OutputFreq;
            [Category("Output")]
            [Description("出力周波数(Hz)\n※ 停止後に反映されます")]
            [DefaultValue(Freqs.Freq_44100)]
            public Freqs OutputFreq
            {
                get
                {
                    return (Freqs)outputFreq;
                }
                set
                {
                    outputFreq = (uint)value;
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
        }
        private void ParseSetting(Dictionary<string,object> setting)
        {
            Util.Util.TryAll(new Controller.VOIDVOID[] { 
                ()=>Controller.playbackOrder = (Controller.PlaybackOrder)setting["PlaybackOrder"],
                ()=>AppCore.volume = (float)setting["Volume"],
                ()=>AppCore.ReplaygainGainBoost = (double)setting["ReplaygainGainBoost"],
                ()=>AppCore.NoReplaygainGainBoost = (double)setting["NoReplaygainGainBoost"],
                ()=>AppCore.createPlaylist((string)setting["latestPlaylistQuery"]),
                ()=>AppCore.enableWASAPIExclusive = (bool)setting["enableWASAPIExclusive"],
                ()=>AppCore.enableWASAPIVolume = (bool)setting["enableWASAPIVolume"],
                ()=>AppCore.OutputFreq = (uint)setting["outputFreq"],
                ()=>AppCore.fadeInOutOnSkip = (bool)setting["fadeInOutOnSkip"],
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
            setting["PlaybackOrder"] = AppCore.playbackOrder;
            setting["Volume"] = AppCore.volume;
            setting["ReplaygainGainBoost"] = AppCore.ReplaygainGainBoost;
            setting["NoReplaygainGainBoost"] = AppCore.NoReplaygainGainBoost;
            setting["latestPlaylistQuery"] = AppCore.latestPlaylistQuery;
            setting["enableWASAPIExclusive"] = AppCore.enableWASAPIExclusive;
            setting["enableWASAPIVolume"] = AppCore.enableWASAPIVolume;
            setting["outputFreq"] = AppCore.OutputFreq;
            setting["fadeInOutOnSkip"] = AppCore.fadeInOutOnSkip;
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
            AppCore.enableWASAPIVolume = pref.EnableWASAPIVolume;
            AppCore.OutputFreq = (uint)pref.OutputFreq;
            AppCore.fadeInOutOnSkip = pref.FadeInOutOnSkip;
        }

        public void Quit()
        {
        }
    }
}
