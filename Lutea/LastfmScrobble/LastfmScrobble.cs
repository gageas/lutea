using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;
using Gageas.Lutea;
using Gageas.Lutea.Core;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.LastfmScrobble
{
    [GuidAttribute("7A719D08-2C82-4A4F-9B33-2CC83B41BDB3")]
    [LuteaComponentInfo("Last.fm Scrobble", "Gageas", 1.2, "Last.fm Scrobble")]
    public class LastfmScrobble : Lutea.Core.LuteaComponentInterface
    {
        internal Preference pref = new Preference(null);
        private ILastfmClient sva;

        private bool scrobbed = false;
        private double currentDuration;

        public void Init(object _setting)
        {
            Controller.onTrackChange += (i) => {
                scrobbed = false;
                currentDuration = Controller.Current.Length;
                if (sva != null) sva.OnTrackChange(i);
            };
            Controller.onPause += () => { if (sva != null) sva.OnPause(); };
            Controller.onResume += () => { if (sva != null) sva.OnResume(); };
            Controller.onElapsedTimeChange += onElapsedTimeChange;

            if (_setting != null)
            {
                var setting = (Dictionary<string, object>)_setting;
                pref = new Preference(setting);
            }
            ResetClient();
        }

        private void onElapsedTimeChange(int time)
        {
            if (!pref.ScrobbleEnabled) return;
            if (currentDuration < pref.IgnoreShorterThan) return;
            if (scrobbed) return;
            if (time > (currentDuration * pref.ScrobbleThreshold / 100.0))
            {
                scrobbed = true;
                if (sva != null) { sva.OnExceedThresh(); }
            }
        }

        public object GetSetting()
        {
            var dict = pref.ToDictionary();
            dict.Remove("Password");
            return dict;
        }

        public object GetPreferenceObject()
        {
            var clone = pref.Clone<Preference>();
            clone.password = "";
            return clone;
        }

        public void SetPreferenceObject(object _pref)
        {
            if (_pref == null) return;
            pref = (Preference)_pref;
            ResetClient();
        }

        public void Quit()
        {
            if (sva != null)
            {
                sva.Dispose();
                sva = null;
            }
        }

        public bool CanSetEnable()
        {
            return true;
        }

        public void SetEnable(bool enable)
        {
            this.pref.ScrobbleEnabled = enable;
            ResetClient();
        }

        public bool GetEnable()
        {
            return this.pref.ScrobbleEnabled;
        }

        private void ResetClient()
        {
            if (sva != null)
            {
                try
                {
                    sva.Dispose();
                }
                catch (Exception ex) { Logger.Error(ex); }
                finally
                {
                    sva = null;
                }
            }
            switch (pref.ScrobbleMethod)
            {
                case Preference.ScrobbleMethods.Standalone:
                    if ((pref.Username != null && pref.password != null && pref.Username != "" && pref.password != "") || pref.Username != this.pref.Username)
                    {
                        pref.AuthToken = "";
                        try
                        {
                            var lastfm = new LuteaLastfm();
                            var result = lastfm.Auth_getMobileSession(pref.Username, pref.password);
                            if (result)
                            {
                                pref.AuthToken = Lastfm.GenAuthToken(pref.Username, pref.password);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e);
                        }
                    }
                    if (pref.ScrobbleEnabled) sva = new StandaloneLastfmClient(this);
                    break;
                case Preference.ScrobbleMethods.ViaLastfmApp:
                    if (pref.ScrobbleEnabled) sva = new ViaAppLastfmClient();
                    break;
            }
        }

        public class Preference : LuteaPreference
        {
            public enum ScrobbleMethods
            {
                Standalone = 0,
                ViaLastfmApp = 1,
            }
            private readonly string[] Sortorder = 
            { 
                "ScrobbleMethod",
                "ScrobbleEnabled", 
                "UpdateNowPlayingEnabled", 
                "Username", 
                "Password", 
                "Authenticated"
            };
            private const string HiddenPassword = "********";

            public uint ignoreShorterThan = 30;
            public uint scrobbleThreshold = 50;
            public string password = "";
            public string password_disp;

            public Preference()
            {
            }

            public Preference(Dictionary<string, object> setting) : base(setting)
            {
                this.password = "";
                this.password_disp = Authenticated ? HiddenPassword : "";
            }

            public override string[] GetSortOrder()
            {
                return Sortorder;
            }

            [Browsable(false)]
            public string AuthToken
            {
                get;
             
                internal set;
            }

            [Category("\tMethod")]
            [DefaultValue(ScrobbleMethods.Standalone)]
            [Description("Scrobbleの方法を選択します。\nStandaloneではLuteaから直接last.fmサーバにScrobbleを送信します。\nViaLastfmAppではLast.fm Scrobblerアプリを介してScrobbleを送信します。\nViaLastfmAppの場合，以下の設定は不要です(Last.fm Scrobbleアプリにて設定が必要です)")]
            public ScrobbleMethods ScrobbleMethod
            {
                get;
                set;
            }

            [Category("\t\tEnable")]
            [DefaultValue(false)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [Description("Scrobbleを有効にする")]
            public bool ScrobbleEnabled
            {
                get;
                set;
            }

            [Category("Option")]
            [DefaultValue(false)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [Description("NowPlayingの更新を有効にする")]
            public bool UpdateNowPlayingEnabled
            {
                get;
                set;
            }

            [Category("Option")]
            [DefaultValue(30)]
            [Description("これより短いトラックはscrobbleしない(秒)")]
            public uint IgnoreShorterThan
            {
                get
                {
                    return (uint)ignoreShorterThan;
                }
                set
                {
                    if (value > 0)
                    {
                        this.ignoreShorterThan = (uint)value;
                    }
                }
            }

            [Category("Option")]
            [DefaultValue(50)]
            [Description("これ以上再生したらscrobbleする(%) 1-99")]
            public uint ScrobbleThreshold
            {
                get
                {
                    return scrobbleThreshold;
                }
                set
                {
                    if ((value > 0) && (value < 100))
                    {
                        this.scrobbleThreshold = value;
                    }
                }
            }

            [Category("Auth")]
            [Description("ユーザ名")]
            public string Username
            {
                get;
                set;
            }

            [Category("Auth")]
            [DefaultValue(false)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [Description("認証に成功したかどうか")]
            public bool Authenticated
            {
                get
                {
                    return !string.IsNullOrEmpty(this.AuthToken);
                }
            }

            [Category("Auth")]
            [DefaultValue(HiddenPassword)]
            [Description("アカウントの情報を変更する場合のみ入力します\n ※パスワードはハッシュ化して保存されます")]
            public string Password
            {
                get
                {
                    return password_disp;
                }
                set
                {
                    this.password = value;
                    this.password_disp = HiddenPassword;
                }
            }
        }
    }
}
