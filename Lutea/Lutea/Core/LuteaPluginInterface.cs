using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Gageas.Lutea.Core
{
    public interface LuteaComponentInterface
    {
        void Init(object setting);
        object GetSetting();
        object GetPreferenceObject();
        void SetPreferenceObject(object pref);
        void Quit();
    }

    public interface LuteaUIComponentInterface : LuteaComponentInterface
    {
        void LibraryInitializeRequired();
    }

    public class LuteaComponentInfo : System.Attribute
    {
        public string name;
        public string auther;
        public double version;
        public string description;

        public LuteaComponentInfo(string name, string auther, double version, string description)
        {
            this.name = name;
            this.auther = auther;
            this.version = version;
            this.description = description;
        }
        public override string ToString()
        {
            return String.Format("{0}'s {1} v{2:0.00} - {3}",auther,name,version,description);
        }
    }
}
