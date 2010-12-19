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
    [LuteaComponentInfo("HTTPController", "Gageas", 1.0, "HTTPController")]
    public class HTTPControllerComponent : LuteaComponentInterface
    {
        private HTTPController controller;
        public void Init(object _setting)
        {
            int port = 8080;
            if (_setting != null)
            {
                var setting = (Dictionary<string, object>)_setting;
                Util.Util.TryAll(
                    new Lutea.Core.Controller.VOIDVOID[]{
                    () => port = (int)setting["port"]
                }, null);
            }
            Setup(port);
        }

        private void Setup(int port)
        {
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
            setting.Add("port", controller.port);
            return setting;
        }

        public object GetPreferenceObject()
        {
            return new Preference(controller.port);
        }

        public void SetPreferenceObject(object _pref)
        {
            var pref = (Preference)_pref;
            controller.Abort();
            Setup(pref.Port);
        }

        public void Quit()
        {

        }

        class Preference
        {
            private int port;

            public Preference(int port)
            {
                this.port = port;
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
        }
    }
}
