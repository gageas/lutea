using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.OutputDevice
{
    class OutputDeviceFactory
    {
        private static object outputChannelLock = new object();

        public static IOutputDevice CreateOutputDevice(OutputDevice.StreamProc streamProc, uint freq, uint chans, bool useFloat, int bufferLen, string preferredDeviceName = null)
        {
            lock (outputChannelLock)
            {
                Logger.Debug("Rebuild output");
                if (Lutea.Core.AppCore.EnableWASAPIExclusive)
                {
                    try
                    {
                        var ret = new BASSWASAPIOutputChannel(true, freq, chans, preferredDeviceName, (uint)bufferLen);
                        ret.SetStreamProc(streamProc);
                        BASSWASAPIOutput.SetPriority(System.Diagnostics.ThreadPriorityLevel.TimeCritical);
                        return ret;
                    }
                    catch (BASSWASAPIOutput.BASSWASAPIException) { }
                }

                try
                {
                    var ret = new BASSWASAPIOutputChannel(false, freq, chans, preferredDeviceName, (uint)bufferLen);
                    ret.SetStreamProc(streamProc);
                    BASSWASAPIOutput.SetPriority(System.Diagnostics.ThreadPriorityLevel.TimeCritical);
                    return ret;
                }
                catch (BASSWASAPIOutput.BASSWASAPIException) { }

                try
                {
                    var ret = new BASSOutput(freq, chans, preferredDeviceName, bufferLen);
                    ret.SetStreamProc(streamProc);
                    BASSWASAPIOutput.SetPriority(System.Diagnostics.ThreadPriorityLevel.TimeCritical);
                    return ret;
                }
                catch (BASS.BASSException) { }

                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// 参考ストリームを再生するために出力の再初期化が必要かどうか
        /// </summary>
        /// <param name="outputChannel">出力チャンネル</param>
        /// <param name="freq">サンプリング周波数</param>
        /// <param name="chans">チャンネル数</param>
        /// <param name="useFloat">データ型が浮動小数点(float)かどうか</param>
        /// <returns>再構成が必要かどうか</returns>
        public static bool RebuildRequired(IOutputDevice outputChannel, uint freq, uint chans, bool useFloat)
        {
            if (outputChannel == null || outputChannel.Freq != freq || outputChannel.Chans != chans)
            {
                return true;
            }
            return false;
        }
    }
}
