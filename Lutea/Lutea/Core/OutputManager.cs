using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gageas.Wrapper.BASS;
using System.Threading;

namespace Gageas.Lutea.Core
{
    class OutputManager
    {
        private delegate BASS.IPlayable OutputChannelBuilder(uint freq, uint chans, string preferredDeviceName, uint bufferLength);

        /// <summary>
        /// 出力ストリーム
        /// </summary>
        private BASS.IPlayable outputChannel;
        private object outputChannelLock = new object();
        private Controller.OutputModeEnum outputMode;
        private BASS.StreamProc StreamProc;

        public OutputManager(BASS.StreamProc streamProc)
        {
            this.StreamProc = streamProc;
        }

        /// <summary>
        /// 出力ストリームが利用できるかどうか
        /// </summary>
        public bool Available
        {
            get
            {
                return outputChannel != null;
            }
        }

        /// <summary>
        /// 現在の出力モード
        /// </summary>
        public Controller.OutputModeEnum OutputMode
        {
            get
            {
                if (!Available) return Controller.OutputModeEnum.STOP;
                return outputMode;
            }
        }

        public int OutputFreq
        {
            get
            {
                if (!Available) return -1;
                return (int)outputChannel.GetFreq();
            }
        }

        public Controller.Resolutions OutputResolution
        {
            get
            {
                if (!Available) return Controller.Resolutions.Unknown;
                if(outputChannel is BASSWASAPIOutput){
                    switch(((BASSWASAPIOutput)outputChannel).Info.Format){
                        case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_FLOAT: return Controller.Resolutions.Float_32Bit;
                        case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_8BIT: return Controller.Resolutions.Integer_8bit;
                        case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_16BIT: return Controller.Resolutions.Integer_16bit;
                        case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_24BIT: return Controller.Resolutions.Integer_24bit;
                        case BASSWASAPIOutput.BASS_WASAPI_INFO.Formats.BASS_WASAPI_FORMAT_32BIT: return Controller.Resolutions.Integer_32bit;
                        default: return Controller.Resolutions.Unknown;
                    }
                }
                return Controller.Resolutions.Unknown;
            }
        }

        #region set/get Volume
        private Object volumeLock = new Object();
        internal float Volume
        {
            set
            {
                lock (volumeLock)
                {
                    if (Available)
                    {
                        outputChannel.SetVolume(value);
                    }
                }
            }

        }
        #endregion

        #region set/get Pause
        private bool _pause = false;
        internal bool Pause
        {
            get
            {
                return _pause;
            }
            set
            {
                if (!Available)
                {
                    _pause = false;
                    return;
                }
                _pause = value;
                if (value)
                {
                    outputChannel.Pause();
                }
                else
                {
                    outputChannel.Resume();
                }
            }
        }
        #endregion

        internal bool CanAbort
        {
            get
            {
                if (Available) return outputChannel.CanAbort();
                return false;
            }
        }

        internal bool Abort()
        {
            if (Available) return outputChannel.Abort();
            return false;
        }

        internal void Resume()
        {
            _pause = false;
            if (Available) outputChannel.Resume();
        }

        internal void Start()
        {
            _pause = false;
            if (Available) outputChannel.Start();
        }

        internal void Stop()
        {
            if (Available) outputChannel.Stop();
        }

        internal void SetVolume(float vol, uint timespan)
        {
            if (Available) outputChannel.SetVolume(vol, timespan);
        }

        internal uint GetDataFFT(float[] buffer, Wrapper.BASS.BASS.IPlayable.FFT fftopt)
        {
            if (Available) return outputChannel.GetDataFFT(buffer, fftopt);
            return 0;
        }

        /// <summary>
        /// 参考ストリームを再生するために出力の再初期化が必要かどうか
        /// </summary>
        /// <param name="reference">参考ストリーム</param>
        /// <returns>再構成が必要かどうか</returns>
        internal bool RebuildRequired(BASS.Stream reference)
        {
            if (reference == null) return true;
            var info = reference.Info;
            uint freq = info.Freq;
            uint chans = info.Chans;

            return RebuildRequired(freq, chans, (info.Flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) != 0);
        }

