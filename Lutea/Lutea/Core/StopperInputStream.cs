using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.Core
{
    class StopperInputStream : InputStream
    {
        public StopperInputStream()
        {
        }

        public override ulong LengthSample
        {
            get { return 0; }
        }

        public override ulong PositionSample
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public override uint GetData(IntPtr buffer, uint length)
        {
            return 0;
        }

        public override uint Chans
        {
            get { return 1; }
        }

        public override uint Freq
        {
            get { return 1; }
        }
    }
}
