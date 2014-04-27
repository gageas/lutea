using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Gageas.Wrapper.BASS;


namespace Gageas.Wrapper.BASS
{
    class BASSWASAPIOutput : BASS.IPlayable, IDisposable
    {
        private delegate UInt32 WASAPIStreamProc(IntPtr buffer, UInt32 length, IntPtr user);

        /// <summary>
        /// 現在のデバイスについての情報を保持する構造体
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct BASS_WASAPI_INFO
        {
            public enum Formats : uint
            {
                BASS_WASAPI_FORMAT_FLOAT = 0,
                BASS_WASAPI_FORMAT_8BIT = 1,
                BASS_WASAPI_FORMAT_16BIT = 2,
                BASS_WASAPI_FORMAT_24BIT = 3,
                BASS_WASAPI_FORMAT_32BIT = 4
            }

            public InitFlags Initflags;
            public uint Freq;
            public uint Chans;
            public Formats Format;
            public uint Buflen;
            public uint Volmax;
            public uint Volmin;
            public uint Volstep;
        }

        /// <summary>
        /// デバイスについての情報を保持する構造体
        /// </summary>
        public struct BASS_WASAPI_DEVICEINFO
        {
            public enum Types : uint
            {
                BASS_WASAPI_TYPE_NETWORKDEVICE = 0, // A network device.
                BASS_WASAPI_TYPE_SPEAKERS = 1, // A speakers device.
                BASS_WASAPI_TYPE_LINELEVEL = 2, // A line level device.
                BASS_WASAPI_TYPE_HEADPHONES = 3, // A headphone device.
                BASS_WASAPI_TYPE_MICROPHONE = 4, // A microphone device.
                BASS_WASAPI_TYPE_HEADSET = 5, // A headset device.
                BASS_WASAPI_TYPE_HANDSET = 6, // A handset device.
                BASS_WASAPI_TYPE_DIGITAL = 7, // A digital device.
                BASS_WASAPI_TYPE_SPDIF = 8, // A S/PDIF device.
                BASS_WASAPI_TYPE_HDMI = 9, // A HDMI device.
                BASS_WASAPI_TYPE_UNKNOWN = 10, // An unknown device.
            }

            [Flags]
            public enum Flags : uint
            {
                BASS_DEVICE_ENABLED = 1,
                BASS_DEVICE_DEFAULT = 2,
                BASS_DEVICE_INIT = 4,
                BASS_DEVICE_LOOPBACK = 8,
                BASS_DEVICE_INPUT = 16,
            }

            private IntPtr name;
            private IntPtr id;
            public Types Type;
            public Flags Flag;
            public Single Minperiod;
            public Single Defperiod;
            public UInt32 Mixfreq;
            public UInt32 Mixchans;

            public string Name
            {
                get
                {
                    return Marshal.PtrToStringAnsi(this.name);
                }
            }

            public string ID
            {
                get
                {
                    return Marshal.PtrToStringAnsi(this.id);
                }
            }

            public override string ToString()
            {
                return
                    "Name: " + Name + "\n" +
                    "ID  : " + ID + "\n" +
                    "Type  : " + Type + "\n" +
                    "Flags  : " + Flag + "\n" +
                    "Mixfreq  : " + Mixfreq + "\n" +
                    "Mixchans  : " + Mixchans + "\n";
            }
        }

        [Flags]
        public enum InitFlags : uint
        {
            Exclusive = 1,
            AutoFormat = 2,
            Buffer = 4,
        }

        public class BASSWASAPIException : Exception
        {
        }

        public static bool IsAvailable
        {
            private set;
            get;
        }

        private WASAPIStreamProc streamProc;
        private BASS.StreamProc userStreamProc;
        private bool running = false;
        private bool disposed = true;
        private static List<int> bassThreadIDs = new List<int>();

        private IntPtr pauseBackup = IntPtr.Zero;
        private uint pauseBackupLen = 0;
        private uint pauseBackupPos = 0;

