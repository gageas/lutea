using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.SoundStream
{
    /// <summary>
    /// 1chの入力を2chの出力に変換するフィルタ
    /// </summary>
    class Mono2StereoFilter : AbstractFilter
    {
        public Mono2StereoFilter(PullSoundStreamBase input)
            : base(input)
        {
            if (input.Chans != 1) throw new ArgumentException("Input stream's channel num MUST be 1");
        }
    
        public override uint Chans
        {
            get
            {
                return 2;
            }
        }

        public unsafe override uint GetData(IntPtr buffer, uint length)
        {
            var read = Input.GetData(buffer, length);

            var src = ((float*)buffer.ToPointer()) + read - 1;
            var dest = ((float*)buffer.ToPointer()) + read * 2 - 1;
            for (var i = 0; i < read; i++)
            {
                *dest-- = *src;
                *dest-- = *src--;
            }
            return read;
        }
    }
}
