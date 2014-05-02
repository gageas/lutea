using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Gageas.Wrapper.BASS
{
    /// <summary>
    /// Pluginクラス
    /// </summary>
    public class BASSPlugin : IDisposable
    {
        private static List<BASSPlugin> plugins = new List<BASSPlugin>();

        private IntPtr HPlugin;
        
        /// <summary>
        /// プラグインのファイル名
        /// </summary>
        public readonly string Filename;

        /// <summary>
        /// バージョン
        /// </summary>
        public UInt32 Version
        {
            get { return GetInfo().Version; }
        }

        /// <summary>
        /// プラグインのリストを取得
        /// </summary>
        /// <returns></returns>
        public static BASSPlugin[] GetPlugins()
        {
            return plugins.ToArray();
        }

        public static Boolean Load(string filename, uint flags)
        {
            try
            {
                var pinPtr = _BASS_PluginLoad(filename, BASS.BASS_UNICODE | flags);
                if (pinPtr == IntPtr.Zero) return false;
                var pin = new BASSPlugin(filename, pinPtr);
                plugins.Add(pin);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private BASSPlugin(string filename, IntPtr ptr)
        {
            this.Filename = filename;
            this.HPlugin = ptr;
        }

        public void Dispose()
        {
            if (HPlugin != IntPtr.Zero)
            {
                _BASS_PluginFree(HPlugin);
            }
            HPlugin = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }

        ~BASSPlugin()
        {
            this.Dispose();
        }

        BASS_PLUGININFO GetInfo()
        {
            var p_pinfo = _BASS_PluginGetInfo(HPlugin);
            return (BASS_PLUGININFO)Marshal.PtrToStructure(p_pinfo, typeof(BASS_PLUGININFO));
        }

        public BASSPluginFormat[] GetFormats()
        {
            var p_pinfo = _BASS_PluginGetInfo(HPlugin);
            var info = GetInfo();
            if (info.FormatCount <= 0) return new BASSPluginFormat[0];
            BASSPluginFormat[] forms = new BASSPluginFormat[info.FormatCount];
            for (int i = 0; i < info.FormatCount; i++)
            {
                forms[i] = GetPluginForm(p_pinfo, i);
            }
            return forms;
        }

        BASSPluginFormat GetPluginForm(IntPtr thisptr, int index)
        {
            BASSPluginFormat pform;
            var ptr = Marshal.ReadIntPtr(thisptr, sizeof(UInt32) * 2);
            pform = (BASSPluginFormat)Marshal.PtrToStructure(IntPtr.Add(ptr, BASSPluginFormat.Size * index), typeof(BASSPluginFormat));
            return pform;
        }

        [DllImport("bass.dll", EntryPoint = "BASS_PluginLoad", CharSet = CharSet.Unicode)]
        private static extern IntPtr _BASS_PluginLoad(string filename, uint flags);

        [DllImport("bass.dll", EntryPoint = "BASS_PluginFree", CharSet = CharSet.Unicode)]
        private static extern bool _BASS_PluginFree(IntPtr plugin);

        [DllImport("bass.dll", EntryPoint = "BASS_PluginGetInfo", CharSet = CharSet.Unicode)]
        private static extern IntPtr _BASS_PluginGetInfo(IntPtr plugin);

    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BASSPluginFormat
    {
        UInt32 ctype;
        IntPtr name;
        IntPtr exts;
        public static readonly int Size = sizeof(UInt32) + IntPtr.Size + IntPtr.Size;
        public UInt32 CType
        {
            get
            {
                return ctype;
            }
        }
        public string Name
        {
            get
            {
                return Marshal.PtrToStringAnsi(name);
            }
        }
        public string Exts
        {
            get
            {
                return Marshal.PtrToStringAnsi(exts);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BASS_PLUGININFO
    {
        public UInt32 Version;
        public UInt32 FormatCount;
    }
}
