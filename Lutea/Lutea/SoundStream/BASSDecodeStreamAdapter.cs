using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.SoundStream
{
    /// <summary>
    /// BASSAudioのデコーダーの出力をPullSoundStreamのIFに合わせるアダプタ
    /// </summary>
    class BASSDecodeStreamAdapter : PullSoundStreamBase
    {
        /// <summary>
        /// GetData関数の再試行回数の上限
        /// </summary>
        private const int GET_DATA_RETRY_MAX_DEFAULT = 5;

        private BASS.Stream Stream;

        [ThreadStatic]
        private static bool Initialized;

        /// <summary>
        /// BASSのStreamFlagを生成する
        /// </summary>
        /// <param name="floatingPoint">float出力かどうか</param>
        /// <param name="preScan">preScanをするかどうか</param>
        /// <returns>BASS.Stream.StreamFlag</returns>
        private static BASS.Stream.StreamFlag MakeFlag(bool floatingPoint, bool preScan)
        {
            BASS.Stream.StreamFlag flag = BASS.Stream.StreamFlag.BASS_STREAM_DECODE | BASS.Stream.StreamFlag.BASS_STREAM_ASYNCFILE;
            if (floatingPoint) flag |= BASS.Stream.StreamFlag.BASS_STREAM_FLOAT;
            if (preScan) flag |= BASS.Stream.StreamFlag.BASS_STREAM_PRESCAN;
            return flag;
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <exception cref="System.ArgumentException"></exception>
        /// <param name="filename"></param>
        /// <param name="isFloat"></param>
        /// <param name="preScan"></param>
        public BASSDecodeStreamAdapter(string filename, bool isFloat = true, bool preScan = false)
        {
            if (!Initialized)
            {
                BASS.BASS_SetDevice(0);
                Initialized = true;
            }
            try
            {
                this.Stream = new BASS.FileStream(filename, MakeFlag(isFloat, preScan), getData_retryMax: GET_DATA_RETRY_MAX_DEFAULT);
            }
            catch (BASS.BASSException ex)
            {
                throw new ArgumentException("Cannot create stream", ex);
            }
        }

        public override void Dispose()
        {
            if (this.Stream != null)
            {
                this.Stream.Dispose();
            }
            this.Stream = null;
            GC.SuppressFinalize(this);
        }

        ~BASSDecodeStreamAdapter()
        {
            Dispose();
        }

        public override uint Chans
        {
            get { return Stream.GetChans(); }
        }

        public override uint Freq
        {
            get { return Stream.GetFreq(); }
        }

        public override ulong LengthSample
        {
            get { return Stream.filesize / SampleBytes; }
        }

        public override string Location
        {
            get { return Stream.Info.Filename; }
        }

        public override ulong PositionSample
        {
            get
            {
                return Stream.position / SampleBytes;
            }
            set
            {
                var RangeOffset = PositionSample - value;
                if (RangeOffset == 0) return;
                if ((RangeOffset > 0) && (RangeOffset < 4000))
                {
                    ulong left = RangeOffset;
                    if (left > 0)
                    {
                        var mem = Marshal.AllocHGlobal(128 * (int)SampleBytes);
                        while (left > 0)
                        {
                            int toread = (int)Math.Min(128, left);
                            Stream.GetData(mem, (uint)toread * SampleBytes);
                            left -= (uint)toread;
                        }
                        Marshal.FreeHGlobal(mem);
                    }
                }
                else
                {
                    Stream.position = value * SampleBytes;
                }
            }
        }

        public override uint GetData(IntPtr buffer, uint length)
        {
            var ret = Stream.GetData(buffer, length * SampleBytes);
            if (ret == uint.MaxValue) return 0;
            return ret / SampleBytes;
        }

        public override double? ReplayGain
        {
            get { return null; }
        }

    }
}
