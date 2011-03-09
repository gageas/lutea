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
        private delegate UInt32 WASAPISTREAMPROC(IntPtr buffer, UInt32 length, IntPtr user);

        public struct BASS_WASAPI_INFO
        {
            public uint initflags;
            public uint freq;
            public uint chans;
            public uint format;
            public uint buflen;
            public uint volmax;
            public uint volmin;
            public uint volstep;
        }

        [Flags]
        public enum Flags : uint
        {
            Exclusive = 1,
            AutoFormat = 2,
            Buffer = 4,
        }

        public class BASSWASAPIException : Exception
        {
        }

        private static BASSWASAPIOutput lastConstructed = null;
        private static bool isAvailable = false;
        public static bool IsAvailable
        {
            get { return isAvailable; }
        }

        private static WASAPISTREAMPROC streamProc = (buffer, length, user) =>
            (lastConstructed == null || lastConstructed.disposed)
                ? 0x80000000
                : lastConstructed.originalStreamProc(buffer, length);

        private bool running = false;
        private BASS.StreamProc originalStreamProc;
        private bool disposed = true;
        private bool volumeAdjust = false;
        private static List<int> bassThreadIDs = new List<int>();
        
        #region Static Constructor
        static BASSWASAPIOutput()
        {
            try
            {
                uint wasapiVer = 0;
                wasapiVer = BASS_WASAPI_GetVersion();
                if (wasapiVer == 0) return;
                using (var test = new BASSWASAPIOutput(44100, 2, null))
                {
                    if (test != null) isAvailable = true;
                }
            }
            catch { }
        }
        #endregion

        public BASSWASAPIOutput(uint freq, uint chans, BASS.StreamProc proc, bool volumeAdjust = false)
            : this(freq, chans, proc, Flags.Buffer, volumeAdjust)
        {
        }

        public BASSWASAPIOutput(uint freq, uint chans, BASS.StreamProc proc, Flags flag, bool volumeAdjust = false)
        {
            bool success = false;
            if (lastConstructed != null)
            {
                lastConstructed.Dispose();
                lastConstructed = null;
            }
            this.originalStreamProc = proc;

            // Init前から走っていたスレッドのIdを保持
            var ths_before = System.Diagnostics.Process.GetCurrentProcess().Threads;
            List<int> ids = new List<int>();
            for (int i = 0; i < ths_before.Count; i++)
            {
                ids.Add(ths_before[i].Id);
            }
            success = BASS_WASAPI_Init(-1, freq, chans, (uint)flag, 0.5F, 0.0F, streamProc, IntPtr.Zero);

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
            this.volumeAdjust = volumeAdjust;
            lastConstructed = this;
        }

        public override void Dispose()
        {
            if (this.disposed) return;
            Stop();
            BASS_WASAPI_Free();
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

        public override bool Start()
        {
            if (disposed) return false;
            return running = BASS_WASAPI_Start();
        }

        public override bool Stop()
        {
            if (disposed) return false;
            running = false;
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
            return BASS_WASAPI_Stop(false);
        }

        public override bool SetVolume(float vol, uint timespan)
        {
            return this.SetVolume(vol);
        }
        public override bool SetVolume(float vol)
        {
            if (!volumeAdjust)
            {
                if (vol == -1)
                {
                    BASS_WASAPI_Stop(true);
                }
            }
            if (!volumeAdjust) return false;
            return BASS_WASAPI_SetVolume(true, vol);
        }

        public override float GetVolume()
        {
            if (!volumeAdjust) return 1.0F;
            return BASS_WASAPI_GetVolume(true);
        }

        public override bool SetMute(bool mute)
        {
            return BASS_WASAPI_SetMute(mute);
        }

        public BASS_WASAPI_INFO Info
        {
            get
            {
                BASS_WASAPI_INFO info = new BASS_WASAPI_INFO();
                BASS_WASAPI_GetInfo(out info);
                return info;
            }
        }

        public override uint GetFreq()
        {
            return Info.freq;
        }

        public override uint GetChans()
        {
            return Info.chans;
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
        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_Init")]
        private static extern bool BASS_WASAPI_Init(int device, uint freq, uint chans, uint flags,
            float buffer, float period, WASAPISTREAMPROC proc, IntPtr user);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_Free")]
        private static extern bool BASS_WASAPI_Free();

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_Stop")]
        private static extern bool BASS_WASAPI_Stop(bool reset);

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_Start")]
        private static extern bool BASS_WASAPI_Start();

        [DllImport("basswasapi.dll", EntryPoint = "BASS_WASAPI_GetInfo")]
        private static extern bool BASS_WASAPI_GetInfo(out BASSWASAPIOutput.BASS_WASAPI_INFO info);

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
