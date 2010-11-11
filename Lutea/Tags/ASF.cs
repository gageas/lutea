using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Tags
{
    /// <summary>
    /// WMA等ASFフォーマットのメタデータを読むクラス
    /// 行き当たりばったりで書いたのでひどい
    /// </summary>
    class ASF
    {
        private static readonly Guid GUID_HEADER_OBJECT = new Guid(new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C });
        private static readonly Guid GUID_CONTENTS_DESCRIPTION_OBJECT = new Guid(new byte[] { 0x33, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C });
        private static readonly Guid GUID_EXTENDED_CONTENTS_DESCRIPTION_OBJECT = new Guid("d2d0a440-e307-11d2-97f0-00a0c95ea850");
        private static readonly Guid GUID_HEADER_EXTENSION_OBJECT = new Guid("5fbf03b5-a92e-11cf-8ee3-00c00c205365");
        private static readonly Guid GUID_METADATA_OBJECT = new Guid("C5F8CBEA-5BAF-4877-8467-AA8C44FA4CCA");
        private static readonly Guid GUID_METADATA_LIBRARY_OBJECT = new Guid("44231c94-9498-49d1-a141-1d134e457054");
        private struct HeaderObject {
            public UInt64 size;
            public UInt32 count;
//            public byte reserved1;
//            public byte reserved2;
        }

        private static readonly Dictionary<string, string> WM2APE_MAP = new Dictionary<string, string>()
        {
            {"WM/TRACKNUMBER","TRACK"},
            {"WM/PUBLISHER","PUBLISHER"},
            {"WM/GENRE","GENRE"},
            {"WM/ALBUMARTIST","ALBUMARTIST"},
            {"WM/YEAR","DATE"},
            {"WM/ALBUMTITLE","ALBUM"},
        };

        private struct ContentsDescriptionObject
        {
            public UInt64 size;
            public UInt16 title_length;
            public UInt16 artist_length;
            public UInt16 copy_length;
            public UInt16 description_length;
            public UInt16 rating_length;
            public string title;
            public string artist;
            public string copyright;
            public string description;
            public string rating;
        }
        public static List<KeyValuePair<string, object>> Read(Stream stream, bool createImageObject)
        {
            var header = new HeaderObject();
            Guid guid = ReadGuid(stream);
            if (!guid.Equals(GUID_HEADER_OBJECT)) return null;
            var buf = new byte[14];
            stream.Read(buf, 0, 14);
            header.size = BitConverter.ToUInt64(buf, 0);
            header.count = BitConverter.ToUInt32(buf, 8);

            List<KeyValuePair<string, object>> ecd = null;
            List<KeyValuePair<string, object>> heo = null;
            ContentsDescriptionObject cd = default(ContentsDescriptionObject);
            for (int i = 0; i < header.count; i++)
            {
                var guid_sub = ReadGuid(stream, 0);
                Logger.Log(guid_sub.ToString() + " , "+ stream.Position);
                if (guid_sub.Equals(GUID_CONTENTS_DESCRIPTION_OBJECT))
                {
                    cd = ReadContentsDescriptionObject(stream, 0);
                }
                else if (guid_sub.Equals(GUID_EXTENDED_CONTENTS_DESCRIPTION_OBJECT))
                {
                    ecd = ReadExtendedContentsDescriptionObject(stream, 0);
                }
                else if (guid_sub.Equals(GUID_HEADER_EXTENSION_OBJECT))
                {
                    heo = ReadHeaderExtensionObject(stream, 0);
                }
                else
                {
                    byte[] buf_size = new byte[8];
                    stream.Read(buf_size, 0, 8);
                    stream.Seek((long)BitConverter.ToUInt64(buf_size, 0) - 16 - 8, SeekOrigin.Current);
                }
            }
            if (ecd != null)
            {
                if (ecd.Find((e) => e.Key == "TITLE").Key == null && cd.title != null) ecd.Add(new KeyValuePair<string, object>("TITLE", cd.title));
                if (ecd.Find((e) => e.Key == "ARTIST").Key == null && cd.artist != null) ecd.Add(new KeyValuePair<string, object>("ARTIST", cd.artist));
                if (ecd.Find((e) => e.Key == "COMMENT").Key == null && cd.description != null) ecd.Add(new KeyValuePair<string, object>("COMMENT", cd.description));
                if (heo != null)
                {
                    ecd.AddRange(heo);
                }
                return ecd;
            }
            else
            {
                var tag = new List<KeyValuePair<string, object>>();
                if (cd.title != null) tag.Add(new KeyValuePair<string, object>("TITLE", cd.title));
                if (cd.artist != null) tag.Add(new KeyValuePair<string, object>("ARTIST", cd.artist));
                if (cd.description != null) tag.Add(new KeyValuePair<string, object>("COMMENT", cd.description));
                return tag;
            }
        }

        private static Guid ReadGuid(Stream strm, int offset = 0)
        {
            byte[] buf = new byte[16];
            strm.Read(buf,offset,16);
            return new Guid(buf);
        }

        private static ContentsDescriptionObject ReadContentsDescriptionObject(Stream strm,int offset = 0)
        {
            byte[] buf = new byte[18];
            strm.Read(buf, offset, 18);
            ContentsDescriptionObject cd = new ContentsDescriptionObject();
            cd.size = BitConverter.ToUInt64(buf, 0);
            cd.title_length = BitConverter.ToUInt16(buf, 8);
            cd.artist_length = BitConverter.ToUInt16(buf, 8+2);
            cd.copy_length = BitConverter.ToUInt16(buf, 8+2+2);
            cd.description_length = BitConverter.ToUInt16(buf, 8+2+2+2);
            cd.rating_length = BitConverter.ToUInt16(buf, 8+2+2+2+2);

            byte[] buf_body = new byte[cd.size - 18 - 16];
            strm.Read(buf_body,offset,buf_body.Length);
            if(cd.title_length > 0) cd.title = Encoding.Unicode.GetString(buf_body, 0, cd.title_length-2);
            if (cd.artist_length > 0) cd.artist = Encoding.Unicode.GetString(buf_body, cd.title_length, cd.artist_length - 2);
            if (cd.copy_length > 0) cd.copyright = Encoding.Unicode.GetString(buf_body, cd.title_length + cd.artist_length, cd.copy_length - 2);
            if (cd.description_length > 0) cd.description = Encoding.Unicode.GetString(buf_body, cd.title_length + cd.artist_length + cd.copy_length, cd.description_length - 2);
            if (cd.rating_length > 0) cd.rating = Encoding.Unicode.GetString(buf_body, cd.title_length + cd.artist_length + cd.copy_length + cd.description_length, cd.rating_length - 2);
            return cd;
        }

        private static List<KeyValuePair<string, object>> ReadHeaderExtensionObject(Stream strm, int offset = 0)
        {
            byte[] buf = new byte[30];
            strm.Read(buf, offset, 30);
            UInt64 size = BitConverter.ToUInt64(buf, 0);
            UInt32 header_extension_data_size = BitConverter.ToUInt32(buf, 26);

            var tag = new List<KeyValuePair<string, object>>();
            long positon_end = strm.Position + header_extension_data_size;
            while (strm.Position < positon_end)
            {
                Guid guid = ReadGuid(strm);
                byte[] buf2 = new byte[8];
                strm.Read(buf2,0,8);
                UInt64 data_size = BitConverter.ToUInt64(buf2, 0);
                if (guid.Equals(GUID_METADATA_OBJECT))
                {
                    byte[] buf3 = new byte[2];
                    strm.Read(buf3, 0, 2);
                    int count = BitConverter.ToUInt16(buf3, 0);
                    for (int i = 0; i < count; i++)
                    {
                        byte[] buf4 = new byte[12];
                        strm.Read(buf4, 0, buf4.Length);
                        ushort name_length = BitConverter.ToUInt16(buf4, 4);
                        ushort data_type = BitConverter.ToUInt16(buf4, 6);
                        uint data_length = BitConverter.ToUInt32(buf4, 8);
                        byte[] buf_body = new byte[name_length + data_length];
                        strm.Read(buf_body, 0, buf_body.Length);
                        string name = Encoding.Unicode.GetString(buf_body, 0, name_length - 2);
                        Logger.Log(name);
                    }
                }
                else if (guid.Equals(GUID_METADATA_LIBRARY_OBJECT))
                {
                    byte[] buf3 = new byte[2];
                    strm.Read(buf3, 0, 2);
                    int count = BitConverter.ToUInt16(buf3, 0);
                    for (int i = 0; i < count; i++)
                    {
                        byte[] buf4 = new byte[12];
                        strm.Read(buf4, 0, buf4.Length);
                        ushort name_length = BitConverter.ToUInt16(buf4, 4);
                        ushort data_type = BitConverter.ToUInt16(buf4, 6);
                        uint data_length = BitConverter.ToUInt32(buf4, 8);
                        byte[] buf_body = new byte[name_length + data_length];
                        strm.Read(buf_body, 0, buf_body.Length);
                        string name = Encoding.Unicode.GetString(buf_body, 0, name_length - 2);
                        if (name == "WM/Picture")
                        {
                            var memst = new MemoryStream(buf_body,name_length,buf_body.Length - name_length);
                            byte type = (byte)memst.ReadByte();
                            byte[] buf5 = new byte[4];
                            memst.Read(buf5, 0, 4);
                            var datalen = BitConverter.ToUInt32(buf5, 0);

                            byte[] buf6 = new byte[2];
                            // MIMEを読み捨て
                            do
                            {
                                memst.Read(buf6, 0, 2);
                            } while (buf6[0] != 0 || buf6[1] != 0);

                            // Description読み捨て
                            do
                            {
                                memst.Read(buf6, 0, 2);
                            } while (buf6[0] != 0 || buf6[1] != 0);

                            var memst2 = new MemoryStream(buf_body, (int)(name_length + memst.Position), (int)(buf_body.Length - name_length - memst.Position));
                            tag.Add(new KeyValuePair<string, object>("COVER ART", System.Drawing.Image.FromStream(memst2)));
                        }
                        Logger.Log(name);
                    }
                }
                else
                {
                    Logger.Log("throughed " + guid.ToString());
                    strm.Seek((long)data_size - 8 - 16, SeekOrigin.Current);
                }
            }
            return tag;
        }


        private static List<KeyValuePair<string,object>> ReadExtendedContentsDescriptionObject(Stream strm, int offset = 0)
        {
            byte[] buf = new byte[10];
            strm.Read(buf, offset, 10);
            UInt64 size = BitConverter.ToUInt64(buf, 0);
            ushort count = BitConverter.ToUInt16(buf, 8);

            if (count == 0) return null;

            var tag = new List<KeyValuePair<string, object>>();
            for (int i = 0; i < count; i++)
            {
                byte[] buf_nameheader = new byte[2];
                strm.Read(buf_nameheader,0,2);
                ushort name_length = BitConverter.ToUInt16(buf_nameheader, 0);
                byte[] buf_namebody = new byte[name_length];
                strm.Read(buf_namebody, 0, name_length);
                string name = Encoding.Unicode.GetString(buf_namebody,0,name_length-2).ToUpper();
                if (WM2APE_MAP.ContainsKey(name)) name = WM2APE_MAP[name];

                byte[] buf_valueheader = new byte[4];
                strm.Read(buf_valueheader, 0, 4);
                ushort value_type = BitConverter.ToUInt16(buf_valueheader, 0);
                ushort value_length = BitConverter.ToUInt16(buf_valueheader, 2);
                byte[] value_body = new byte[value_length];
                strm.Read(value_body, 0, value_length);
                switch (value_type)
                {
                    case 0:
                        tag.Add(new KeyValuePair<string, object>(name, Encoding.Unicode.GetString(value_body,0,value_length-2)));
                        break;
                    case 1:
                        tag.Add(new KeyValuePair<string, object>(name, value_body));
                        break;
                    case 2:
                        tag.Add(new KeyValuePair<string, object>(name, BitConverter.ToBoolean(value_body, 0)));
                        break;
                    case 3:
                        tag.Add(new KeyValuePair<string, object>(name, BitConverter.ToUInt32(value_body, 0)));
                        break;
                    case 4:
                        tag.Add(new KeyValuePair<string, object>(name, BitConverter.ToUInt64(value_body, 0)));
                        break;
                    case 6:
                        tag.Add(new KeyValuePair<string, object>(name, BitConverter.ToUInt16(value_body, 0)));
                        break;
                }
            }
            return tag;
        }

     }
}
