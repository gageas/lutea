using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gageas.Lutea.Core;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.OutputDevice
{
    class BASSWASAPIOutputChannel : IOutputDevice
    {
        private BASSWASAPIOutput BassWasapiOutput;
        private OutputDevice.StreamProc StreamProc;
        private bool Exclusive;
        private bool _pause = false;

        public BASSWASAPIOutputChannel(bool exclusive, uint freq, uint chans, string preferredDeviceName, uint bufLen)
        {
            this.Exclusive = exclusive;
            BASS.BASS_SetDevice(0);

            int deviceid = -1;
            var devices = BASSWASAPIOutput.GetDevices();
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                if ((device.Name == preferredDeviceName) &&
                    ((device.Flag & BASSWASAPIOutput.BASS_WASAPI_DEVICEINFO.Flags.BASS_DEVICE_INPUT) == 0) &&
                    (device.Mixchans >= chans))
                {
                    deviceid = i;
                    Logger.Debug("Found preferred output device:" + device.ToString());
                    break;
                }
            }
            BassWasapiOutput = new BASSWASAPIOutput(freq, chans, (x, y) => { if (StreamProc == null)return 0; return StreamProc(x, y); }, BASSWASAPIOutput.InitFlags.Buffer | (exclusive ? BASSWASAPIOutput.InitFlags.Exclusive : 0), deviceid, bufLen);
            Logger.Log("Use WASAPI Exclusive Output: freq=" + BassWasapiOutput.Info.Freq + ", format=" + BassWasapiOutput.Info.Format);
        }

        public void SetStreamProc(OutputDevice.StreamProc proc)
        {
            this.StreamProc = proc;
        }

        public bool CanAbort
        {
            get { return BassWasapiOutput.CanAbort(); }
        }

        public void Dispose()
        {
            if (BassWasapiOutput != null)
            {
                BassWasapiOutput.Dispose();
                BassWasapiOutput = null;
            }
        }

        public float Volume
        {
            get
            {
                return 1.0f;
            }
            set
            {
            }
        }

        public int Chans
        {
            get { return (int)BassWasapiOutput.GetChans(); }
        }

        public int Freq
        {
            get { return (int)BassWasapiOutput.GetFreq(); }
        }

        public Core.Controller.OutputModeEnum OutputMode
        {
            get
            {
                return Exclusive
                    ? Core.Controller.OutputModeEnum.WASAPIEx
                    : Controller.OutputModeEnum.WASAPI;
            }
        }

        public Core.Controller.Resolutions OutputResolution
        {
            get
            {
                switch (BassWasapiOutput.Info.Format)
                {
                    case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_FLOAT: return Controller.Resolutions.float_32bit;
                    case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_8BIT: return Controller.Resolutions.integer_8bit;
                    case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_16BIT: return Controller.Resolutions.integer_16bit;
                    case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_24BIT: return Controller.Resolutions.integer_24bit;
                    case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_32BIT: return Controller.Resolutions.integer_32bit;
                    default: return Controller.Resolutions.Unknown;
                }
            }
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
                    BassWasapiOutput.Pause();
                }
                else
                {
                    BassWasapiOutput.Resume();
                }
            }
        }

        public void Start()
        {
            BassWasapiOutput.Start();
        }

        public void Stop()
        {
            BassWasapiOutput.Stop();
        }

        public void Resume()
        {
            BassWasapiOutput.Resume();
        }

        public ulong BufferedSamples
        {
            get { return (ulong)(BassWasapiOutput.GetData(IntPtr.Zero, 0) / sizeof(float) / Chans); }
        }

        public uint GetDataFFT(float[] buffer, Controller.FFTNum fftNum)
        {
            return BassWasapiOutput.GetDataFFT(buffer, BASSOutput.MapFFTEnum(fftNum));
        }
    }
}
