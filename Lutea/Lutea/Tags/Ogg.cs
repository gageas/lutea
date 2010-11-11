using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.Tags
{
    class Ogg
    {
        private const string OGG_MARK = "OggS";
        private const int OGG_HEADER_SIZE = 27;

        public static List<KeyValuePair<string, object>> Read(System.IO.Stream stream)
        {
            byte[] oggHeaderBuffer = new byte[OGG_HEADER_SIZE];
            int pageSegmentCnt;
            byte[] pageSegmentTable;

            // 最初のpageを読み飛ばす
            stream.Seek(0, System.IO.SeekOrigin.Begin);
            stream.Read(oggHeaderBuffer, 0, OGG_HEADER_SIZE);
            if (Encoding.ASCII.GetString(oggHeaderBuffer, 0, OGG_MARK.Length) != OGG_MARK) return null;
            pageSegmentCnt = oggHeaderBuffer[26];
            pageSegmentTable = new byte[pageSegmentCnt];
            stream.Read(pageSegmentTable, 0, pageSegmentCnt);
            stream.Seek(pageSegmentTable.Sum((b) => b), System.IO.SeekOrigin.Current);

            // 2ページ目（タグが入っているページ）を読む
            stream.Read(oggHeaderBuffer, 0, OGG_HEADER_SIZE);
            if (Encoding.ASCII.GetString(oggHeaderBuffer, 0, OGG_MARK.Length) != OGG_MARK) return null;
            pageSegmentCnt = oggHeaderBuffer[26];
            pageSegmentTable = new byte[pageSegmentCnt];
            stream.Read(pageSegmentTable, 0, pageSegmentCnt);

            int sum = 0;
            for (int i = 0; i < pageSegmentTable.Length; i++)
            {
                sum += pageSegmentTable[i];
                if (pageSegmentTable[i] != 0xff) break;
            }

            byte[] vorbisCommentTable = new byte[sum];
            stream.Read(vorbisCommentTable, 0, sum);

            return GetVorbisCommentFromBuffer(vorbisCommentTable, 7);
        }

        protected static List<KeyValuePair<string, object>> GetVorbisCommentFromBuffer(byte[] buffer, int ofset = 0)
        {
            List<KeyValuePair<string, object>> data = new List<KeyValuePair<string, object>>();

            int p = ofset;
            int venderLength = BitConverter.ToInt32(buffer, p);
            p += venderLength + 4;
            int fieldCount = BitConverter.ToInt32(buffer, p);
            p += 4;
            for (int i = 0; i < fieldCount; i++)
            {
                int size = BitConverter.ToInt32(buffer, p);
                if (size == 0) break;
                p += 4;
                string[] field = Encoding.UTF8.GetString(buffer, p, size).Split(new char[] { '=' }, 2);
                data.Add(new KeyValuePair<string, object>(field[0].ToUpper(), field[1]));
                p += size;
            }
            return data;
        }
    }
}
