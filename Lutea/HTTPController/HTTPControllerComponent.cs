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
    [LuteaComponentInfo("HTTPController", "Gageas", 0.089, "HTTPController")]
    public class HTTPControllerComponent : LuteaComponentInterface
    {
        private HTTPController controller;
        private bool enabled = false;
        private int port = 8080;
        public void Init(object _setting)
        {
            if (_setting != null)
            {
                var setting = (Dictionary<string, object>)_setting;
                Util.Util.TryAll(
                    new Lutea.Core.Controller.VOIDVOID[]{
                    () => enabled = (bool)setting["enabled"],
                    () => port = (int)setting["port"]
                }, null);
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
            if (!enabled) return;
            try
            {
                controller = new HTTPController(port);
                controller.Start();
            }
            catch { Logger.Log("HTTPControllerサービスの起動に失敗しました。ポート番号" + port); }
        }

        public object GetSetting()
        {
            var setting = new Dictionary<string, object>();
            setting.Add("port", port);
            setting.Add("enabled", enabled);
            return setting;
        }

        public object GetPreferenceObject()
        {
            return new Preference(port, enabled);
        }

        public void SetPreferenceObject(object _pref)
        {
            var pref = (Preference)_pref;
            port = pref.Port;
            enabled = pref.Enabled;
            Setup();
        }

        public void Quit()
        {

        }

        class Preference
        {
            private int port;
            private bool enabled;

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
        }
    }
}