        #region Static Constructor
        static BASSWASAPIOutput()
        {
            IsAvailable = false;
            try
            {
                uint wasapiVer = 0;
                wasapiVer = BASS_WASAPI_GetVersion();
                if (wasapiVer == 0) return;
                IsAvailable = true;
            }
            catch { }
        }
        #endregion

        public BASSWASAPIOutput(uint freq, uint chans, BASS.StreamProc proc, int device = -1, uint bufferLen = 0)
            : this(freq, chans, proc, InitFlags.Buffer, device, bufferLen)
        {
        }

        public BASSWASAPIOutput(uint freq, uint chans, BASS.StreamProc proc, InitFlags flag, int device = -1, uint bufferLen = 0)
        {

            bool success = false;
            this.userStreamProc = proc;
            this.streamProc = WrapperStreamProc;

            // Init前から走っていたスレッドのIdを保持
            var ths_before = System.Diagnostics.Process.GetCurrentProcess().Threads;
            List<int> ids = new List<int>();
            for (int i = 0; i < ths_before.Count; i++)
            {
                ids.Add(ths_before[i].Id);
            }
            success = BASS_WASAPI_Init(device, freq, chans, (uint)flag, (bufferLen / 1000.0F), 0.0F, streamProc, IntPtr.Zero);

            // 新しく生成されたスレッドのプライオリティを上げる
            var ths_after = System.Diagnostics.Process.GetCurrentProcess().Threads;
            for (int i = 0; i < ths_after.Count; i++)
            {
                var th = ths_after[i];
                if (ids.Contains(th.Id)) continue;
                if (!bassThreadIDs.Contains(th.Id))
                {
                    bassThreadIDs.Add(th.Id);
                }
            }
            if (!success)
            {
                throw new BASSWASAPIException();
            }
            disposed = false;
        }

        private UInt32 WrapperStreamProc(IntPtr buffer, UInt32 length, IntPtr user)
        {
            if (pauseBackup == IntPtr.Zero)
            {
                return userStreamProc(buffer, length);
            }

            // Pause時のバックアップからコピーする
            uint tocopy = Math.Min(length, pauseBackupLen - pauseBackupPos);
            MoveMemory(buffer, IntPtr.Add(pauseBackup, (int)pauseBackupPos), (int)tocopy);
            pauseBackupPos += tocopy;
            if (pauseBackupPos == pauseBackupLen)
            {
                Marshal.FreeHGlobal(pauseBackup);
                pauseBackup = IntPtr.Zero;
            }
            if (tocopy == length) return length;
            return userStreamProc(IntPtr.Add(buffer, (int)tocopy), length - tocopy) + tocopy;
        }

        public override void Dispose()
        {
            if (this.disposed) return;
            Stop();
            BASS_WASAPI_Free();
            if (pauseBackup != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pauseBackup);
                pauseBackup = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
            this.disposed = true;
        }

        ~BASSWASAPIOutput()
        {
            this.Dispose();
        }

        public static void SetPriority(System.Diagnostics.ThreadPriorityLevel priority)
        {
            var ths = System.Diagnostics.Process.GetCurrentProcess().Threads;
            bassThreadIDs = bassThreadIDs.FindAll((e) =>
            {
                for (int i = 0; i < ths.Count; i++)
                {
                    if (e == ths[i].Id)
                    {
#if DEBUG
                        Gageas.Lutea.Logger.Log("スレッドID" + ths[i].Id + " のプライオリティ" + priority);
#endif
                        ths[i].PriorityLevel = priority;
                        return true;
                    }
                }
                return false;
            });
        }

        public override bool IsFloatData()
        {
            // BASS WASAPI always uses Float.
            return true;
        }

        public override bool CanAbort()
        {
            return false;
        }

        public override bool Abort()
        {
            return false;
        }

        public override bool Start()
        {
            if (disposed) return false;
            return running = BASS_WASAPI_Start();
        }

        public override bool Stop()
        {
            if (disposed) return false;
            running = false;
            if (pauseBackup != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pauseBackup);
                pauseBackup = IntPtr.Zero;
            }
            return BASS_WASAPI_Stop(true);
        }

