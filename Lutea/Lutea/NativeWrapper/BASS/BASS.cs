using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Gageas.Wrapper.BASS
{
    public class BASS
    {
        #region Plugin
        /// <summary>
        /// Pluginクラス
        /// </summary>
        public class Plugin : IDisposable
        {
            private static List<Plugin> plugins = new List<Plugin>();

            public static Plugin[] GetPlugins()
            {
                return plugins.ToArray();
            }

            public static Boolean Load(string filename, uint flags)
            {
                IntPtr pinPtr = (IntPtr)0;
                try
                {
                    pinPtr = _BASS_PluginLoad(filename, BASS_UNICODE | flags);
                    if (pinPtr != (IntPtr)0)
                    {
                        var pin = new Plugin(filename, pinPtr);
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

            private Plugin(string filename, IntPtr ptr)
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

            ~Plugin()
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
        #endregion

        /*
         * Sync（再生時イベント列挙体）
         */
        public enum SYNC_TYPE
        {
            POS = 0,
            END = 2,
            META = 4,
            SLIDE = 5,
            STALL = 6,
            DOWNLOAD = 7,
            FREE = 8,
            SETPOS = 11,
            MUSICPOS = 10,
            MUSICINST = 1,
            MUSICFX = 3,
            OGG_CHANGE = 12
        }

        /*
         * BASS_ChannelSlideAttributeで使うAttributes
         */
        private enum BASS_ATTRIB
        {
            FREQ = 1, VOL, PAN, EAXMIX,
            MUSIC_AMPLIFY = 0x100, MUSIC_PANSEP, MUSIC_PSCALER, MUSIC_BPM, MUSIC_SPEED, MUSIC_VOL_GLOBAL, MUSIC_VOL_CHAN = 0x200, MUSIC_VOL_INST = 0x300
        }
        public enum BASS_CONFIG
        {
            BASS_CONFIG_BUFFER = 0,
            BASS_CONFIG_UPDATEPERIOD = 1,
            BASS_CONFIG_GVOL_SAMPLE = 4,
            BASS_CONFIG_GVOL_STREAM = 5,
            BASS_CONFIG_GVOL_MUSIC = 6,
            BASS_CONFIG_CURVE_VOL = 7,
            BASS_CONFIG_CURVE_PAN = 8,
            BASS_CONFIG_FLOATDSP = 9,
            BASS_CONFIG_3DALGORITHM = 10,
            BASS_CONFIG_NET_TIMEOUT = 11,
            BASS_CONFIG_NET_BUFFER = 12,
            BASS_CONFIG_PAUSE_NOPLAY = 13,
            BASS_CONFIG_NET_PREBUF = 15,
            BASS_CONFIG_NET_PASSIVE = 18,
            BASS_CONFIG_REC_BUFFER = 19,
            BASS_CONFIG_NET_PLAYLIST = 21,
            BASS_CONFIG_MUSIC_VIRTUAL = 22,
            BASS_CONFIG_VERIFY = 23,
            BASS_CONFIG_UPDATETHREADS = 24,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BASS_CHANNELINFO
        {
            public UInt32 freq;
            public UInt32 chans;
            public Stream.StreamFlag flags;
            public UInt32 ctype;
            public UInt32 origres;
            public IntPtr plugin;
            public IntPtr sample;
            private IntPtr _filename;
            public string filename
            {
                get
                {
                    return Marshal.PtrToStringUni(_filename);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BASS_DEVICEINFO {
            [Flags]
            public enum FLAGS : uint
            {
                ENABLED = 1, DEFAULT = 2, INIT = 4
            }

            private IntPtr name;
            private IntPtr driver;
            private UInt32 flags;

            public string Name
            {
                get
                {
                    return Marshal.PtrToStringAnsi(this.name);
                }
            }

            public string Driver
            {
                get
                {
                    return Marshal.PtrToStringAnsi(this.driver);
                }
            }

            public FLAGS Flags
            {
                get
                {
                    return (FLAGS)this.flags;
                }
            }

            public bool isEnabled
            {
                get { return (Flags & FLAGS.ENABLED) != 0; }
            }

            public bool isDefault
            {
                get { return (Flags & FLAGS.DEFAULT) != 0; }
            }

            public bool isInit
            {
                get { return (Flags & FLAGS.INIT) != 0; }
            }
        }

        public static BASS_DEVICEINFO? GetDeviceInfo(UInt32 device)
        {
            BASS_DEVICEINFO info;
            bool success = _BASS_GetDeviceInfo(device, out info);
            return success ? info : (BASS_DEVICEINFO?)null;
        }
        
        public delegate UInt32 StreamProc(IntPtr bffer, UInt32 length);

        private const uint BASS_UNICODE = 0x80000000;
        private const uint BASS_POS_BYTE = 0;

        public static Boolean Floatable {
            get
            {
                if (!BASS.isAvailable) return false;
                bool floatable = false;
                using (var strm = new UserSampleStream(44100, 1, null, Stream.StreamFlag.BASS_STREAM_FLOAT))
                {
                    if (strm != null) floatable = true;
                }
                return floatable;
            }
        }

        public static bool BASS_Init(int device, uint freq = 44100, uint buffer_len = 1500)
        {
            bool success = false;
            // Init前から走っていたスレッドのIdを保持
            var ths_before = System.Diagnostics.Process.GetCurrentProcess().Threads;
            List<int> ids = new List<int>();
            for (int i = 0; i < ths_before.Count; i++)
            {
                ids.Add(ths_before[i].Id);
            }

            // BASS初期化
            try
            {
                success = BASS.BASS_Init(device, freq, 0, (IntPtr)0, (IntPtr)0);
                BASS.BASS_SetConfig(BASS.BASS_CONFIG.BASS_CONFIG_BUFFER, buffer_len);
            }
            catch { }

            // 新しく生成されたスレッドのプライオリティを上げる
            var ths_after = System.Diagnostics.Process.GetCurrentProcess().Threads;
            for (int i = 0; i < ths_after.Count; i++)
            {
                var th = ths_after[i];
                if (ids.Contains(th.Id)) continue;
#if DEBUG
                Gageas.Lutea.Logger.Debug("スレッドID" + th.Id + " のプライオリティを上げます");
#endif
                th.PriorityLevel = System.Diagnostics.ThreadPriorityLevel.Highest;
            }
            return success;
        }

        /*
         * bass.dllの読み込みに成功したかどうか
         */
        public static bool isAvailable
        {
            get
            {
                try
                {
                    return BASS.BASS_GetVersion() > 0;
                }
                catch { return false; }
            }
        }

        [System.Obsolete("これはシステムのマスターボリュームを変更する。Channnel#volumeを使うべし")]
        public static float volume
        {
            get
            {
                return _BASS_GetVolume();
            }

            set
            {
                _BASS_SetVolume(value);
            }
        }

        public static IntPtr BASS_StreamCreateFile(Boolean ismemory, string filename, uint offset, uint length, uint flags)
        {
            try
            {
                return _BASS_StreamCreateFile(ismemory, filename, offset, length, BASS_UNICODE | flags);
            }
            catch (Exception)
            {
                return (IntPtr)0;
            }
        }

        public static bool BASS_Free()
        {
            return _BASS_Free();
        }
        
        public abstract class IPlayable : IDisposable
        {
            public enum FFT : uint
            {
                BASS_DATA_FFT256 = 0x80000000, // 256 sample FFT 
                BASS_DATA_FFT512 = 0x80000001, // 512 sample FFT 
                BASS_DATA_FFT1024 = 0x80000002, // 1024 FFT 
                BASS_DATA_FFT2048 = 0x80000003, // 2048 FFT 
                BASS_DATA_FFT4096 = 0x80000004, // 4096 FFT 
                BASS_DATA_FFT8192 = 0x80000005, // 8192 FFT 
                BASS_DATA_FFT_INDIVIDUAL = 0x10, // FFT flag: FFT for each channel, else all combined
                BASS_DATA_FFT_NOWINDOW = 0x20, // FFT flag: no Hanning window
            };
            public abstract bool Start();
            public abstract bool Resume();
            public abstract bool Stop();
            public abstract bool Pause();
            public abstract bool SetVolume(float vol);
            public abstract bool SetVolume(float vol,uint timespan);
            public abstract uint GetFreq();
            public abstract uint GetChans();
            public abstract float GetVolume();
            public abstract uint GetData(IntPtr buffer, uint length);
            public abstract uint GetDataFFT(float[] buffer, FFT fftparam);
            public abstract void Dispose();
            public abstract bool SetMute(bool mute);
        }

        public abstract class Channel : IPlayable, IDisposable
        {
            private _SyncProc dSyncProcProxyInvoker;
            protected bool disposed = false;

            // コンストラクタ
            protected Channel()
            {
                dSyncProcProxyInvoker = new _SyncProc(syncProcProxyInvoker);
            }
            /*
             * SyncProcのdelegateがGCに回収されないようにここで保持している。
             * BASS.dllにList内のindexを渡しているので、removeしてはいけない。
             * removeSyncはnullに書き換えることで行う
             */
//            private static Dictionary<int, SyncObject> syncProcs = new Dictionary<int, SyncObject>();            private static Dictionary<int, SyncObject> syncProcs = new Dictionary<int, SyncObject>();
            private List<SyncObject> syncProcs = new List<SyncObject>();
            public delegate void SyncProc(SYNC_TYPE type, object cookie);
            private class SyncObject
            {
                public IntPtr sync;
                public SYNC_TYPE type;
                public SyncProc proc;
                public object cookie;
                public SyncObject(SYNC_TYPE type, SyncProc proc, object cookie)
                {
                    this.type = type;
                    this.proc = proc;
                    this.cookie = cookie;
                }
            }
            protected IntPtr handle;
            public override void Dispose()
            {
                if (disposed) return;
                disposed = true;
                clearAllSync();
                GC.SuppressFinalize(this);
            }
            ~Channel(){
                this.Dispose();
            }
            public override bool Start()
            {
                return Start(true);
            }
            public override bool Resume()
            {
                return Start(false);
            }
            public bool Start(bool restart)
            {
                return _BASS_ChannelPlay(this.handle, restart);
            }
            public override bool Stop()
            {
                // _BASS_ChannelStopを使うと再開に難があるくさいので
                this.Pause();
                this.Start(true);
                return this.Pause();
            }
            public override bool Pause()
            {
                return _BASS_ChannelPause(this.handle);
            }
            public override bool SetMute(bool mute)
            {
                return true;
            }
            public UInt64 filesize{
                get
                {
                    return _BASS_ChannelGetLength(handle, 0);
                }
            }
            /*
             * 0 to 1
             */
            public float volume
            {
                set
                {
                    _BASS_ChannelSetAttribute(handle, (int)BASS_ATTRIB.VOL, value);
                }
                get
                {
                    float val;
                    _BASS_ChannelGetAttribute(handle, (int)BASS_ATTRIB.VOL, out val);
                    return val;
                }
            }
            public double length
            {
                get
                {
                    return _BASS_ChannelBytes2Seconds(handle, filesize);
                }
            }
            public UInt64 position
            {
                get
                {
                    return _BASS_ChannelGetPosition(handle, BASS_POS_BYTE);
                }
                set
                {
                    float v = volume;
                    volume = 0;
                    _BASS_ChannelSetPosition(handle, value, BASS_POS_BYTE);
                    volume = v;
                }
            }
            public double positionSec
            {
                get
                {
                    return _BASS_ChannelBytes2Seconds(handle, position);
                }
                set
                {
                    position = _BASS_ChannelSeconds2Bytes(handle, value);
                }
            }

            public UInt64 Seconds2Bytes(double pos)
            {
                return _BASS_ChannelSeconds2Bytes(handle, pos);
            }

            public double Bytes2Seconds(UInt64 pos)
            {
                return _BASS_ChannelBytes2Seconds(handle, pos);
            }

            public void setSync(SYNC_TYPE type, SyncProc callback, UInt64 data=0, object cookie = null){
                SyncObject sync = new SyncObject(type, callback, cookie);
                lock (syncProcs)
                {
                    syncProcs.Add(sync);
                    int user = syncProcs.IndexOf(sync);
                    sync.sync = _BASS_ChannelSetSync(handle, (UInt32)type, data, dSyncProcProxyInvoker, (IntPtr)user);
                }
            }

            // TODO: 個別のSyncのFreeに対応・・・syncまわりの設計変えなきゃならないし多分使わないと思うからいいかなぁ・・・
            public void clearAllSync()
            {
                lock (syncProcs)
                {
                    foreach (SyncObject sync in syncProcs)
                    {
                        _BASS_ChannelRemoveSync(handle, sync.sync);
                    }
                    syncProcs.Clear();
                }
            }

            public void syncProcProxyInvoker(IntPtr hsync, IntPtr handle, UInt32 data, IntPtr _user)
            {
                int user = (int)_user;
                if(syncProcs.Count>user){
                    SyncObject sync = syncProcs[(int)user];
                    sync.proc.Invoke(sync.type, sync.cookie);
                }
            }

            public BASS_CHANNELINFO Info
            {
                get
                {
                    BASS_CHANNELINFO info;
                    _BASS_ChannelGetInfo(handle, out info);
                    return info;
                }
            }

            public override uint GetData(IntPtr buffer, uint length)
            {
                return _BASS_ChannelGetData(handle, buffer, length);
            }

            public override uint GetDataFFT(float[] buf, IPlayable.FFT fft)
            {
                return _BASS_ChannelGetData(handle, buf, (uint)fft);
            }

            public override bool SetVolume(float vol)
            {
                if (vol < 0) vol = 0;
                volume = vol;
                return true;
            }

            public override bool SetVolume(float vol, uint timespan)
            {
                if (vol < 0) vol = 0;
                if (timespan == 0)
                {
                    return _BASS_ChannelSetAttribute(handle, (uint)BASS_ATTRIB.VOL, vol);
                }
                else
                {
                    return _BASS_ChannelSlideAttribute(handle, (uint)BASS_ATTRIB.VOL, vol, timespan);
                }
            }

            public override float GetVolume()
            {
                return volume;
            }

            public override uint GetFreq()
            {
                return Info.freq;
            }

            public override uint GetChans()
            {
                return Info.chans;
            }
        }

        public abstract class Stream : Channel
        {
            public enum StreamFlag : uint{
                BASS_STREAM_DECODE = 0x200000,
                BASS_STREAM_AUTOFREE =  0x40000,
                BASS_STREAM_FLOAT = 256,
            }
            override public void Dispose()
            {
                try
                {
                    base.Dispose();
                }finally{
                    if (handle != IntPtr.Zero)
                    {
                        _BASS_StreamFree(handle);
                        handle = IntPtr.Zero;
                    }
                }
            }
        }

        public class UserSampleStream : Stream
        {
            private STREAMPROC streamProc;
            private StreamProc proc;
            public UserSampleStream(uint freq, uint channels, StreamProc proc, StreamFlag flag)
            {
                this.streamProc = (handle, buffer, length, user) => this.disposed ? 0x80000000 : this.proc(buffer, length);
                this.proc = proc;
                this.handle = _BASS_StreamCreate(freq, channels, (uint)flag, this.streamProc, (IntPtr)this.GetHashCode());
            }
        }

        public class FileStream : Stream
        {
            public FileStream(String filename, StreamFlag flags = 0, ulong offset = 0, ulong length = 0)
            {
                IntPtr ret = _BASS_StreamCreateFile(false, filename, offset, length, (BASS_UNICODE | (uint)flags));
                if (ret == IntPtr.Zero)
                {
                    throw (new Exception("Could not create stream.\ncode is " + BASS_ErrorGetCode()));
                }
                handle = ret;
                _BASS_ChannelSetPosition(ret, 0, BASS_POS_BYTE);
            }
        }

        #region DLLImport BASS
        [DllImport("bass.dll", EntryPoint = "BASS_Init")]
        private static extern Boolean BASS_Init(int device, uint freq, uint flags, IntPtr hwnd, IntPtr guid);

        [DllImport("bass.dll", EntryPoint = "BASS_PluginLoad", CharSet = CharSet.Unicode)]
        private static extern IntPtr _BASS_PluginLoad(string filename, uint flags);

        [DllImport("bass.dll", EntryPoint = "BASS_PluginFree", CharSet = CharSet.Unicode)]
        private static extern bool _BASS_PluginFree(IntPtr plugin);

        [DllImport("bass.dll", EntryPoint = "BASS_PluginGetInfo", CharSet = CharSet.Unicode)]
        private static extern IntPtr _BASS_PluginGetInfo(IntPtr plugin);

        [DllImport("bass.dll", EntryPoint = "BASS_SetConfig", CharSet = CharSet.Unicode)]
        public static extern bool BASS_SetConfig(BASS_CONFIG option, UInt32 value);

        [DllImport("bass.dll", EntryPoint = "BASS_SetDevice", CharSet = CharSet.Unicode)]
        public static extern bool BASS_SetDevice(UInt32 device);

        [DllImport("bass.dll", EntryPoint = "BASS_StreamCreateFile", CharSet = CharSet.Unicode)]
        private static extern IntPtr _BASS_StreamCreateFile(Boolean ismemory, string filename, UInt64 offset, UInt64 length, uint flags);

        private delegate UInt32 STREAMPROC(IntPtr handle, IntPtr buffer, UInt32 length, IntPtr user);
        [DllImport("bass.dll", EntryPoint = "BASS_StreamCreate", CharSet = CharSet.Unicode)]
        private static extern IntPtr _BASS_StreamCreate(uint freq, uint chans, uint flags, STREAMPROC proc, IntPtr user);

        [DllImport("bass.dll", EntryPoint = "BASS_StreamFree")]
        private static extern bool _BASS_StreamFree(IntPtr stream);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelPlay")]
        private static extern bool _BASS_ChannelPlay(IntPtr chan, bool restart);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelStop")]
        private static extern bool _BASS_ChannelStop(IntPtr chan);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelPause")]
        private static extern bool _BASS_ChannelPause(IntPtr handle);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetLength")]
        private static extern UInt64 _BASS_ChannelGetLength(IntPtr handle, uint mode);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetData")]
        private static extern uint _BASS_ChannelGetData(IntPtr handle, IntPtr buffer, uint length);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetData")]
        private static extern uint _BASS_ChannelGetData(IntPtr handle, float[] buffer, uint length);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetInfo")]
        private static extern bool _BASS_ChannelGetInfo(IntPtr handle, out BASS_CHANNELINFO info);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetPosition")]
        private static extern UInt64 _BASS_ChannelGetPosition(IntPtr handle, uint mode);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelSetPosition")]
        private static extern bool _BASS_ChannelSetPosition(IntPtr handle, UInt64 pos, uint mode);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelBytes2Seconds")]
        private static extern double _BASS_ChannelBytes2Seconds(IntPtr handle, UInt64 pos);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelSeconds2Bytes")]
        private static extern UInt64 _BASS_ChannelSeconds2Bytes(IntPtr handle, double pos);

        private delegate void _SyncProc(IntPtr hsync, IntPtr handle, UInt32 data, IntPtr user);
        [DllImport("bass.dll", EntryPoint = "BASS_ChannelSetSync")]
        private static extern IntPtr _BASS_ChannelSetSync(IntPtr handle, UInt32 type, UInt64 param, _SyncProc proc, IntPtr user);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelRemoveSync")]
        private static extern bool _BASS_ChannelRemoveSync(IntPtr handle, IntPtr sync);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelSetAttribute")]
        private static extern bool _BASS_ChannelSetAttribute(IntPtr handle, UInt32 attrib, float value);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetAttribute")]
        private static extern bool _BASS_ChannelGetAttribute(IntPtr handle, UInt32 attrib, out float value);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelSlideAttribute")]
        private static extern bool _BASS_ChannelSlideAttribute(IntPtr handle, UInt32 attrib, float value, uint time);

        [DllImport("bass.dll", EntryPoint = "BASS_ErrorGetCode")]
        public static extern int BASS_ErrorGetCode();

        [DllImport("bass.dll", EntryPoint = "BASS_SetVolume")]
        private static extern bool _BASS_SetVolume(float volume);

        [DllImport("bass.dll", EntryPoint = "BASS_GetVolume")]
        private static extern float _BASS_GetVolume();

        [DllImport("bass.dll", EntryPoint = "BASS_GetVersion")]
        private static extern UInt32 BASS_GetVersion();

        [DllImport("bass.dll", EntryPoint = "BASS_GetDeviceInfo")]
        private static extern bool _BASS_GetDeviceInfo(UInt32 device, out BASS_DEVICEINFO info);

        [DllImport("bass.dll", EntryPoint = "BASS_Free")]
        private static extern bool _BASS_Free();
        #endregion
    }
}