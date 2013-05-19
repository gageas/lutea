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
    [LuteaComponentInfo("Last.fm Scrobble", "Gageas", 1.1, "Last.fm Scrobble")]
    public class LastfmScrobble : Lutea.Core.LuteaComponentInterface
    {
        private const int RETRY_COUNT = 5;
        private Preference pref = new Preference(null);
        private Lastfm lastfm = null;

        private bool scrobbed = false;
        private bool nowPlayingUpdated = false;
        private double currentDuration;

        public void Init(object _setting)
        {
            lastfm = new LuteaLastfm();

            Controller.onTrackChange += (i)=>{ 
                scrobbed = false;
                nowPlayingUpdated = false;
                currentDuration = Controller.Current.Length;
                UpdateNowPlaying();
            };

            Controller.onElapsedTimeChange += Scrobble;

            if (_setting != null)
            {
                var setting = (Dictionary<string, object>)_setting;
                pref = new Preference(setting);
            }
        }

        private void Scrobble(int time)
        {
            if (!pref.ScrobbleEnabled) return;
            if (currentDuration < pref.IgnoreShorterThan) return;
            if (scrobbed) return;
            if (time > (currentDuration * pref.ScrobbleThreshold / 100.0))
            {
                scrobbed = true;
                if (lastfm.session_key == null)
                {
                    try
                    {
                        lastfm.Auth_getMobileSessionByAuthToken(pref.Username, pref.AuthToken);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }

                int tagTracknumber = -1;
                Lutea.Util.Util.tryParseInt(Controller.Current.MetaData("tagTracknumber"), ref tagTracknumber);
                var args = new List<KeyValuePair<string, string>>() { 
                    new KeyValuePair<string, string>("method", "track.scrobble"),
                    new KeyValuePair<string, string>("timestamp", Lastfm.CurrentTimestamp.ToString()),
                    new KeyValuePair<string, string>("artist", Controller.Current.MetaData("tagArtist").Replace("\n", ", ")),
                    new KeyValuePair<string, string>("track", Controller.Current.MetaData("tagTitle")),
                    new KeyValuePair<string, string>("album", Controller.Current.MetaData("tagAlbum")),
                    new KeyValuePair<string, string>("albumArtist", Controller.Current.MetaData("tagAlbumArtist")),
                    new KeyValuePair<string, string>("trackNumber", tagTracknumber <= 0 ? null : tagTracknumber.ToString()),
                    new KeyValuePair<string, string>("duration", ((int)Controller.Current.Length).ToString()),
                };

                var success = false;
                for (int i = 0; i < RETRY_COUNT; i++)
                {
                    try
                    {
                        var result = lastfm.CallAPIWithSig(args);
                        if (result != null)
                        {
                            var scrobbles = result.GetElementsByTagName("scrobbles");
                            if (scrobbles.Count == 1)
                            {
                                var acceptecd = scrobbles.Item(0).Attributes["accepted"].Value;
                                if (acceptecd == "1")
                                {
                                    success = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }
                Logger.Log("last.fm scrobble " + Controller.Current.MetaData("tagTitle") + (success ? " OK." : " Fail."));
            }
        }

        private void UpdateNowPlaying()
        {
            if (!pref.UpdateNowPlayingEnabled) return;
            if (Controller.Current.Length < pref.IgnoreShorterThan) return;
            if (nowPlayingUpdated) return;
            nowPlayingUpdated = true;
            if (lastfm.session_key == null)
            {
                try
                {
                    lastfm.Auth_getMobileSessionByAuthToken(pref.Username, pref.AuthToken);
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }

            int tagTracknumber = -1;
            Lutea.Util.Util.tryParseInt(Controller.Current.MetaData("tagTracknumber"), ref tagTracknumber);
            var args = new List<KeyValuePair<string, string>>() { 
                new KeyValuePair<string, string>("method", "track.updateNowPlaying"),
                new KeyValuePair<string, string>("artist", Controller.Current.MetaData("tagArtist").Replace("\n", ", ")),
                new KeyValuePair<string, string>("track", Controller.Current.MetaData("tagTitle")),
                new KeyValuePair<string, string>("album", Controller.Current.MetaData("tagAlbum")),
                new KeyValuePair<string, string>("albumArtist", Controller.Current.MetaData("tagAlbumArtist")),
                new KeyValuePair<string, string>("trackNumber", tagTracknumber <= 0 ? null : tagTracknumber.ToString()),
                new KeyValuePair<string, string>("duration", ((int)Controller.Current.Length).ToString()),
            };

            var success = false;
            for (int i = 0; i < RETRY_COUNT; i++)
            {
                try
                {
                    var result = lastfm.CallAPIWithSig(args);
                    if (result != null)
                    {
                        var scrobbles = result.GetElementsByTagName("lfm");
                        if (scrobbles.Count == 1)
                        {
                            var status = scrobbles[0].Attributes["status"].Value;
                            if (status == "ok")
                            {
                                success = true;
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
            Logger.Log("last.fm update " + Controller.Current.MetaData("tagTitle") + (success ? " OK." : " Fail."));
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
            if (_pref != null)
            {
                var pref = (Preference)_pref;
                if ((pref.Username != null && pref.password != null && pref.Username != "" && pref.password != "") || pref.Username != this.pref.Username)
                {
                    pref.AuthToken = "";
                    try
                    {
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
                this.pref = pref;
                UpdateNowPlaying();
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

        public class Preference : LuteaPreference
        {
            private readonly string[] Sortorder = 
            { 
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

            [Category("Enable")]
            [DefaultValue(false)]
            [Description("Scrobbleを有効にする")]
            public bool ScrobbleEnabled
            {
                get;
                set;
            }

            [Category("Enable")]
            [DefaultValue(false)]
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
