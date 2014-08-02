using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;

namespace Gageas.Wrapper.BASS
{
    /// <summary>
    /// BASS Audio(BASS.dll)のラッパ
    /// </summary>
    public class BASS
    {
        internal const uint BASS_UNICODE = 0x80000000;
        internal const uint BASS_POS_BYTE = 0;

        /// <summary>
        /// ストリームプロシージャのデリゲート
        /// </summary>
        /// <param name="bffer">バッファへのポインタ</param>
        /// <param name="length">バッファ長</param>
        /// <returns>バッファへ出力したデータ長</returns>
        public delegate UInt32 StreamProc(IntPtr bffer, UInt32 length);

        /// <summary>
        /// BASS内の例外
        /// </summary>
        public class BASSException : Exception
        {
            /// <summary>
            /// BASSのエラーコード
            /// </summary>
            public readonly int BASSErrCode;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="msg">エラーメッセージ</param>
            /// <param name="errCode">BASSのエラーコード</param>
            public BASSException(string msg, int errCode)
                : base(msg)
            {
                this.BASSErrCode = errCode;
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="msg">エラーメッセージ</param>
            public BASSException(string msg)
                : base(msg)
            {
                this.BASSErrCode = BASS_ErrorGetCode();
            }

            /// <summary>
            /// ToStringの実装
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return Message + "; code = " + BASSErrCode;
            }
        }

        /// <summary>
        /// BASS_ChannelSlideAttributeで使うAttributes
        /// </summary>
        private enum BASS_ATTRIB
        {
            FREQ = 1, VOL, PAN, EAXMIX,
            MUSIC_AMPLIFY = 0x100, MUSIC_PANSEP, MUSIC_PSCALER, MUSIC_BPM, MUSIC_SPEED, MUSIC_VOL_GLOBAL, MUSIC_VOL_CHAN = 0x200, MUSIC_VOL_INST = 0x300
        }

        /// <summary>
        /// BASS Config列挙体
        /// </summary>
        private enum BASS_CONFIG : uint
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
            BASS_CONFIG_ASYNCFILE_BUFFER = 45,
        }

        /// <summary>
        /// BASS Channnel情報構造体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BASS_CHANNELINFO
        {
            /// <summary>
            /// サンプリング周波数
            /// </summary>
            public UInt32 Freq;

            /// <summary>
            /// チャンネル数
            /// </summary>
            public UInt32 Chans;

            /// <summary>
            /// StreamFlag
            /// </summary>
            public Stream.StreamFlag Flags;

            /// <summary>
            /// Ctype
            /// </summary>
            public UInt32 Ctype;

            /// <summary>
            /// OrigRes
            /// </summary>
            public UInt32 OrigRes;

            /// <summary>
            /// Plugin
            /// </summary>
            public IntPtr Plugin;

            /// <summary>
            /// Sample
            /// </summary>
            public IntPtr Sample;

            /// <summary>
            /// ファイル名(ポインタ)
            /// </summary>
            private IntPtr filename;

            /// <summary>
            /// ファイル名
            /// </summary>
            public string Filename
            {
                get
                {
                    return Marshal.PtrToStringUni(filename);
                }
            }
        }

        /// <summary>
        /// BASSデバイス情報構造体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BASS_DEVICEINFO {
            [Flags]
            private enum FLAGS : uint
            {
                ENABLED = 1, DEFAULT = 2, INIT = 4
            }

            private IntPtr name;
            private IntPtr driver;
            private UInt32 flags;

            /// <summary>
            /// デバイス名
            /// </summary>
            public string Name
            {
                get
                {
                    return Marshal.PtrToStringAnsi(this.name);
                }
            }

            /// <summary>
            /// ID
            /// </summary>
            public string Driver
            {
                get
                {
                    return Marshal.PtrToStringAnsi(this.driver);
                }
            }

            private FLAGS Flags
            {
                get
                {
                    return (FLAGS)this.flags;
                }
            }

            /// <summary>
            /// 有効かどうか
            /// </summary>
            public bool IsEnabled
            {
                get { return (Flags & FLAGS.ENABLED) != 0; }
            }

            /// <summary>
            /// 既定デバイスかどうか
            /// </summary>
            public bool IsDefault
            {
                get { return (Flags & FLAGS.DEFAULT) != 0; }
            }

            /// <summary>
            /// このデバイスで初期化されているかどうか
            /// </summary>
            public bool IsInit
            {
                get { return (Flags & FLAGS.INIT) != 0; }
            }
            
