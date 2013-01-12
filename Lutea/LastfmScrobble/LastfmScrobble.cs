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
    [LuteaComponentInfo("Last.fm Scrobble", "Gageas", 1.0, "Last.fm Scrobble")]
    public class LastfmScrobble : Lutea.Core.LuteaComponentInterface
    {
        private Preference pref = new Preference();
        private Lastfm lastfm = null;

        private bool scrobbed = false;
        private double currentDuration;

        public void Init(object _setting)
        {
            lastfm = Lastfm.GetLuteaLastfmInstance();

            Controller.onTrackChange += (i)=>{ 
                scrobbed = false; 
                currentDuration = Controller.Current.Length;
            };

            Controller.onTrackChange += (id) =>
            {
                if (!pref.ScrobbleEnabled) return;
                if (!pref.UpdateNowPlayingEnabled) return;

                var tagArtist = Controller.Current.MetaData("tagArtist");
                var tagTitle = Controller.Current.MetaData("tagTitle");
                var tagAlbum = Controller.Current.MetaData("tagAlbum");
                int tagTracknumber = -1;
                Lutea.Util.Util.tryParseInt(Controller.Current.MetaData("tagTracknumber"), ref tagTracknumber);
                var tagAlbumArtist = Controller.Current.MetaData("tagAlbumArtist");
                var duration = (int)Controller.Current.Length;
                if (lastfm.session_key == null)
                {
                    lastfm.Auth_getMobileSessionByAuthToken(pref.Username, pref.authToken);
                }
                var result = Track_updateNowPlaying(tagArtist, tagTitle, tagAlbum, tagTracknumber <= 0 ? null : tagTracknumber.ToString(), tagAlbumArtist, duration);
                Logger.Log("last.fm scrobble " + tagTitle + (result ? " OK." : " Fail."));
            };

            Controller.onElapsedTimeChange += (time) =>
            {
                if (!pref.ScrobbleEnabled) return;
                if (currentDuration < pref.IgnoreShorterThan) return;
                if (scrobbed == false && time > (currentDuration * pref.ScrobbleThreshold / 100.0))
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
                        lastfm.Auth_getMobileSessionByAuthToken(pref.Username, pref.authToken);
                    }
                    var result = Track_scrobble(tagArtist, tagTitle, tagAlbum, tagTracknumber <= 0 ? null : tagTracknumber.ToString(), tagAlbumArtist, duration);
                    Logger.Log("last.fm scrobble " + tagTitle + (result ? " OK." : " Fail."));
                }
            };

            if (_setting != null)
            {
                var setting = (Dictionary<string, object>)_setting;
                pref = new Preference(setting);
            }
        }

        public bool Track_scrobble(string artist, string track, string album = null, string trackNumber = null, string albumArtist = null, int? duration = null)
        {
            var result = lastfm.CallAPIWithSig(new List<KeyValuePair<string, string>>() { 
                new KeyValuePair<string, string>("method", "track.scrobble"),
                new KeyValuePair<string, string>("timestamp", Lastfm.CurrentTimestamp.ToString()),
                new KeyValuePair<string, string>("artist", artist),
                new KeyValuePair<string, string>("track", track),
                new KeyValuePair<string, string>("album", album),
                new KeyValuePair<string, string>("albumArtist", albumArtist),
                new KeyValuePair<string, string>("trackNumber", trackNumber),
                new KeyValuePair<string, string>("duration", duration == null ? null : duration.ToString()),
            });
            if (result != null)
            {
                var scrobbles = result.GetElementsByTagName("scrobbles");
                if (scrobbles.Count == 1)
                {
                    var acceptecd = scrobbles.Item(0).Attributes["accepted"].Value;
                    if (acceptecd == "1")
                    {
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }

        public bool Track_updateNowPlaying(string artist, string track, string album = null, string trackNumber = null, string albumArtist = null, int? duration = null)
        {
            var result = lastfm.CallAPIWithSig(new List<KeyValuePair<string, string>>() { 
                new KeyValuePair<string, string>("method", "track.updateNowPlaying"),
                new KeyValuePair<string, string>("artist", artist),
                new KeyValuePair<string, string>("track", track),
                new KeyValuePair<string, string>("album", album),
                new KeyValuePair<string, string>("albumArtist", albumArtist),
                new KeyValuePair<string, string>("trackNumber", trackNumber),
                new KeyValuePair<string, string>("duration", duration == null ? null : duration.ToString()),
            });
            if (result != null)
            {
                var scrobbles = result.GetElementsByTagName("lfm");
                if (scrobbles.Count == 1)
                {
                    var status = scrobbles[0].Attributes["status"].Value;
                    if (status == "ok")
                    {
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }
        
        public object GetSetting()
        {
            return pref.ToDictionary();
        }

        public object GetPreferenceObject()
        {
            var clone = pref.Clone();
            return clone;
        }

        public void SetPreferenceObject(object _pref)
        {
            if (_pref != null)
            {
                var pref = (Preference)_pref;
                if ((pref.username != null && pref.password != null && pref.username != "" && pref.password != "") || pref.username != this.pref.username)
                {
                    try
                    {
                        var result = lastfm.Auth_getMobileSession(pref.username, pref.password);
                        if (result)
                        {
                            pref.authToken = Lastfm.GenAuthToken(pref.username, pref.password);
                        }
                        else
                        {
                            pref.authToken = "";
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                        pref.authToken = "";
                    }
                }
                this.pref = pref;
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
            this.pref.ScrobbleEnabled = enable;
        }

        public bool GetEnable()
        {
            return this.pref.ScrobbleEnabled;
        }

        public class Preference : LuteaPreference, ICloneable
        {
            private readonly string[] sortorder = { "ScrobbleEnabled", "UpdateNowPlayingEnabled", "Username", "Password", "Authenticated"};
            private const string HiddenPassword = "********";
            private bool authenticated
            {
                get
                {
                    return !string.IsNullOrEmpty(this.authToken);
                }
            }
            internal string authToken;

            public bool scrobbleEnabled = false;
            public bool updateNowPlayingEnabled = false;
            public uint ignoreShorterThan = 30;
            public uint scrobbleThreshold = 50;
            public string username;
            public string password;
            public string password_disp;

            public Preference()
            {
            }

            public Preference(Dictionary<string, object> setting)
            {
                Util.Util.TryAll(
                    new Lutea.Core.Controller.VOIDVOID[]{
                    () => this.ScrobbleEnabled = (bool)setting["enabled"],
                    () => this.UpdateNowPlayingEnabled = (bool)setting["enabledUpdateNowPlaying"],
                    () => this.Username = (string)setting["username"],
                    () => this.authToken = (string)setting["authToken"],
                    () => this.IgnoreShorterThan = (uint)setting["ignoreShorterThan"],
                    () => this.ScrobbleThreshold = (uint)setting["scrobbleThreshold"],
                }, null);
                this.password_disp = authenticated ? HiddenPassword : "";
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

            [Category("Enable")]
            [DefaultValue(false)]
            [Description("UpdateNowPlayingEnabled")]
            public bool UpdateNowPlayingEnabled
            {
                get
                {
                    return updateNowPlayingEnabled;
                }
                set
                {
                    this.updateNowPlayingEnabled = value;
                }
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

            public Dictionary<string, object> ToDictionary()
            {
                var setting = new Dictionary<string, object>();
                setting.Add("enabled", this.ScrobbleEnabled);
                setting.Add("enabledUpdateNowPlaying", this.UpdateNowPlayingEnabled);
                setting.Add("username", this.Username);
                setting.Add("authToken", authToken);
                setting.Add("ignoreShorterThan", this.IgnoreShorterThan);
                setting.Add("scrobbleThreshold", this.ScrobbleThreshold);
                return setting;
            }

            public object Clone()
            {
                return new Preference(this.ToDictionary());
            }
        }
    }
}
