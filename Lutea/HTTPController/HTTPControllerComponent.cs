using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;
using System.Text;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.HTTPController
{
    [GuidAttribute("E63658A1-2ACC-40A1-BE98-2B5F5FC95243")]
    [LuteaComponentInfo("HTTPController", "Gageas", 0.200, "HTTPController")]
    public class HTTPControllerComponent : LuteaComponentInterface
    {
        private HTTPController controller;
        private Preference pref = new Preference();
        public void Init(object _setting)
        {
            if (_setting != null)
            {
                var setting = (Dictionary<string, object>)_setting;
                pref = new Preference(setting);
            }
            Setup();
        }

        private void Setup()
        {
            try
            {
                if (controller != null) controller.Abort();
            }
            catch { }
            if (!pref.Enabled) return;
            try
            {
                controller = new HTTPController(pref.Port);
                controller.Start();
            }
            catch { Logger.Log("HTTPControllerサービスの起動に失敗しました。ポート番号" + pref.Port); }
        }

        public object GetSetting()
        {
            return this.pref.ToDictionary();
        }

        public object GetPreferenceObject()
        {
            return pref.Clone<Preference>();
        }

        public void SetPreferenceObject(object _pref)
        {
            this.pref = (Preference)_pref;
            Setup();
        }

        public void Quit()
        {

        }

        class Preference : LuteaPreference
        {
            private int port = 8080;
            private bool enabled = false;

            public Preference(int port, bool enabled)
            {
                this.port = port;
                this.enabled = enabled;
            }

            [DefaultValue(8080)]
            [Description("待ちうけポート")]
            public int Port
            {
                get
                {
                    return port;
                }
                set
                {
                    this.port = value;
                }
            }

            [DefaultValue(false)]
            [TypeConverter(typeof(BooleanYesNoTypeConverter))]
            [Description("有効にする")]
            public bool Enabled
            {
                get
                {
                    return enabled;
                }
                set
                {
                    this.enabled = value;
                }
            }
            
            public Preference(Dictionary<string, object> setting)
                : base(setting)
            {
            }

            public Preference()
            {
            }
        }

        public bool CanSetEnable()
        {
            return true;
        }

        public void SetEnable(bool enable)
        {
            pref.Enabled = enable;
            Setup();
        }

        public bool GetEnable()
        {
            return pref.Enabled;
        }
    }
}