            public override string ToString()
            {
                return "Name:" + Name + "ID: " + Driver + "Flags" + Flags;
            }
        }

        private static List<int> bassThreadIDs = new List<int>();

        public static BASS_DEVICEINFO? GetDeviceInfo(UInt32 device)
        {
            BASS_DEVICEINFO info;
            bool success = _BASS_GetDeviceInfo(device, out info);
            return success ? info : (BASS_DEVICEINFO?)null;
        }

        /// <summary>
        /// 全てのデバイスのデバイス情報の配列を返す
        /// </summary>
        /// <returns>デバイス情報の配列</returns>
        public static BASS_DEVICEINFO[] GetDevices()
        {
            UInt32 id = 0;
            BASS_DEVICEINFO? info = new BASS_DEVICEINFO();
            List<BASS_DEVICEINFO> list = new List<BASS_DEVICEINFO>();
            while ((info = GetDeviceInfo(id)) != null)
            {
                list.Add(info.Value);
                id++;
            }
            return list.ToArray();
        }

        private static int[] GetThreadIdsArray()
        {
            var ths_before = System.Diagnostics.Process.GetCurrentProcess().Threads;
            var ids = new int[ths_before.Count];
            for (int i = 0; i < ths_before.Count; i++)
            {
                ids[i] = ths_before[i].Id;
            }
            return ids;
        }

        /// <summary>
        /// BASSを初期化
        /// </summary>
        /// <param name="device">デバイス番号</param>
        /// <param name="freq">サンプリング周波数</param>
        /// <param name="buffer_len">バッファ長(ms)</param>
        /// <returns>成功したかどうか</returns>
        public static bool BASS_Init(int device, uint freq = 44100, uint buffer_len = 1500)
        {
            bool success = false;
            // Init前から走っていたスレッドのIdを保持
            var ids = GetThreadIdsArray();

            // BASS初期化
            try
            {
                success = BASS.BASS_Init(device, freq, 0, (IntPtr)0, (IntPtr)0);
                BASS.BASS_SetConfig(BASS.BASS_CONFIG.BASS_CONFIG_BUFFER, buffer_len);
            }
            catch { }

            // 新しく生成されたスレッドを保持
            bassThreadIDs.Union(ids.Except(GetThreadIdsArray()));

            return success;
        }

        public static void SetPriority(System.Diagnostics.ThreadPriorityLevel priority)
        {
            var ths = System.Diagnostics.Process.GetCurrentProcess().Threads;
            bassThreadIDs = bassThreadIDs.FindAll((e) =>
            {
                foreach (System.Diagnostics.ProcessThread th in ths)
                {
                    if (e == th.Id)
                    {
#if DEBUG
                        Gageas.Lutea.Logger.Debug("スレッドID" + th.Id + " のプライオリティ" + priority);
#endif
                        th.PriorityLevel = priority;
                        return true;
                    }
                }
                return false;
            });
        }

        /// <summary>
        /// bass.dllの読み込みに成功したかどうか
        /// </summary>
        public static bool IsAvailable
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

        /// <summary>
        /// BASSを解放
        /// </summary>
        /// <returns>成功</returns>
        public static bool BASS_Free()
        {
            return _BASS_Free();
        }
        
        /// <summary>
        /// BASSにおけるIPlayable
        /// </summary>
        public abstract class IPlayable : IDisposable
        {
            /// <summary>
            /// FFTサイズ, フラグ
            /// </summary>
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
            /// <summary>
            /// Start
            /// </summary>
            /// <returns></returns>
            public abstract bool Start();

            /// <summary>
            /// Resume
            /// </summary>
            /// <returns></returns>
            public abstract bool Resume();

            /// <summary>
            /// Stop
            /// </summary>
            /// <returns></returns>
            public abstract bool Stop();

            /// <summary>
            /// Pause
            /// </summary>
            /// <returns></returns>
            public abstract bool Pause();

            /// <summary>
            /// CanAbort
            /// </summary>
            /// <returns></returns>
            public abstract bool CanAbort();

            /// <summary>
            /// Abort
            /// </summary>
            /// <returns></returns>
            public abstract bool Abort();
            public abstract bool SetVolume(float vol);
            public abstract bool SetVolume(float vol,uint timespan);
            public abstract uint GetFreq();
            public abstract uint GetChans();
            public abstract bool IsFloatData();
            public abstract float GetVolume();
            public abstract uint GetData(IntPtr buffer, uint length);
            public abstract uint GetDataFFT(float[] buffer, FFT fftparam);
            public abstract void Dispose();
            public abstract bool SetMute(bool mute);
        }

        public abstract class Channel : IPlayable
        {
            private UInt64 positionCache = 0;
            protected bool disposed = false;

            // コンストラクタ
            protected Channel()
            {
            }
            protected IntPtr handle;
            public override void Dispose()
            {
                if (disposed) return;
                disposed = true;
                GC.SuppressFinalize(this);
            }
            ~Channel(){
                this.Dispose();
            }
            public override bool IsFloatData()
            {
                return ((Info.Flags & Stream.StreamFlag.BASS_STREAM_FLOAT) == 0) ? false : true;
            }
            public override bool CanAbort()
            {
                return true;
            }
            public override bool Abort()
            {
                return _BASS_ChannelStop(this.handle);
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
                positionCache = 0;
                return _BASS_ChannelStop(this.handle);
            }
            public override bool Pause()
            {
                return _BASS_ChannelPause(this.handle);
            }
            public override bool SetMute(bool mute)
            {
                return true;
            }

            UInt64 _filesize = UInt64.MaxValue;
            public UInt64 filesize{
                get
                {
                    // 結果をキャッシュ
                    if (_filesize == UInt64.MaxValue)
                    {
                        _filesize = _BASS_ChannelGetLength(handle, 0);
                    }
                    return _filesize;
                }
            }

            // 0 to 1
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
                    if (positionCache != 0) return positionCache;
                    return positionCache = _BASS_ChannelGetPosition(handle, BASS_POS_BYTE);
                }
                set
                {
                    float v = volume;
                    volume = 0;
                    _BASS_ChannelSetPosition(handle, value, BASS_POS_BYTE);
                    positionCache = _BASS_ChannelGetPosition(handle, BASS_POS_BYTE);
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

            private BASS_CHANNELINFO? _info;
            public BASS_CHANNELINFO Info
            {
                get
                {
                    // 一回実行したら結果をキャッシュする
                    if (_info == null)
                    {
                        BASS_CHANNELINFO info;
                        _BASS_ChannelGetInfo(handle, out info);
                        _info = info;
                    }
                    return _info.Value;
                }
            }

            public override uint GetData(IntPtr buffer, uint length)
            {
                var readlen = _BASS_ChannelGetData(handle, buffer, length);
                if (readlen == 0xffffffff) // err
                {
                    // 終端してもpositionがfilesizeに到達しない場合があるので強制的にpositionCacheをfilesizeに書き換える。
                    if (positionCache != 0) positionCache = filesize;
                    readlen = 0;
                }
                positionCache += readlen;
                return readlen;
            }

            public override uint GetDataFFT(float[] buf, IPlayable.FFT fft)
            {
                var readlen = _BASS_ChannelGetData(handle, buf, (uint)fft);
                if (readlen == 0xffffffff) // err
                {
                    // 終端してもpositionがfilesizeに到達しない場合があるので強制的にpositionCacheをfilesizeに書き換える。
                    if (positionCache != 0) positionCache = filesize;
                    readlen = 0;
                }
                positionCache += readlen;
                return readlen;
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
                return Info.Freq;
            }

            public override uint GetChans()
            {
                return Info.Chans;
            }
        }

        public abstract class Stream : Channel
        {
            public enum StreamFlag : uint{
                BASS_STREAM_DECODE = 0x200000,
                BASS_STREAM_AUTOFREE =  0x40000,
                BASS_STREAM_FLOAT = 256,
                BASS_STREAM_ASYNCFILE = 0x40000000,
                BASS_STREAM_PRESCAN = 0x20000,
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
                if (this.handle == IntPtr.Zero)
                {
                    throw new BASSException("UserSampleStream: BASS_StreamCreate Failed.");
                }
            }
        }

        public class FileStream : Stream
        {
            public FileStream(String filename, StreamFlag flags = 0, ulong offset = 0, ulong length = 0)
            {
                IntPtr ret = _BASS_StreamCreateFile(false, filename, offset, length, (BASS_UNICODE | (uint)flags));
                if (ret == IntPtr.Zero)
                {
                    throw (new BASSException("Could not create stream."));
                }
                handle = ret;
                _BASS_ChannelSetPosition(ret, 0, BASS_POS_BYTE);
            }
        }

        #region DLLImport BASS
        [DllImport("bass.dll", EntryPoint = "BASS_Init")]
        private static extern Boolean BASS_Init(int device, uint freq, uint flags, IntPtr hwnd, IntPtr guid);

        [DllImport("bass.dll", EntryPoint = "BASS_SetConfig", CharSet = CharSet.Unicode)]
        private static extern bool BASS_SetConfig(BASS_CONFIG option, UInt32 value);

        /// <summary>
        /// 使用デバイスを設定
        /// </summary>
        /// <param name="device">デバイスID</param>
        /// <returns>成功</returns>
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

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelSetAttribute")]
        private static extern bool _BASS_ChannelSetAttribute(IntPtr handle, UInt32 attrib, float value);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelGetAttribute")]
        private static extern bool _BASS_ChannelGetAttribute(IntPtr handle, UInt32 attrib, out float value);

        [DllImport("bass.dll", EntryPoint = "BASS_ChannelSlideAttribute")]
        private static extern bool _BASS_ChannelSlideAttribute(IntPtr handle, UInt32 attrib, float value, uint time);

        [DllImport("bass.dll", EntryPoint = "BASS_ErrorGetCode")]
        private static extern int BASS_ErrorGetCode();

        [DllImport("bass.dll", EntryPoint = "BASS_GetVersion")]
        private static extern UInt32 BASS_GetVersion();

        [DllImport("bass.dll", EntryPoint = "BASS_GetDeviceInfo")]
        private static extern bool _BASS_GetDeviceInfo(UInt32 device, out BASS_DEVICEINFO info);

        [DllImport("bass.dll", EntryPoint = "BASS_Free")]
        private static extern bool _BASS_Free();
        #endregion
    }
}