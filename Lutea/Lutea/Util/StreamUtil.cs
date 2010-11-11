using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Util
{
    static class StreamUtil
    {
        public static int ReadOrThrow(this Stream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            read = stream.Read(buffer, offset, count);
            if (read == 0) throw new System.IO.EndOfStreamException();
            return read;
        }
    }
}
