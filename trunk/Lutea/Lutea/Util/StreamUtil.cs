using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Util
{
    static class StreamUtil
    {
        public static byte[] ReadBytes(this Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            var read = stream.Read(buffer, 0, count);
            if (read != count)
            {
                throw new System.IO.EndOfStreamException();
            }
            return buffer;
        }
    }
}
