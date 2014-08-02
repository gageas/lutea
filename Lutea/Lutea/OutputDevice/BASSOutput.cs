using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gageas.Lutea.Core;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.OutputDevice
{
    class BASSOutput : IOutputDevice
    {
        private BASS.IPlayable Bassout;
        private OutputDevice.StreamProc StreamProc;
        private bool _pause = false;

        public BASSOutput(uint freq, uint chans, string preferredDeviceName, int buflen)
        {
            var outdev = GetInitializedBassRealOutputDevice();
            if (outdev == 0)
            {
                int deviceid = -1;
                var devices = BASS.GetDevices();
                for (int i = 0; i < devices.Length; i++)
                {
                    var device = devices[i];
                    if (device.Name == preferredDeviceName)
                    {
                        deviceid = i;
                        Logger.Debug("Found preferred output device:" + device.ToString());
                        break;
                    }
                }
                BASS.BASS_Init(deviceid, freq, 1500);
                outdev = GetInitializedBassRealOutputDevice();
            }
            BASS.BASS_SetDevice(outdev);
            Bassout = new BASS.UserSampleStream(freq, chans, (x, y) => { if (StreamProc == null)return 0; return StreamProc(x, y); }, (BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) | BASS.Stream.StreamFlag.BASS_STREAM_AUTOFREE);
            Logger.Debug("Use Float Output");
        }

        public void SetStreamProc(OutputDevice.StreamProc proc)
        {
            this.StreamProc = proc;
        }

        public int Freq
        {
            get { return (int)Bassout.GetFreq(); }
        }

        public int Chans
        {
            get { return (int)Bassout.GetChans(); }
        }

        public bool CanAbort
        {
            get { return true; }
        }

        public bool Pause
        {
            get
            {
                return _pause;
            }
            set
            {
                _pause = value;
                if (value)
                {
                    Bassout.Pause();
                }
                else
                {
                    Bassout.Resume();
                }
            }
        }

        public float Volume
        {
            get { return Bassout.GetVolume(); }
            set { Bassout.SetVolume(value); }
        }

        public Controller.OutputModeEnum OutputMode
        {
            get { return Controller.OutputModeEnum.FloatingPoint; }
        }

        public Controller.Resolutions OutputResolution
        {
            get { return Controller.Resolutions.Unknown; }
        }

        public void Start()
        {
            Bassout.Start();
        }

        public void Stop()
        {
            Bassout.Stop();
        }

        public void Resume()
        {
            Bassout.Resume();
        }

        public ulong BufferedSamples
        {
            get { return (ulong)(Bassout.GetData(IntPtr.Zero, 0) / sizeof(float) / Chans); }
        }

        public static BASS.IPlayable.FFT MapFFTEnum(Controller.FFTNum fftNum)
        {
            switch (fftNum)
            {
                case Controller.FFTNum.FFT256:  return BASS.IPlayable.FFT.BASS_DATA_FFT256;
                case Controller.FFTNum.FFT512:  return BASS.IPlayable.FFT.BASS_DATA_FFT512;
                case Controller.FFTNum.FFT1024: return BASS.IPlayable.FFT.BASS_DATA_FFT1024;
                case Controller.FFTNum.FFT2048: return BASS.IPlayable.FFT.BASS_DATA_FFT2048;
                case Controller.FFTNum.FFT4096: return BASS.IPlayable.FFT.BASS_DATA_FFT4096;
                case Controller.FFTNum.FFT8192: return BASS.IPlayable.FFT.BASS_DATA_FFT8192;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public uint GetDataFFT(float[] buffer, Controller.FFTNum fftNum)
        {
            return Bassout.GetDataFFT(buffer, MapFFTEnum(fftNum));
        }

        /// <summary>
        /// BASS_Initが実行されたサウンド出力デバイスを取得する(除くno soundデバイス)
        /// </summary>
        /// <returns>最初に見つかったInitedなサウンド出力デバイスのデバイス。見つからなかった場合は0</returns>
        private uint GetInitializedBassRealOutputDevice()
        {
            uint a;
            BASS.BASS_DEVICEINFO? info;
            for (a = 1; (info = BASS.GetDeviceInfo(a)).HasValue; a++)
            {
                var _info = info.Value;
                if (_info.IsInit) return a;
            }
            return 0;
        }

        public void Dispose()
        {
            if (Bassout != null)
            {
                Bassout.Dispose();
                Bassout = null;
            }
        }
    }
}
