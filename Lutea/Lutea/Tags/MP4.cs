using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace Gageas.Lutea.Tags
{
    class MP4
    {
        private const int NODE_LENGTH_FIELD_SIZE = 4;
        private const int NODE_NAME_FIELD_SIZE = 4;
        private const int NODE_LIST_HEADER_SIZE = NODE_LENGTH_FIELD_SIZE + NODE_NAME_FIELD_SIZE;

        private static readonly Dictionary<UInt32, String> KnownItunesTagAtoms = new Dictionary<UInt32, String>(){
            {0x74726B6E, "TRACK"},         // trkn
            {0x61415254, "ALBUM ARTIST"},  // aART
            {0x636F7672, "COVER ART"},     // covr
            {0xA9415254, "ARTIST"},        // .ART
            {0xA96E616D, "TITLE"},         // .nam
            {0xA9616C62, "ALBUM"},         // .alb
            {0xA967656E, "GENRE"},         // .gen
            {0xA9646179, "DATE"},          // .dat
            {0xA9636D74, "COMMENT"},       // .cmt
            {0xA9777274, "COMPOSER"},      // .wrt
            {0xA96C7972, "LYRICS"},        // .lyr
            {0x70757264, "PURCHASE DATE"}, // purd
        };

        private static readonly Dictionary<UInt32, int> KnownListAtoms = new Dictionary<uint, int>()
        {
            // name,internal nodes offset
            {0x6D6F6F76, 0}, // moov
            {0x75647461, 0}, // udta
            {0x696C7374, 0}, // ilst
            {0x6469736B, 0}, // disk
            {0x7472616B, 0}, // trak
            {0x6D646961, 0}, // mdia
            {0x6D696E66, 0}, // minf
            {0x7374626C, 0}, // stbl
            {0x6D657461, 4}, // meta
            {0x73747364, 8}, // stsd
        };

        private Stream strm;
        private List<KeyValuePair<string, object>> tags;
        private bool createImageObject = true;
        private string tagKey;
        private object tagValue;
        private List<System.Drawing.Image> tagValuePicture = new List<System.Drawing.Image>();

        /// <summary>
        /// Read MP4 audio meta data.
        /// </summary>
        /// <param name="strm">MP4 input data stream</param>
        /// <param name="createImageObject">Read embedded Cover Art(=true) or not(=false)</param>
        /// <returns>List of meta data or null</returns>
        public static List<KeyValuePair<string, object>> Read(Stream strm, bool createImageObject = true)
        {
            var parser = new MP4(strm, createImageObject);

            try
            {
                parser.ReadRecurse(strm.Length);
            }
            catch (Exception) { }

            return parser.tags;
        }

        private MP4(Stream strm, bool createImageObject)
        {
            this.strm = strm;
            this.createImageObject = createImageObject;
            this.tags = new List<KeyValuePair<string, object>>();
        }

        /// <summary>
        /// Read MP4 structure recursively and make a list of meta data.
        /// </summary>
        private void ReadRecurse(long length)
        {
            long p = 0;
            while (true)
            {
                // read ATOM header
                byte[] buffer = new byte[NODE_LIST_HEADER_SIZE];
                strm.Read(buffer, 0, NODE_LIST_HEADER_SIZE);
                long atom_size = BEUInt32(buffer, 0);
                UInt32 atom_name = BEUInt32(buffer, 4);

                if (atom_size == 1)
                {
                    byte[] large_size_buf = new byte[sizeof(UInt64)];
                    strm.Read(large_size_buf, 0, sizeof(UInt64));
                    var large_size = BEUInt64(large_size_buf, 0);
                    atom_size = (long)large_size;
                    atom_size -= (NODE_LIST_HEADER_SIZE + sizeof(UInt64));
                }
                else if (atom_size == 0)
                {
                    throw new System.IO.FileFormatException("atom_size is zero");
                }
                else
                {
                    atom_size -= NODE_LIST_HEADER_SIZE;
                }

                if ((atom_size > (length - p)) || (atom_size < 0)) return;

                long initial_pos = strm.Position;

                switch (atom_name)
                {
                    case 0x64617461: // data
                        if (tagValue == null)
                        {
                            ReadData((int)atom_size);
                        }
                        break;
                    case 0x6E616D65: // name
                        tagKey = ReadName((int)atom_size).ToUpper();
                        break;
                    case 0x676E7265: // gnre
                        tagValue = null;
                        try
                        {
                            ReadRecurse(atom_size);
                            if (tagValue == null) break;
                            if (!(tagValue is int)) break;
                            var genreStr = ID3.GetGenreString((int)tagValue - 1);
                            if (genreStr == null) break;
                            tags.Add(new KeyValuePair<string, object>("GENRE", genreStr));
                        }
                        catch { }
                        break;
                    case 0x6D703461: // mp4a
                        ReadMp4a((int)atom_size);
                        break;
                    case 0x6D766864: // mvhd
                        ReadMvhd((int)atom_size);
                        break;
                    case 0x2D2D2D2D: // ----
                        Read____((int)atom_size);
                        break;
                    case 0x70696E66: // pinf
                        tags.Add(new KeyValuePair<string, object>("PURCHASED", "true"));
                        break;
                    default:
                        if (KnownListAtoms.ContainsKey(atom_name))
                        {
                            var offset = KnownListAtoms[atom_name];
                            if (offset != 0)
                            {
                                strm.Seek(offset, SeekOrigin.Current);
                            }
                            ReadRecurse(atom_size - offset);
                        }
                        else if (KnownItunesTagAtoms.ContainsKey(atom_name))
                        {
                            tagValue = null;
                            ReadRecurse(atom_size);
                            if ((tagValue != null) || (tagValuePicture != null)) AddToTag(KnownItunesTagAtoms[atom_name]);
                        }
                        else
                        {
                            strm.Seek(atom_size, SeekOrigin.Current);
                        }
                        break;
                }

                p += atom_size;
                initial_pos += atom_size;
                if (p >= length)
                {
                    return;
                }
                if (strm.Position != initial_pos)
                {
                    strm.Seek(initial_pos, SeekOrigin.Begin);
                }
            }
        }

        private void AddToTag(string tagKey)
        {
            if (tagKey == "COVER ART")
            {
                tags.AddRange(tagValuePicture.Select(_ => new KeyValuePair<string, object>("COVER ART", _)));
                tagValuePicture.Clear();
            }
            else
            {
                var alreadyExisting = tags.Find(_ => _.Key == tagKey && _.Value is string);
                /* 同じフィールド名のstringのフィールドが存在するとき，\0をセパレータとして連結する */
                if ((tagValue is string) && (alreadyExisting.Key != null))
                {
                    tags.Remove(alreadyExisting);
                    tags.Add(new KeyValuePair<string, object>(tagKey, ((string)(alreadyExisting.Value)).Split('\0').Concat(new string[] { tagValue.ToString() }).Distinct().Aggregate((a, b) => a + '\0' + b)));
                }
                else
                {
                    tags.Add(new KeyValuePair<string, object>(tagKey, tagValue));
                }
                tagValue = null;
            }
        }

        private void ReadMvhd(int length)
        {
            if (length < 4) return;
            byte[] buf = new byte[4];
            strm.Read(buf, 0, 4);
            UInt32 version = BEUInt32(buf, 0);
            if (version == 0)
            {
                byte[] buf2 = new byte[4 * 4];
                if (length < 4 + buf2.Length) return;
                strm.Read(buf2, 0, buf2.Length);
                UInt32 creation_time = BEUInt32(buf2, 0);
                UInt32 modification_time = BEUInt32(buf2, 0);
                UInt32 timescale = BEUInt32(buf2, 8);
                UInt32 duration = BEUInt32(buf2, 12);
                tags.Add(new KeyValuePair<string, object>("__X-LUTEA-DURATION__", ((int)(duration / timescale)).ToString()));
            }
            else if (version == 1)
            {
                byte[] buf2 = new byte[8 + 8 + 4 + 8];
                if (length < 4 + buf2.Length) return;
                strm.Read(buf2, 0, buf2.Length);
                UInt64 creation_time = BEUInt64(buf2, 0);
                UInt64 modification_time = BEUInt64(buf2, 8);
                UInt32 timescale = BEUInt32(buf2, 16);
                UInt64 duration = BEUInt64(buf2, 20);
                tags.Add(new KeyValuePair<string, object>("__X-LUTEA-DURATION__", ((int)(duration / timescale)).ToString()));
            }
        }

        private void ReadMp4a(int length)
        {
            int size = 8 + 8 + 2 + 2 + 2 + 2 + 4;
            if (length < size) return;
            byte[] buf_audio_sample_entry = new byte[size];
            strm.Read(buf_audio_sample_entry, 0, size);
            UInt16 channelcount = BEUInt16(buf_audio_sample_entry, 8 + 8);
            UInt16 samplesize = BEUInt16(buf_audio_sample_entry, 8 + 8 + 2);
            UInt32 samplerate = BEUInt32(buf_audio_sample_entry, 8 + 8 + 2 + 2 + 2 + 2);
            tags.Add(new KeyValuePair<string, object>("__X-LUTEA-CHANS__", channelcount.ToString()));
            tags.Add(new KeyValuePair<string, object>("__X-LUTEA-BITS__", samplesize.ToString()));
            tags.Add(new KeyValuePair<string, object>("__X-LUTEA-FREQ__", (samplerate >> 16).ToString()));
            if (length - size > 0)
            {
                ReadRecurse(length - size);
            }
        }

        private void Read____(int length)
        {
            tagKey = null;
            tagValue = null;
            ReadRecurse(length);
            if ((tagKey != null) && (tagValue != null))
            {
                var alreadyExisting = tags.Find(_ => _.Key == tagKey && _.Value is string);
                /* 同じフィールド名のstringのフィールドが存在するとき，\0をセパレータとして連結する */
                if ((tagValue is string) && (alreadyExisting.Key != null))
                {
                    tags.Remove(alreadyExisting);
                    tags.Add(new KeyValuePair<string, object>(tagKey, ((string)(alreadyExisting.Value)).Split('\0').Concat(new string[] { tagValue.ToString() }).Distinct().Aggregate((a, b) => a + '\0' + b)));
                }
                else
                {
                    tags.Add(new KeyValuePair<string, object>(tagKey, tagValue));
                }
            }
            tagKey = null;
            tagValue = null;
        }

        /// <summary>
        /// Read "name" node.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private string ReadName(int length)
        {
            var buf = new byte[length];
            strm.Read(buf, 0, length);
            return Encoding.ASCII.GetString(buf, 4, (int)length - 4);
        }

        /// <summary>
        /// Read "data" node.
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private void ReadData(int length)
        {
            if (length <= 8)
            {
                return;
            }
            byte[] buf;
            var buf_type = new byte[8];
            strm.Read(buf_type, 0, (int)8);
            switch (BitConverter.ToUInt64(buf_type, 0))
            {
                case 0: // trkn or disk. "nn/mm" style.
                    buf = new byte[length - 8];
                    strm.Read(buf, 0, buf.Length);
                    if (length - 8 == 2)
                    {
                        tagValue = (buf[0] << 8) + buf[1];
                    }
                    else if (length - 8 == 8)
                    {
                        var tr = ((buf[2] << 8) + buf[3]);
                        var tr_total = ((buf[4] << 8) + buf[5]);
                        tagValue = (tr_total == 0 ? tr.ToString() : (tr + "/" + tr_total));
                    }
                    break;

                case 0x0000000001000000: // Text
                    buf = new byte[length - 8];
                    strm.Read(buf, 0, buf.Length);
                    tagValue = Encoding.UTF8.GetString(buf, 0, buf.Length);
                    break;

                default: // 画像データになる場合のtype値が色々あって把握できないのでdefaultで画像として読んでみるようにする
                    if (createImageObject)
                    {
                        buf = new byte[length - 8];
                        strm.Read(buf, 0, buf.Length);
                        try
                        {
                            tagValuePicture.Add(System.Drawing.Image.FromStream(new MemoryStream(buf, 0, buf.Length)));
                        }
                        catch (Exception)
                        {
                            return;
                        }
                    }
                    else
                    {
                        strm.Seek(length - 8, SeekOrigin.Current);
                    }
                    break;
            }
            return;
        }

        /// <summary>
        /// Read byte array as BIG-ENDIAN-UInt16(2bytes).
        /// </summary>
        /// <param name="buf">Bite array to read</param>
        /// <param name="offset">Data offset in buffer</param>
        /// <returns>Read UInt16 value</returns>
        private static UInt16 BEUInt16(byte[] buf, int offset)
        {
            return (UInt16)(((UInt16)buf[0 + offset] << 8) + (UInt16)buf[1 + offset]);
        }

        /// <summary>
        /// Read byte array as BIG-ENDIAN-UInt32(4bytes).
        /// </summary>
        /// <param name="buf">Bite array to read</param>
        /// <param name="offset">Data offset in buffer</param>
        /// <returns>Read UInt32 value</returns>
        private static UInt32 BEUInt32(byte[] buf, int offset)
        {
            return ((UInt32)buf[0 + offset] << 24) + ((UInt32)buf[1 + offset] << 16) + ((UInt32)buf[2 + offset] << 8) + (UInt32)buf[3 + offset];
        }

        /// <summary>
        /// Read byte array as BIG-ENDIAN-UInt64(8bytes).
        /// </summary>
        /// <param name="buf">Bite array to read</param>
        /// <param name="offset">Data offset in buffer</param>
        /// <returns>Read UInt64 value</returns>
        private static UInt64 BEUInt64(byte[] buf, int offset)
        {
            return ((UInt64)buf[0 + offset] << 56) + ((UInt64)buf[1 + offset] << 48) + ((UInt64)buf[2 + offset] << 40) + ((UInt64)buf[3 + offset] << 32) +
                   ((UInt64)buf[4 + offset] << 24) + ((UInt64)buf[5 + offset] << 16) + ((UInt64)buf[6 + offset] << 8) + ((UInt64)buf[7 + offset]);
        }
    }
}
