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
        /// Read MP4 audio meta data.
        /// </summary>
        /// <param name="strm">MP4 input data stream</param>
        /// <param name="createImageObject">Read embedded Cover Art(=true) or not(=false)</param>
        /// <returns>List of meta data or null</returns>
        public static List<KeyValuePair<string, object>> Read(Stream strm, bool createImageObject = true)
        {
            List<KeyValuePair<string, object>> tag = new List<KeyValuePair<string, object>>();

            try
            {
                ReadRecurse(strm, strm.Length, tag, createImageObject);
            }
            catch (Exception) { }

            return tag;
        }

        /// <summary>
        /// Read "name" node.
        /// </summary>
        /// <param name="strm"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private static string ReadName(Stream strm, int length)
        {
            var buf = new byte[length];
            strm.Read(buf, 0, length);
            return Encoding.ASCII.GetString(buf, 4, (int)length - 4);
        }

        /// <summary>
        /// Read "data" node.
        /// </summary>
        /// <param name="strm"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="createImageObject">Read embedded Cover Art(=true) or not(=false)</param>
        /// <returns></returns>
        private static object ReadData(Stream strm, int length, bool createImageObject)
        {
            if(length <= 8){
                return null;
            }
            byte[] buf;
            var buf_type = new byte[8];
            strm.Read(buf_type, 0, (int)8);
            switch (BitConverter.ToUInt64(buf_type, 0))
            {
                case 0: // trkn or disk. "nn/mm" style.
                    buf = new byte[length - 8];
                    strm.Read(buf,0,buf.Length);
                    if (length - 8 == 2)
                    {
                        return (buf[0] << 8) + buf[1];
                    }
                    else
                    {
                        var tr = ((buf[2] << 8) + buf[3]);
                        var tr_total = ((buf[4] << 8) + buf[5]);
                        return (tr_total == 0 ? tr.ToString() : (tr + "/" + tr_total));
                    }

                case 0x0000000001000000: // Text
                    buf = new byte[length - 8];
                    strm.Read(buf, 0, buf.Length);
                    return Encoding.UTF8.GetString(buf, 0, buf.Length);

//                case 0x000000000D000000: // Image
//                case 0x000000000E000000: // Image
//                case 0x0D0000000D000000: // Image moraで見つけた
                default: // 画像データになる場合のtype値が色々あって把握できないのでdefaultで画像として読んでみるようにする
                    if (createImageObject)
                    {
                        buf = new byte[length - 8];
                        strm.Read(buf, 0, buf.Length);
                        try
                        {
                            return System.Drawing.Image.FromStream(new MemoryStream(buf, 0, buf.Length));
                        }
                        catch (Exception) {
                            return null;
                        }
                    }
                    else
                    {
                        strm.Seek(length - 8, SeekOrigin.Current);
                    }
                    strm.Seek(length - 8, SeekOrigin.Current);
                    break;
            }
            return null;
        }

        /// <summary>
        /// Read MP4 structure recursively and make a list of meta data.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="tags"></param>
        /// <param name="strm"></param>
        /// <param name="createImageObject"></param>
        /// <param name="tagKey">Use this string as tag-key (and "data" node's value as tag-value). This string will provided from "data" node's parent or previous node.</param>
        private static void ReadRecurse(Stream strm, long length, List<KeyValuePair<string, object>> tags, bool createImageObject = true, string tagKey = null)
        {
            long p = 0;
            string lastTagKeyName = null;
            byte[] header = new byte[NODE_LIST_HEADER_SIZE];

            while (p < length)
            {
                strm.Read(header, 0, NODE_LIST_HEADER_SIZE);
                UInt32 atom_size = BEUInt32(header, 0) - NODE_LIST_HEADER_SIZE;
                string atom_name = Encoding.ASCII.GetString(header, NODE_LENGTH_FIELD_SIZE, NODE_NAME_FIELD_SIZE);
                p += atom_size + NODE_LIST_HEADER_SIZE;

                long initial_pos = strm.Position;
                switch (atom_name)
                {
                    case "moov":
                    case "udta":
                    case "ilst":
                    case "disk":
                    case "----":
                        ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject);
                        break;

                    case "meta":
                        strm.Seek(4, SeekOrigin.Current);
                        ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE - 4, tags, createImageObject);
                        break;

                    case "data":
                        if (lastTagKeyName == null) lastTagKeyName = tagKey;
                        if (lastTagKeyName == null) break;
                        object data = ReadData(strm, (int)atom_size, createImageObject);
                        if(data == null){
                           lastTagKeyName = null;                            
                            break;
                        }
                        if (tagKey == "GENRE" && data is int)
                        {
                            var genreStr = ID3.GetGenreString((int)data - 1);
                            if (genreStr != null)
                            {
                                tags.Add(new KeyValuePair<string, object>(lastTagKeyName, genreStr));
                            }
                        }
                        else
                        {
                            tags.Add(new KeyValuePair<string, object>(lastTagKeyName, data));
                        }
                        lastTagKeyName = null;
                        break;

                    case "trkn":
                        ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "TRACK");
                        break;

                    case "aART":
                        ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "ALBUM ARTIST");
                        break;

                    case "gnre":
                        ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "GENRE");
                        break;

                    case "covr":
                        ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "COVER ART");
                        break;

                    case "name":
                        string name = ReadName(strm, (int)atom_size);
                        if (name == "iTunSMPB")
                        {
                            lastTagKeyName = "ITUNSMPB";
                        }
                        break;

                    // Non-Text node name. Read as hex.
                    default:
                        switch (BitConverter.ToUInt32(header, NODE_NAME_FIELD_SIZE)) // NOTICE: for Little Endian
                        {
                            case 0x545241A9: // .ART Artist
                                ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "ARTIST");
                                break;
                            case 0x6D616EA9: // .nam Track
                                ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "TITLE");
                                break;
                            case 0x626C61A9: // .alb Album
                                ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "ALBUM");
                                break;
                            case 0x6E6567A9: // .gen Genre
                                ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "GENRE");
                                break;
                            case 0x796164A9: // .dat Date
                                ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "DATE");
                                break;
                            case 0x746D63A9: // .cmt Comment
                                ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "COMMENT");
                                break;
                            case 0x747277A9: // .wrt Writer
                                ReadRecurse(strm, atom_size - NODE_LIST_HEADER_SIZE, tags, createImageObject, "COMPOSER");
                                break;
                            default:
                                break;
                        }
                        break;
                }

                strm.Seek(initial_pos += atom_size, SeekOrigin.Begin);
            }
        }
    }
}
