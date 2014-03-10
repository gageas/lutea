using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.IO;

namespace Gageas.Lutea.Core
{
    class ComponentManager
    {
        private string SettingFilename;
        private static List<LuteaComponentInterface> Components = new List<LuteaComponentInterface>();
        private static List<Assembly> Assemblys = new List<Assembly>();

        internal ComponentManager(string settingFilename)
        {
            SettingFilename = settingFilename;
        }

        internal LuteaComponentInterface[] GetComponents()
        {
            return Components.ToArray();
        }

        internal void Add(LuteaComponentInterface component)
        {
            Components.Add(component);
        }

        internal System.Windows.Forms.Form BuildAllInstance(string[] files)
        {
            System.Windows.Forms.Form componentAsMainForm = null;
            try
            {
                foreach (var component_file in files)
                {
                    try
                    {
                        //アセンブリとして読み込む
                        var asm = System.Reflection.Assembly.LoadFrom(component_file);
                        var types = asm.GetTypes().Where(_ => _.IsClass && _.IsPublic && !_.IsAbstract && _.GetInterface(typeof(Lutea.Core.LuteaComponentInterface).FullName) != null);
                        foreach (Type t in types)
                        {
                            var p = (Lutea.Core.LuteaComponentInterface)asm.CreateInstance(t.FullName);
                            if (p == null) continue;
                            Components.Add(p);
                            Assemblys.Add(asm);
                            if (componentAsMainForm == null && p is System.Windows.Forms.Form)
                            {
                                componentAsMainForm = (System.Windows.Forms.Form)p;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }
            }
            catch (Exception ee)
            {
                Logger.Error(ee);
            }
            return componentAsMainForm;
        }

        internal void LoadSettings()
        {
            // load Plugins Settings
            Dictionary<Guid, object> pluginSettings = new Dictionary<Guid, object>();
            try
            {
                using (var fs = new System.IO.FileStream(SettingFilename, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                    var pluginSettingsTmp = (Dictionary<Guid, byte[]>)(new BinaryFormatter()).Deserialize(fs);
                    foreach (var e in pluginSettingsTmp)
                    {
                        try
                        {
                            pluginSettings.Add(e.Key, (new BinaryFormatter()).Deserialize(new MemoryStream(e.Value)));
                        }
                        catch { }
                    }
                    AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                }
            }
            catch (Exception)
            {
                try
                {
                    using (var fs = new System.IO.FileStream(SettingFilename, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                        pluginSettings = (Dictionary<Guid, object>)(new BinaryFormatter()).Deserialize(fs);
                        AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve);
                    }
                }
                catch (Exception ex2) { Logger.Log(ex2); }
            }

            // initialize plugins
            foreach (var pin in Components)
            {
                try
                {
                    pin.Init(pluginSettings.FirstOrDefault(_ => _.Key == pin.GetType().GUID).Value);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        internal void FinalizeComponents()
        {
            using (var fs = new System.IO.FileStream(SettingFilename, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite))
            {
                Dictionary<Guid, byte[]> pluginSettings = new Dictionary<Guid, byte[]>();
                foreach (var p in Components)
                {
                    try
                    {
                        var ms = new MemoryStream();
                        (new BinaryFormatter()).Serialize(ms, p.GetSetting());
                        pluginSettings.Add(p.GetType().GUID, ms.ToArray());
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                    finally
                    {
                        try { p.Quit(); }
                        catch (Exception ex)
                        {
                            Logger.Error(ex);
                        }
                    }
                }
                (new BinaryFormatter()).Serialize(fs, pluginSettings);
            }
        }

        internal void ActivateUIComponents()
        {
            foreach (var plg in Components)
            {
                if (plg is LuteaUIComponentInterface)
                {
                    ((LuteaUIComponentInterface)plg).ActivateUI();
                }
            }
        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assemblys.First(_ => _.FullName == args.Name);
        }
    }
}
