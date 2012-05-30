using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.ComponentModel;
using Gageas.Lutea;
using Gageas.Lutea.Core;

namespace ClassLibrary1
{
    // プラグインごとにユニークなGUID
    [GuidAttribute("973FD092-9944-495F-9AA8-35D3BEAA4D51")]
    [LuteaComponentInfo("Sample1", "Gageas", 1.0, "Componentの実装サンプル")]
    public class Class1 : Gageas.Lutea.Core.LuteaComponentInterface
    {
        private Dictionary<string, object> setting = new Dictionary<string, object>();
        public int StartCount = 0;
        private TimeSpan TotalUpTime = new TimeSpan(0);


        private DateTime start = DateTime.Now;
        public void Init(object _setting)
        {
            try
            {
                if (_setting != null)
                {
                    ParseSetting((Dictionary<string,object>)_setting);
                }
            }
            catch { }
//            AppCore.onTrackChange += ((Controller.VOIDINT)((i) => { Gageas.Lutea.Logger.Log(Controller.Current.MetaData(DBCol.tagTitle)+"を再生してるよ" + StartCount); }));
            StartCount++;
        }
        public object GetSetting()
        {
            Dictionary<string, object> export = new Dictionary<string, object>();
            export["StartCount"] = StartCount;
            TimeSpan upTime = new TimeSpan(DateTime.Now.Ticks - start.Ticks);
            export["TotalUpTime"] = TotalUpTime.Add(upTime);
            return export;
        }
        private void ParseSetting(Dictionary<string,object> setting)
        {
            this.StartCount = (int)setting["StartCount"];
            this.TotalUpTime = (TimeSpan)setting["TotalUpTime"];
        }


        public object GetPreferenceObject()
        {
            return new Preference(this);
        }

        public void SetPreferenceObject(object pref)
        {
            return;
//            throw new NotImplementedException();
        }

        public void Quit() { }

        private class Preference
        {
            private Class1 class1;

            public Preference(Class1 class1)
            {
                this.class1 = class1;
            }

            [Description("起動回数カウンタ")]
            public int StartCount
            {
                get
                {
                    return class1.StartCount;
                }
            }

            [Description("起動時間カウンタ")]
            public TimeSpan TotalUpTime
            {
                get
                {
                    return class1.TotalUpTime;
                }
            }

            [Description("起動時間カウンタ")]
            public List<Gageas.Lutea.Library.Column> cols //TimeSpan TotalUpTime
            {
                get
                {
                    return Controller.ExtraColumns.ToList();
//                    return class1.TotalUpTime;
                }
            }
        }
    }
}
