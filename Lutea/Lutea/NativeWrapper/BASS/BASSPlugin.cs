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

        public static BASSPlugin[] GetPlugins()
        {
            return plugins.ToArray();
        }

        public static Boolean Load(string filename, uint flags)
        {
            IntPtr pinPtr = (IntPtr)0;
            try
            {
                pinPtr = _BASS_PluginLoad(filename, BASS.BASS_UNICODE | flags);
                if (pinPtr != (IntPtr)0)
                {
                    var pin = new BASSPlugin(filename, pinPtr);
                    plugins.Add(pin);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private IntPtr ptr;
        private string filename;

        private BASSPlugin(string filename, IntPtr ptr)
        {
            this.filename = filename;
            this.ptr = ptr;
        }

        public void Dispose()
        {
            _BASS_PluginFree(ptr);
            ptr = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }

        ~BASSPlugin()
        {
            this.Dispose();
        }

        public string Filename
        {
            get { return filename; }
        }

        BASS_PLUGININFO GetInfo()
        {
            var p_pinfo = _BASS_PluginGetInfo(ptr);
            return (BASS_PLUGININFO)Marshal.PtrToStructure(p_pinfo, typeof(BASS_PLUGININFO));
        }

        public UInt32 Version
        {
            get { return GetInfo().Version; }
        }

        public BASSPluginFormat[] GetFormats()
        {
            var p_pinfo = _BASS_PluginGetInfo(ptr);
            var info = GetInfo();
            if (info.FormatCount <= 0) return null;
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
            pform = (BASSPluginFormat)Marshal.PtrToStructure(new IntPtr((int)ptr + (sizeof(UInt32) + IntPtr.Size + IntPtr.Size) * index), typeof(BASSPluginFormat));
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
