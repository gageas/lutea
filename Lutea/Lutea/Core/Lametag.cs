using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// LAMETagを読むクラス
    /// 最低限の内容だけ。あとで拡張するかも。
    /// ref. http://gabriel.mp3-tech.org/mp3infotag.html
    /// </summary>
    class Lametag
    {
        public class LameInfo
        {
            public bool isVBR;
            public int delay;
            public int padding;
        }
        public static LameInfo Read(Stream strm)
        {
            byte[] buffer = new byte[10];
            strm.Read(buffer, 0, 10);
            if (Encoding.ASCII.GetString(buffer, 0, 3) == "ID3")
            {
                var size = (buffer[6] << 21) + (buffer[7] << 14) + (buffer[8] << 7) + buffer[9];
                strm.Seek(size, SeekOrigin.Current);
            }
            buffer = new byte[0x180];
            strm.Read(buffer, 0, buffer.Length);
            if (buffer[0] != 0xFF) return null;
            if (buffer[1] != 0xFB) return null;

            var XingORInfo = Encoding.ASCII.GetString(buffer, 0x24, 4);
            LameInfo info = new LameInfo();
            if (XingORInfo == "Xing")
            {
                info.isVBR = true;
            }
            else if (XingORInfo == "Info")
            {
                info.isVBR = false;
            }
            else
            {
                return null;
            }
            info.delay = (buffer[0xb1] << 4) + (buffer[0xb2] >> 4);
            info.padding = ((buffer[0xb2] & 0x0f) << 8) + buffer[0xb3];
            return info;
        }
    }
}
