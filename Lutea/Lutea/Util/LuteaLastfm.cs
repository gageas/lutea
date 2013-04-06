using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Gageas.Lutea.Util
{
    public class LuteaLastfm : Lastfm
    {
        const string lutea_api_key = "a6e1b05b051efa32e157be87d93bf074";
        const string lutea_secret = "401bf2202659a84b8ffc73b9e3c39d02";

        public override string GetAPIKey()
        {
            return lutea_api_key;
        }

        public override string GetAPISecret()
        {
            return lutea_secret;
        }

        public XmlDocument Artist_getInfo(string artistname, string lang = "jp")
        {
            if (artistname == null) return null;
            var result = this.SendWithoutAPISig(new List<KeyValuePair<string, string>>() {
                new KeyValuePair<string, string>("method", "artist.getInfo"),
                new KeyValuePair<string, string>("artist", artistname),
                new KeyValuePair<string, string>("lang", lang),
                new KeyValuePair<string, string>("api_key", GetAPIKey()) 
            });
            if (result != null)
            {
                return result;
            }
            return null;
        }
    }
}
