using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Gageas.Lutea.SoundStream
{
    class FreqConvertFilter : AbstractFilter
    {
        private IntPtr tmpBuffer = IntPtr.Zero;
        private uint tmpBufferSize = 0;

        public FreqConvertFilter(PullSoundStreamBase input)
            : base(input)
        {

        }

        public override uint Freq
        {
            get
            {
                return Input.Freq / 2;
            }
        }

        public override ulong LengthSample
        {
            get
            {
                return Input.LengthSample / 2;
            }
        }

        public override ulong PositionSample
        {
            get
            {
                return Input.PositionSample / 2;
            }
            set
            {
                Input.PositionSample = value * 2;
            }
        }

        public unsafe override uint GetData(IntPtr buffer, uint length)
        {
            var inlength = length * 2;
            if (tmpBufferSize < inlength)
            {
                if (tmpBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(tmpBuffer);
                }
                tmpBuffer = Marshal.AllocHGlobal((int)inlength);
            }
            var read = Input.GetData(tmpBuffer, inlength);
            var num = read / 2;
            float* src = (float*)tmpBuffer;
            float* dest = (float*)buffer;
            for (var i = 0; i < num; i++)
            {
                *dest++ = *src++;
                *dest++ = *src++;
                src += 2;
            }
            return read / 2;
        }

        public override void Dispose()
        {
            if (tmpBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(tmpBuffer);
            }
            tmpBuffer = IntPtr.Zero;
            base.Dispose();
        }
    }
}
