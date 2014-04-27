using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.SoundStream
{
    /// <summary>
    /// PullSoundStreamに対するフィルタの基底クラス。一次側の出力をそのまま二次側に出力する。
    /// </summary>
    abstract class AbstractFilter : PullSoundStreamBase
    {
        protected PullSoundStreamBase Input;

        public AbstractFilter(PullSoundStreamBase input)
        {
            this.Input = input;
        }

        public override void Dispose()
        {
            if (Input != null)
            {
                Input.Dispose();
            }
            Input = null;
            GC.SuppressFinalize(this);
        }

        ~AbstractFilter()
        {
            Dispose();
        }

        public override string Location
        {
            get { return Input.Location; }
        }

        public override double? ReplayGain
        {
            get { return Input.ReplayGain; }
        }

        public override uint Chans
        {
            get { return Input.Chans; }
        }

        public override uint Freq
        {
            get { return Input.Freq; }
        }

        public override ulong LengthSample
        {
            get { return Input.LengthSample; }
        }

        public override ulong PositionSample
        {
            get { return Input.PositionSample; }
            set { Input.PositionSample = value; }
        }

        public override uint GetData(IntPtr buffer, uint length)
        {
            return Input.GetData(buffer, length);
        }
    }
}
