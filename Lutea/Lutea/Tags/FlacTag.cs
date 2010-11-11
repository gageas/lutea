using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.Tags
{
    class FlacTag : Ogg
    {
        private const string FLACTAG_MARK = "fLaC";
        private const int FLAC_BLOCK_TYPE_VORBIS_COMMENT = 4;

        new public static List<KeyValuePair<string, object>> Read(System.IO.Stream stream)
        {
            stream.Seek(0, System.IO.SeekOrigin.Begin);
            byte[] flacHeaderBuffer = new byte[FLACTAG_MARK.Length];
            stream.Read(flacHeaderBuffer, 0, FLACTAG_MARK.Length);

            if (Encoding.ASCII.GetString(flacHeaderBuffer, 0, FLACTAG_MARK.Length) != FLACTAG_MARK) return null;

            stream.Seek(FLACTAG_MARK.Length, System.IO.SeekOrigin.Begin);
            bool lastMetadataFlag = false;
            while (!lastMetadataFlag)
            {
                byte[] metadataBlockHeader = new byte[4];
                stream.Read(metadataBlockHeader, 0, 4);

                lastMetadataFlag = (((metadataBlockHeader[0] & 0x80) != 0) ? true : false);
                int blockType = metadataBlockHeader[0] & 0x7f;
                int length = ((int)metadataBlockHeader[1] << 16) + ((int)metadataBlockHeader[2] << 8) + (int)metadataBlockHeader[3];
                if (blockType == FLAC_BLOCK_TYPE_VORBIS_COMMENT)
                {
                    byte[] vorbisCommentBody = new byte[length];
                    stream.Read(vorbisCommentBody, 0, length);
                    return GetVorbisCommentFromBuffer(vorbisCommentBody);
                }
                else
                {
                    stream.Seek(length, System.IO.SeekOrigin.Current);
                } 
            }
            return null;
        }
    }
}