        public override bool Resume()
        {
            if (disposed) return false;
            if (running) return true;
            return running = BASS_WASAPI_Start();
        }

        public override bool Pause()
        {
            if (disposed) return false;
            running = false;

            // バッファに入ってるデータを退避する
            var buflen = Info.Buflen;
            if (pauseBackup != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pauseBackup);
            }
            pauseBackup = Marshal.AllocHGlobal((int)buflen);
            pauseBackupLen = BASS_WASAPI_GetData(pauseBackup, buflen);
            pauseBackupPos = 0;
            if (pauseBackupLen == 0)
            {
                Marshal.FreeHGlobal(pauseBackup);
                pauseBackup = IntPtr.Zero;
            }
            return BASS_WASAPI_Stop(true);
        }

        public override bool SetVolume(float vol, uint timespan)
        {
            return this.SetVolume(vol);
        }
        public override bool SetVolume(float vol)
        {
            return false;
        }

        public override float GetVolume()
        {
            return 1.0F;
        }

        public override bool SetMute(bool mute)
        {
            return BASS_WASAPI_SetMute(mute);
        }

        public BASS_WASAPI_INFO Info
        {
            get
            {
                BASS_WASAPI_INFO info;
                BASS_WASAPI_GetInfo(out info);
                return info;
            }
        }

        /// <summary>
        /// 全てのデバイスのデバイス情報の配列を返す
        /// </summary>
        /// <returns>デバイス情報の配列</returns>
        public static BASS_WASAPI_DEVICEINFO[] GetDevices()
        {
            UInt32 id = 0;
            BASS_WASAPI_DEVICEINFO info = new BASS_WASAPI_DEVICEINFO();
            List<BASS_WASAPI_DEVICEINFO> list = new List<BASS_WASAPI_DEVICEINFO>();
            while (BASS_WASAPI_GetDeviceInfo(id, out info))
            {
                list.Add(info);
                id++;
            }
            return list.ToArray();
        }

        public override uint GetFreq()
        {
            return Info.Freq;
        }

        public override uint GetChans()
        {
            return Info.Chans;
        }

        public override uint GetData(IntPtr buffer, uint length)
        {
            return BASS_WASAPI_GetData(buffer, length);
        }

        public override uint GetDataFFT(float[] buffer, BASS.IPlayable.FFT fftparam)
        {
            return BASS_WASAPI_GetData(buffer, (uint)fftparam);
        }

        #region DLLImport BASSWASAPI
        [DllImport("Kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        static extern void MoveMemory(IntPtr dest, IntPtr src, int size);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_Init")]
        private static extern bool BASS_WASAPI_Init(int device, uint freq, uint chans, uint flags,
            float buffer, float period, WASAPIStreamProc proc, IntPtr user);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_Free")]
        private static extern bool BASS_WASAPI_Free();

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_Stop")]
        private static extern bool BASS_WASAPI_Stop(bool reset);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_Start")]
        private static extern bool BASS_WASAPI_Start();

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_GetInfo")]
        private static extern bool BASS_WASAPI_GetInfo(out BASSWASAPIOutput.BASS_WASAPI_INFO info);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_GetDeviceInfo")]
        private static extern bool BASS_WASAPI_GetDeviceInfo(UInt32 device, out BASSWASAPIOutput.BASS_WASAPI_DEVICEINFO devinfo);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_GetVersion")]
        private static extern uint BASS_WASAPI_GetVersion();

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_SetVolume")]
        private static extern bool BASS_WASAPI_SetVolume(bool linear, float volume);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_GetVolume")]
        private static extern float BASS_WASAPI_GetVolume(bool lenear);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_GetData")]
        private static extern uint BASS_WASAPI_GetData(IntPtr buffer, uint length);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_GetData")]
        private static extern uint BASS_WASAPI_GetData(float[] buffer, uint length);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_SetMute")]
        private static extern bool BASS_WASAPI_SetMute(bool mute);
        #endregion
    }
}
