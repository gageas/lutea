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
    public class MyTypeConverter : TypeConverter
    {
        private readonly string[] sortorder = { "ScrobbleEnabled", "Username", "Password" };

        public MyTypeConverter()
        {
        }

        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            var pdc = TypeDescriptor.GetProperties(value, attributes);
            return pdc.Sort(sortorder);
            //                return base.GetProperties(context, value, attributes);
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext context)
        {
            return true;
        }
    }

    [GuidAttribute("7A719D08-2C82-4A4F-9B33-2CC83B41BDB3")]
    [LuteaComponentInfo("Last.fm Scrobble", "Gageas", 1.0, "Last.fm Scrobble")]
    public class LastfmScrobble : Lutea.Core.LuteaComponentInterface
    {
        private bool enabled = false;

        private Lastfm lastfm = null;

        private string username = null;
        private string authToken = null;

        private bool scrobbed = false;
        private double currentDuration;

        private uint ignoreShorterThan = 30;
        private uint scrobbleThreshold = 50;

        public void Init(object _setting)
        {
            lastfm = Lastfm.GetLuteaLastfmInstance();
            Controller.onTrackChange += (i)=>{ 
                scrobbed = false; 
                currentDuration = Controller.Current.Length;
            };
            Controller.onElapsedTimeChange += (time) =>
            {
                if (!enabled) return;
                if (currentDuration < ignoreShorterThan) return;
                if (scrobbed == false && time > (currentDuration * scrobbleThreshold / 100.0))
                {
                    scrobbed = true;
                    var tagArtist = Controller.Current.MetaData("tagArtist");
                    var tagTitle = Controller.Current.MetaData("tagTitle");
                    var tagAlbum = Controller.Current.MetaData("tagAlbum");
                    int tagTracknumber = -1;
                    Lutea.Util.Util.tryParseInt(Controller.Current.MetaData("tagTracknumber"), ref tagTracknumber);
                    var tagAlbumArtist = Controller.Current.MetaData("tagAlbumArtist");
                    var duration = (int)Controller.Current.Length;
                    if (lastfm.session_key == null)
                    {
                        lastfm.Auth_getMobileSessionByAuthToken(username, authToken);
                    }
                    var result = lastfm.Track_scrobble(tagArtist, tagTitle, tagAlbum, tagTracknumber <= 0 ? null: tagTracknumber.ToString(), tagAlbumArtist, duration);
                    Logger.Log("last.fm scrobble " + tagTitle + (result ? " OK." : " Fail."));
                }
            };
            if (_setting != null)
            {
                var setting = (Dictionary<string, object>)_setting;
                Util.Util.TryAll(
                    new Lutea.Core.Controller.VOIDVOID[]{
                    () => enabled = (bool)setting["enabled"],
                    () => username = (string)setting["username"],
                    () => authToken = (string)setting["authToken"],
                    () => ignoreShorterThan = (uint)setting["ignoreShorterThan"],
                    () => scrobbleThreshold = (uint)setting["scrobbleThreshold"],
                }, null);
            }
        }

        public object GetSetting()
        {
            var setting = new Dictionary<string, object>();
            setting.Add("enabled", enabled);
            setting.Add("username", username);
            setting.Add("authToken", authToken);
            setting.Add("ignoreShorterThan", ignoreShorterThan);
            setting.Add("scrobbleThreshold", scrobbleThreshold);
            return setting;
        }

        public object GetPreferenceObject()
        {
            return new Preference(enabled, username, !string.IsNullOrEmpty(authToken), ignoreShorterThan, scrobbleThreshold);
        }

        public void SetPreferenceObject(object _pref)
        {
            if (_pref != null)
            {
                var pref = (Preference)_pref;
                this.username = pref.username;
                this.enabled = pref.scrobbleEnabled;
                this.ignoreShorterThan = pref.ignoreShorterThan;
                this.scrobbleThreshold = pref.scrobbleThreshold;
                if (pref.username != null && pref.password != null && pref.username != "" && pref.password != "")
                {
                    var result = lastfm.Auth_getMobileSession(pref.username, pref.password);
                    if (result)
                    {
                        this.authToken = Lastfm.GenAuthToken(pref.username, pref.password);
                    }
                    else
                    {
                        this.authToken = "";
                    }
                }
            }
        }

        public void Quit()
        {
        }

        public bool CanSetEnable()
        {
            return true;
        }

        public void SetEnable(bool enable)
        {
            this.enabled = enable;
        }

        public bool GetEnable()
        {
            return this.enabled;
        }

        public class Preference : LuteaPreference
        {
            private readonly string[] sortorder = { "ScrobbleEnabled", "Username", "Password", "Authenticated"};
            private const string HiddenPassword = "********";

            public bool scrobbleEnabled;
            public uint ignoreShorterThan;
            public uint scrobbleThreshold;
            public string username;
            public string password;
            public string password_disp;
            public bool authenticated;

            public Preference(bool scrobbleEnabled, string username, bool authenticated, uint ignoreShorterThan, uint scrobbleThreshold)
            {
                this.scrobbleEnabled = scrobbleEnabled;
                this.username = username;
                this.authenticated = authenticated;
                this.password_disp = authenticated ? HiddenPassword : "";
                this.ignoreShorterThan = ignoreShorterThan;
                this.scrobbleThreshold = scrobbleThreshold;
            }

            public override string[] GetSortOrder()
            {
                return sortorder;
            }

            [Category("Enable")]
            [DefaultValue(false)]
            [Description("ScrobbleEnabled")]
            public bool ScrobbleEnabled
            {
                get
                {
                    return scrobbleEnabled;
                }
                set
                {
                    this.scrobbleEnabled = value;
                }
            }

            [Category("Option")]
            [DefaultValue(30)]
            [Description("これより短いトラックはscrobbleしない(秒)")]
            public int IgnoreShorterThan
            {
                get
                {
                    return (int)ignoreShorterThan;
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
            public int ScrobbleThreshold
            {
                get
                {
                    return (int)scrobbleThreshold;
                }
                set
                {
                    if ((value > 0) && (value < 100))
                    {
                        this.scrobbleThreshold = (uint)value;
                    }
                }
            }

            [Category("Auth")]
            [Description("Username")]
            public string Username
            {
                get
                {
                    return username;
                }
                set
                {
                    this.username = value;
                }
            }

            [Category("Auth")]
            [DefaultValue(false)]
            [Description("認証に成功したかどうか")]
            public bool Authenticated
            {
                get
                {
                    return authenticated;
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
