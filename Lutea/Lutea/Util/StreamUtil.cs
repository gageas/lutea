using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Util
{
    static class StreamUtil
    {
        /// <summary>
        /// ストリームから読み出し。要求バイト数読めなければ例外をスロー
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="count"></param>
        /// <exception cref="System.IO.IOException">IOException</exception>
        /// <exception cref="System.IO.EndOfStreamException">要求バイト数読めなかった</exception>
        /// <returns></returns>
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
