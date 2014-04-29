using System.Linq;
using System.IO;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.Tags
{
    /// <summary>
    /// LAMETagを読むクラス
    /// 最低限の内容だけ。あとで拡張するかも。
    /// ref. http://gabriel.mp3-tech.org/mp3infotag.html
    /// </summary>
    class Lametag
    {
        public bool isVBR;
        public int delay;
        public int padding;

        public static Lametag Read(string filename)
        {
            try
            {
                using (var fs = File.Open(filename.Trim(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return Read(fs);
                }
            }
            catch (IOException ex)
            {
                Logger.Log(ex);
                return null;
            }
        }
        public static Lametag Read(Stream strm)
        {
            byte[] buffer;
            try
            {
                buffer = strm.ReadBytes(10);
            }
            catch (IOException ex)
            {
                Logger.Error(ex);
                return null;
            }
            if (buffer[0] == 'I' && buffer[1] == 'D' && buffer[2] == '3')
            {
                var size = (buffer[6] << 21) + (buffer[7] << 14) + (buffer[8] << 7) + buffer[9];
                try
                {
                    strm.Seek(size, SeekOrigin.Current);
                }
                catch (IOException ex)
                {
                    Logger.Error(ex);
                    return null;
                }
            }
            try
            {
                buffer = strm.ReadBytes(0x180);
            }
            catch (IOException ex)
            {
                Logger.Error(ex);
                return null;
            }

            if (buffer[0] != 0xFF) return null;
            if (buffer[1] != 0xFB) return null;

            var XingORInfo = buffer.Skip(0x24).Take(4).ToArray();
            Lametag info = new Lametag();
            if (XingORInfo[0] == 'X' && XingORInfo[1] == 'i' && XingORInfo[2] == 'n' && XingORInfo[3] == 'g')
            {
                info.isVBR = true;
            }
            else if (XingORInfo[0] == 'I' && XingORInfo[1] == 'n' && XingORInfo[2] == 'f' && XingORInfo[3] == 'o')
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
