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
        private const int BASS_BUFFFER_LEN = 1500;
        private const uint OutputFreq = 44100;

        private delegate BASS.IPlayable OutputChannelBuilder(uint freq, uint chans);

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

        internal void Resume()
        {
            _pause = false;
            if (Available) outputChannel.Resume();
        }

        internal void Start()
        {
            if (Available) outputChannel.Start();
        }

        internal void Stop()
        {
            if (Available) outputChannel.Stop();
        }

        internal void SetVolume(float vol, uint timespan)
        {
            outputChannel.SetVolume(vol, timespan);
        }

        internal uint GetDataFFT(float[] buffer, Wrapper.BASS.BASS.IPlayable.FFT fftopt)
        {
            return outputChannel.GetDataFFT(buffer, fftopt);
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
            uint freq = info.freq;
            uint chans = info.chans;

            return RebuildRequired(freq, chans, (info.flags & BASS.Stream.StreamFlag.BASS_STREAM_FLOAT) != 0);
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
        internal void KillOutputChannel(bool waitsync = false)
        {
            var _outputChannel = outputChannel;
            lock (outputChannelLock)
            {
                if (outputChannel != null)
                {
                    outputChannel = null;
                    if (waitsync)
                    {
                        Thread.Sleep(BASS_BUFFFER_LEN);
                    }
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
        internal void ResetOutputChannel(uint freq, uint chans, bool useFloat)
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
                    Logger.Log("Rebuild output");
                    if (useFloat)
                    {
                        outputChannel = Util.Util.TryThese<BASS.IPlayable>(
                            new OutputChannelBuilder[] { 
                                BuildWASAPIExOutput, 
                                BuildWASAPIOutput, 
                                BuildFloatingPointOutput 
                            }, new object[] { freq, chans });
                    }
                }
            }
        }

        /// <summary>
        /// WASAPI排他モードで出力を初期化する
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="chans"></param>
        /// <returns></returns>
        private BASS.IPlayable BuildWASAPIExOutput(uint freq, uint chans)
        {
            if (!BASSWASAPIOutput.IsAvailable || !AppCore.enableWASAPIExclusive)
            {
                throw new Exception();
            }
            BASS.BASS_SetDevice(0);
            var outputChannel = new BASSWASAPIOutput(freq, chans, StreamProc, BASSWASAPIOutput.Flags.Buffer | BASSWASAPIOutput.Flags.Exclusive, AppCore.enableWASAPIVolume);
            if (outputChannel == null) throw new Exception();
            outputMode = Controller.OutputModeEnum.WASAPIEx;
            Logger.Debug("Use WASAPI Exclusive Output");
            return outputChannel;
        }

        /// <summary>
        /// WASAPI共有モードで出力を初期化する
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="chans"></param>
        /// <returns></returns>
        private BASS.IPlayable BuildWASAPIOutput(uint freq, uint chans)
        {
            if (!BASSWASAPIOutput.IsAvailable)
            {
                throw new Exception();
            }
            BASS.BASS_SetDevice(0);
            var outputChannel = new BASSWASAPIOutput(freq, chans, StreamProc, BASSWASAPIOutput.Flags.Buffer, AppCore.enableWASAPIVolume);
            if (outputChannel == null) throw new Exception();
            outputMode = Controller.OutputModeEnum.WASAPI;
            Logger.Debug("Use WASAPI Shared Output");
            return outputChannel;
        }

        /// <summary>
        /// 浮動小数点モードで出力を初期化する
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="chans"></param>
        /// <returns></returns>
        private BASS.IPlayable BuildFloatingPointOutput(uint freq, uint chans)
        {
            var outdev = GetInitializedBassRealOutputDevice();
            if (outdev == 0)
            {
                BASS.BASS_Init(-1, OutputFreq, BASS_BUFFFER_LEN);
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
                if (_info.isInit) return a;
            }
            return 0;
        }
    }
}
