using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel;

namespace Gageas.Lutea.Core
{
    public interface LuteaComponentInterface
    {
        void Init(object setting);
        object GetSetting();
        object GetPreferenceObject();
        void SetPreferenceObject(object pref);
        void Quit();
        bool CanSetEnable();
        void SetEnable(bool enable);
        bool GetEnable();
    }

    class LuteaComponentPreferenceTypeConverter : TypeConverter
    {
        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            var pdc = TypeDescriptor.GetProperties(value, attributes);
            if (value is LuteaPreference)
            {
                LuteaPreference lpref = (LuteaPreference)value;
                var sortorder = lpref.GetSortOrder();
                if (sortorder != null)
                {
                    return pdc.Sort(sortorder);
                }
            }
            return base.GetProperties(context, value, attributes);
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext context)
        {
            return true;
        }
    }

    [TypeConverter(typeof(LuteaComponentPreferenceTypeConverter))]
    public abstract class LuteaPreference
    {
        public virtual string[] GetSortOrder()
        {
            return null;
        }
    }

    public interface LuteaUIComponentInterface : LuteaComponentInterface
    {
        void LibraryInitializeRequired();
    }

    [System.AttributeUsage(System.AttributeTargets.Class, 
                   AllowMultiple=false, 
                   Inherited=true)]
    public sealed class LuteaComponentInfo : System.Attribute
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
