using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.SoundStream
{
    /// <summary>
    /// ReplayGainの値をオーバーライドするフィルタ
    /// </summary>
    class ReplayGainOverrideFilter : AbstractFilter
    {
        private double OverrideGain;

        public ReplayGainOverrideFilter(PullSoundStreamBase input, double gain)
            : base(input)
        {
            this.OverrideGain = gain;
        }

        public override double? ReplayGain
        {
            get { return OverrideGain; }
        }
    }

}