        private bool RebuildRequired(uint freq, uint chans, bool useFloat)
        {
            if (outputChannel == null || outputChannel.GetFreq() != freq || outputChannel.GetChans() != chans)// || (outputChannel.Info.flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) != flag)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 再生を停止し、出力デバイスを解放する
        /// </summary>
        /// <param name="waitsync">現在のバッファの内容を使い切る程度の時間待機してから停止</param>
        internal void KillOutputChannel()
        {
            var _outputChannel = outputChannel;
            lock (outputChannelLock)
            {
                if (outputChannel != null)
                {
                    outputChannel = null;
                    _outputChannel.Stop();
                    _outputChannel.Dispose();
                }
            }
            outputMode = Controller.OutputModeEnum.STOP;
        }

        /// <summary>
        /// 出力デバイスを初期化する
        /// </summary>
        /// <param name="freq">出力周波数</param>
        /// <param name="chans">出力チャンネル</param>
        /// <param name="useFloat">浮動小数点出力モード</param>
        internal void ResetOutputChannel(uint freq, uint chans, bool useFloat, uint bufferLen, string preferredDeviceName = null)
        {
            if (RebuildRequired(freq, chans, useFloat))
            {
                outputMode = Controller.OutputModeEnum.STOP;
                lock (outputChannelLock)
                {
                    if (outputChannel != null)
                    {
                        outputChannel.Dispose();
                        outputChannel = null;
                    }
                    Logger.Debug("Rebuild output");
                    if (useFloat)
                    {
                        foreach(var dlg in new OutputChannelBuilder[] { 
                                BuildWASAPIExOutput, 
                                BuildWASAPIOutput, 
                                BuildFloatingPointOutput 
                            }){
                            try{
                                outputChannel = (BASS.IPlayable)dlg.DynamicInvoke(new object[] { freq, chans, preferredDeviceName, bufferLen });
                                break;
                            }catch(Exception){
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// WASAPI排他モードで出力を初期化する
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="chans"></param>
        /// <param name="preferredDeviceName">デフォルトデバイスに優先して選択するデバイスの名前</param>
        /// <returns></returns>
        private BASS.IPlayable BuildWASAPIExOutput(uint freq, uint chans, string preferredDeviceName, uint bufferLen)
        {
            if (!BASSWASAPIOutput.IsAvailable || !AppCore.EnableWASAPIExclusive)
            {
                throw new Exception();
            }
            BASS.BASS_SetDevice(0);

            int deviceid = -1;
            var devices = BASSWASAPIOutput.GetDevices();
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                if (
                    (device.Name == preferredDeviceName) && 
                    ((device.Flag & BASSWASAPIOutput.BASS_WASAPI_DEVICEINFO.Flags.BASS_DEVICE_INPUT) == 0) && 
                    (device.Mixchans>=2))
                {
                    deviceid = i;
                    Logger.Debug("Found preferred output device:" + device.ToString());
                    break;
                }
            }
            var outputChannel = new BASSWASAPIOutput(freq, chans, StreamProc, BASSWASAPIOutput.InitFlags.Buffer | BASSWASAPIOutput.InitFlags.Exclusive, deviceid, bufferLen);
            if (outputChannel == null) throw new Exception();
            outputMode = Controller.OutputModeEnum.WASAPIEx;
            Logger.Log("Use WASAPI Exclusive Output: freq=" + outputChannel.Info.Freq + ", format=" + outputChannel.Info.Format);
            return outputChannel;
        }

        /// <summary>
        /// WASAPI共有モードで出力を初期化する
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="chans"></param>
        /// <param name="preferredDeviceName">デフォルトデバイスに優先して選択するデバイスの名前</param>
        /// <returns></returns>
        private BASS.IPlayable BuildWASAPIOutput(uint freq, uint chans, string preferredDeviceName, uint bufferLen)
        {
            if (!BASSWASAPIOutput.IsAvailable)
            {
                throw new Exception();
            }
            BASS.BASS_SetDevice(0);

            int deviceid = -1;
            var devices = BASSWASAPIOutput.GetDevices();
            for (int i = 0; i < devices.Length; i++)
            {
                var device = devices[i];
                if (
                    (device.Name == preferredDeviceName) &&
                    ((device.Flag & BASSWASAPIOutput.BASS_WASAPI_DEVICEINFO.Flags.BASS_DEVICE_INPUT) == 0) &&
                    (device.Mixchans >= 2))
                {
                    deviceid = i;
                    Logger.Debug("Found preferred output device:" + device.ToString());
                    break;
                }
            }
            var outputChannel = new BASSWASAPIOutput(freq, chans, StreamProc, BASSWASAPIOutput.InitFlags.Buffer, deviceid, bufferLen);
            if (outputChannel == null) throw new Exception();
            outputMode = Controller.OutputModeEnum.WASAPI;
            Logger.Log("Use WASAPI Exclusive Output: freq=" + outputChannel.Info.Freq + ", format=" + outputChannel.Info.Format);
            return outputChannel;
        }

        /// <summary>
        /// 浮動小数点モードで出力を初期化する
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="chans"></param>
        /// <param name="preferredDeviceName">デフォルトデバイスに優先して選択するデバイスの名前</param>
        /// <returns></returns>
        private BASS.IPlayable BuildFloatingPointOutput(uint freq, uint chans, string preferredDeviceName, uint bufferLen)
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
            var outputChannel = new BASS.UserSampleStream(freq, chans, StreamProc, (BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) | BASS.Stream.StreamFlag.BASS_STREAM_AUTOFREE);
            if (outputChannel != null) outputMode = Controller.OutputModeEnum.FloatingPoint;
            Logger.Debug("Use Float Output");
            return outputChannel;
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
    }
}
