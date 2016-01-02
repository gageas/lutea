using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.SoundStream
{
    /// <summary>
    /// ストリームのOffsetからActualLengthの部分を切り出すフィルタ
    /// </summary>
    class RangeFilter : AbstractFilter
    {
        private ulong Offset;
        private ulong ActualLength;

        public RangeFilter(PullSoundStreamBase input, ulong offset, ulong length)
            :base(input)
        {
            if (offset + length > Input.LengthSample)
            {
                throw new ArgumentOutOfRangeException("offset / length out of range");
            }
            Offset = offset;
            ActualLength = length;
            PositionSample = 0; // 先頭にシーク
        }

        public override ulong LengthSample
        {
            get
            {
                return ActualLength;
            }
        }

        public override ulong PositionSample
        {
            get
            {
                var pos = Input.PositionSample;
                if (Offset > pos) return 0;
                if ((pos - Offset) > Input.LengthSample) return LengthSample;
                return pos - Offset;
            }
            set
            {
                if (value + Offset > Input.LengthSample) return; // ignore
                Input.PositionSample = value + Offset;
            }
        }

        public override uint GetData(IntPtr buffer, uint length)
        {
            var left = (Offset + ActualLength) - Input.PositionSample;
            return base.GetData(buffer, (uint)Math.Min(left, length));
        }
    }
}
