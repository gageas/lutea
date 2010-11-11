using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.Tags
{
    class ApeTag
    {
        private const int APETAG_SIZE = 32;
        private const string APETAG_MARK = "APETAGEX";
        private const int APETAGV2_VERSION = 2000;
        private const string TAG_KEY_COVER_ART = "COVER ART";

        public static List<KeyValuePair<string, object>> Read(Stream stream, bool createImageObject)
        {
            // ファイル末尾から0オフセットで調べる
            var tag = ReadTag(stream,createImageObject,0);
            if(tag != null)return tag;

            // ID3V1が付いている時のために、128バイトオフセットで調べる
            tag = ReadTag(stream,createImageObject,128);
            return tag;
        }

        private static List<KeyValuePair<string,object>> ReadTag(Stream stream,bool createImageObject,int offset)
        {
            byte[] buffer = new byte[APETAG_SIZE];
            stream.Seek(-APETAG_SIZE - offset, System.IO.SeekOrigin.End);
            stream.Read(buffer, 0, APETAG_SIZE);

            if (Encoding.ASCII.GetString(buffer, 0, APETAG_MARK.Length) != APETAG_MARK) return null;
            Int32 version = BitConverter.ToInt32(buffer, 8);
            Int32 tagSize = BitConverter.ToInt32(buffer, 12);
            Int32 tagCount = BitConverter.ToInt32(buffer, 16);

            stream.Seek(-tagSize-offset, System.IO.SeekOrigin.End);
            buffer = new byte[tagSize];
            stream.Read(buffer, 0, tagSize);
            List<KeyValuePair<string, object>> data = new List<KeyValuePair<string, object>>();
            int p = 0;
            Encoding enc = (version < APETAGV2_VERSION) ? Encoding.Default : Encoding.UTF8;
            for (int i=0;i<tagCount;i++)
            {
                int frameSize = BitConverter.ToInt32(buffer, p);
                uint flag = BitConverter.ToUInt32(buffer, p + 4);
                p += 8;
                string key = Encoding.ASCII.GetString(buffer, p,  buffer.IndexOf(p, 0) - p).ToUpper();
                p += key.Length + 1;
                if (key.IndexOf(TAG_KEY_COVER_ART) == 0)
                {
                    if (createImageObject)
                    {
                        int imageBodyOfset = buffer.IndexOf(p, 0) + 1;
                        var memoryStream = new MemoryStream(buffer, imageBodyOfset, buffer.Length - imageBodyOfset);
                        try
                        {
                            var img = Image.FromStream(memoryStream);
                            data.Add(new KeyValuePair<string, object>(key, img));
                        }
                        catch { }
                    }
                }
                else
                {
                    data.Add(new KeyValuePair<string, object>(key, enc.GetString(buffer, p, frameSize)));
                }
                p += frameSize;
            }
            return data;
        }
    }
}
