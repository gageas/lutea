using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Tags
{
    class FlacTag : Ogg
    {
        private const string FLACTAG_MARK = "fLaC";
        private const int FLAC_BLOCK_TYPE_METADATA_BLOCK_STREAMINFO = 0;
        private const int FLAC_BLOCK_TYPE_VORBIS_COMMENT = 4;
        private const int FLAC_BLOCK_TYPE_METADATA_BLOCK_PICTURE = 6;

        public static List<KeyValuePair<string, object>> Read(System.IO.Stream stream, bool createImageObject)
        {
            stream.Seek(0, System.IO.SeekOrigin.Begin);
            byte[] flacHeaderBuffer = new byte[FLACTAG_MARK.Length];
            stream.Read(flacHeaderBuffer, 0, FLACTAG_MARK.Length);

            if (Encoding.ASCII.GetString(flacHeaderBuffer, 0, FLACTAG_MARK.Length) != FLACTAG_MARK) return null;

            stream.Seek(FLACTAG_MARK.Length, System.IO.SeekOrigin.Begin);
            bool lastMetadataFlag = false;
            List<KeyValuePair<string, object>> tags = new List<KeyValuePair<string, object>>();
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
                    var vorbisComment = GetVorbisCommentFromBuffer(vorbisCommentBody);
                    tags.AddRange(vorbisComment);
                }
                else if (blockType == FLAC_BLOCK_TYPE_METADATA_BLOCK_STREAMINFO)
                {
                    if (length != 34) continue;
                    byte[] metadataBlockBody = new byte[length];
                    stream.Read(metadataBlockBody, 0, length);
                    int Freq = (metadataBlockBody[10] << 12) + (metadataBlockBody[11] << 4) + (metadataBlockBody[12] >> 4);
                    int Chans = ((metadataBlockBody[12] >> 1) & 7) + 1;
                    int Bits = (((metadataBlockBody[12] & 1 << 4)) + (metadataBlockBody[13] >> 4)) + 1;
                    int Samples = ((metadataBlockBody[13] & 0x0F) << 32) + (metadataBlockBody[14] << 24) + (metadataBlockBody[15] << 16) + (metadataBlockBody[15] << 8) + (metadataBlockBody[16]);
                    tags.Add(new KeyValuePair<string, object>("__X-LUTEA-CHANS__", Chans.ToString()));
                    tags.Add(new KeyValuePair<string, object>("__X-LUTEA-BITS__", Bits.ToString()));
                    tags.Add(new KeyValuePair<string, object>("__X-LUTEA-FREQ__", Freq.ToString()));
                    tags.Add(new KeyValuePair<string, object>("__X-LUTEA-DURATION__", ((int)(Samples / Freq)).ToString()));
                    Logger.Log(Freq + Chans + Bits + Samples);
                }
                else if (createImageObject && blockType == FLAC_BLOCK_TYPE_METADATA_BLOCK_PICTURE)
                {
                    byte[] pictureBody = new byte[length];
                    stream.Read(pictureBody, 0, length);
                    UInt32 mimeLen = (UInt32)((pictureBody[4] << 24) + (pictureBody[5] << 16) + (pictureBody[6] << 8) + (pictureBody[7]));
                    UInt32 descLen = (UInt32)((pictureBody[8 + mimeLen] << 24) + (pictureBody[9 + mimeLen] << 16) + (pictureBody[10 + mimeLen] << 8) + (pictureBody[11 + mimeLen]));
                    try
                    {
                        var pic = System.Drawing.Image.FromStream(new MemoryStream(pictureBody, (int)(32 + mimeLen + descLen), (int)(pictureBody.Length - (32 + mimeLen + descLen))));
                        if (pic != null)
                        {
                            tags.Add(new KeyValuePair<string, object>("COVER ART", pic));
                        }
                    }catch{}
                }
                else
                {
                    stream.Seek(length, System.IO.SeekOrigin.Current);
                }
            }
            return tags.Count == 0 ? null : tags;
        }
    }
}
