using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Security.Cryptography;

namespace Gageas.Lutea.Util
{
    public class Lastfm
    {
        const string baseURIHTTP  = "http://ws.audioscrobbler.com/2.0/";
        const string baseURIHTTPS = "https://ws.audioscrobbler.com/2.0/";
        const string lutea_api_key = "a6e1b05b051efa32e157be87d93bf074";
        const string lutea_secret = "401bf2202659a84b8ffc73b9e3c39d02";

        private static System.DateTime UnixEpoch = new System.DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static Int64 CurrentTimestamp
        {
            get
            {
                return (long)(System.DateTime.Now.ToUniversalTime().Subtract(UnixEpoch).TotalSeconds);
            }
        }

        private readonly string api_key;
        private readonly string secret;

        public string session_key
        {
            get;
            private set;
        }

        public Lastfm(string api_key, string secret)
        {
            this.api_key = api_key;
            this.secret = secret;
        }

        public static Lastfm GetLuteaLastfmInstance()
        {
            return new Lastfm(lutea_api_key, lutea_secret);
        }

        private static string GetMD5(string src)
        {
            var md5 = new MD5CryptoServiceProvider();
            md5.ComputeHash(Encoding.UTF8.GetBytes(src));
            var sig = md5.Hash.Select(_ => _.ToString("x2")).Aggregate((a, b) => a + b);
            return sig;
        }

        public static string GenAuthToken(string username, string password)
        {
            return GetMD5(username + GetMD5(password));
        }

        public XmlDocument CallAPIWithSig(List<KeyValuePair<string, string>> _args)
        {
            if (session_key == null)
            {
                return null;
            }
            var args = new List<KeyValuePair<string, string>>(_args);
            args.Add(new KeyValuePair<string, string>("api_key", api_key));
            args.Add(new KeyValuePair<string, string>("sk", session_key));
            var result = this.SendWithAPISig(args);
            return result;
        }

        public bool Auth_getMobileSession(string username, string password)
        {
            return Auth_getMobileSessionByAuthToken(username, GenAuthToken(username, password));
        }

        public bool Auth_getMobileSessionByAuthToken(string username, string authtoken)
        {
            var result = this.SendWithAPISig(new List<KeyValuePair<string, string>>() { 
                new KeyValuePair<string, string>("method", "auth.getMobileSession"),
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("authToken", authtoken),
                new KeyValuePair<string, string>("api_key", api_key),
            });
            if (result != null)
            {
                var keys = result.GetElementsByTagName("key");
                if (keys.Count == 1)
                {
                    this.session_key = keys.Item(0).FirstChild.Value;
                    return true;
                }
            }
            return false;
        }

        private string Auth_gettoken()
        {
            var result = this.SendWithAPISig(new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("method", "auth.getToken"),
                new KeyValuePair<string, string>("api_key", api_key) 
            });
            if (result != null)
            {
                var tkns = result.GetElementsByTagName("token");
                if (tkns.Count == 1)
                {
                    return tkns.Item(0).FirstChild.Value;
                }
            }
            return null;
        }

        public XmlDocument Artist_getInfo(string artistname, string lang = "jp")
        {
            if (artistname == null) return null;
            var result = this.SendWithoutAPISig(new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("method", "artist.getInfo"),
                new KeyValuePair<string, string>("artist", artistname),
                new KeyValuePair<string, string>("lang", lang),
                new KeyValuePair<string, string>("api_key", api_key) 
            });
            if (result != null)
            {
                return result;
            }
            return null;
        }

        private XmlDocument send(string parameters, bool https, bool httppost)
        {
            var req = System.Net.HttpWebRequest.Create((https ? baseURIHTTPS : baseURIHTTP) + (httppost ? "" : ("?" + parameters)));
            ((HttpWebRequest)req).UserAgent = "Lutea - audio player for Windows";
            if (httppost)
            {
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                byte[] payload = Encoding.UTF8.GetBytes(parameters);
                req.ContentLength = payload.Length;
                using (var strm = req.GetRequestStream())
                {
                    strm.Write(payload, 0, payload.Length);
                }
            }

            try
            {
                var res = req.GetResponse();
                var xml = new XmlDocument();
                xml.Load(res.GetResponseStream());
                return xml;
            }
            catch (WebException ex)
            {
                ex.Status.ToString();
                return null;
            }
        }

        private XmlDocument SendWithAPISig(List<KeyValuePair<string, string>> parameters, bool https = true, bool httppost = true)
        {
            var sorted = parameters.Where(_ => _.Value != null && _.Value != "").ToList();
            sorted.Sort((a, b) => a.Key.CompareTo(b.Key));
            var f = sorted.Select((e) => e.Key + e.Value).Aggregate((a, b) => a + b) + secret;
            var signature = GetMD5(f);
            return send(sorted.Select((e) => e.Key + "=" + Uri.EscapeDataString(e.Value)).Aggregate((a, b) => a + "&" + b) + "&api_sig=" + signature, https, httppost);
        }

        private XmlDocument SendWithoutAPISig(List<KeyValuePair<string, string>> parameters, bool https = false, bool httppost = false)
        {
            return send(parameters.Select((e) => e.Key + "=" + Uri.EscapeDataString(e.Value)).Aggregate((a, b) => a + "&" + b), https, httppost);
        }
    }
}