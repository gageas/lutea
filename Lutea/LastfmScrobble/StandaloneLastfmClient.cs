using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gageas.Lutea.Core;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.LastfmScrobble
{
    class StandaloneLastfmClient : AbstractLastfmClient
    {
        private LastfmScrobble Parent;
        private const int RETRY_COUNT = 5;
        private Lastfm lastfm = null;

        public StandaloneLastfmClient(LastfmScrobble parent)
        {
            lastfm = new LuteaLastfm();
            this.Parent = parent;
        }

        public override void Dispose()
        {
            // nothing
        }

        public override void OnTrackChange(int i)
        {
            UpdateNowPlaying();
        }

        public override void OnExceedThresh()
        {
            if (lastfm.session_key == null)
            {
                try
                {
                    lastfm.Auth_getMobileSessionByAuthToken(Parent.pref.Username, Parent.pref.AuthToken);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }

            int tagTracknumber = Lutea.Util.Util.GetTrackNumberInt(Controller.Current.MetaData("tagTracknumber"));
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

        public override void OnPause()
        {
            throw new NotImplementedException();
        }

        public override void OnResume()
        {
            throw new NotImplementedException();
        }

        private void UpdateNowPlaying()
        {
            if (!Parent.pref.UpdateNowPlayingEnabled) return;
            if (Controller.Current.Length < Parent.pref.IgnoreShorterThan) return;
            if (lastfm.session_key == null)
            {
                try
                {
                    lastfm.Auth_getMobileSessionByAuthToken(Parent.pref.Username, Parent.pref.AuthToken);
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }

            int tagTracknumber = Lutea.Util.Util.GetTrackNumberInt(Controller.Current.MetaData("tagTracknumber"));
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
    }
}
