using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Gageas.Lutea.Tags
{
    class MP4
    {
        private static object getTagValue<T>(ATOM root) where T:ATOM
        {
            var node = root.GetChildNode<T>();
            if (node == null) return null;
            var data = (ATOM_data)node.First();
            if (data == null) return null;
            if (data.objValue != null) return data.objValue;
            return data.strValue;
        }

        private static int BEint(byte[] buf,int offset)
        {
            return (buf[0 + offset] << 24) + (buf[1 + offset] << 16) + (buf[2 + offset] << 8) + buf[3 + offset];
        }

        public static List<KeyValuePair<string,object>> Read(Stream strm, bool createImageObject = true){
            List<KeyValuePair<string,object>> tag = new List<KeyValuePair<string,object>>();
            ATOM_nodeList atom = new ATOM_nodeList();
            atom.CreateHeavyObject = createImageObject;
            atom.BuildFromStream(strm,0, strm.Length);

            var artist = getTagValue<ATOM__ART>(atom);
            if (artist != null) tag.Add(new KeyValuePair<string, object>("ARTIST", artist));

            var title = getTagValue<ATOM__nam>(atom);
            if (title != null) tag.Add(new KeyValuePair<string, object>("TITLE", title));

            var album = getTagValue<ATOM__alb>(atom);
            if (album != null) tag.Add(new KeyValuePair<string, object>("ALBUM", album));

            var track = getTagValue<ATOM_trkn>(atom);
            if (track != null) tag.Add(new KeyValuePair<string, object>("TRACK", track));

            var cover = getTagValue<ATOM_covr>(atom);
            if (cover != null) tag.Add(new KeyValuePair<string, object>("COVER ART", cover));

            return tag;
        }

        abstract class ATOM : IEnumerable<ATOM>
        {
//            public IEnumerator<ATOM> GetEnumerator()
//            {
//                return childNodes.GetEnumerator();
//            }
            public ATOM ParentNode = null;
            private List<ATOM> childNodes;
            public ATOM()
            {
                this.childNodes = new List<ATOM>();
            }
            public abstract void BuildFromStream(Stream strm, int offset, long length);


            public T GetChildNode<T>() where T:ATOM {
                foreach (ATOM atom in childNodes)
                {
                    if (atom is T) return (T)atom;
                    T sub = atom.GetChildNode<T>();
                    if (sub != null) return sub;
                }
                return null;
            }

            public void AddChild(ATOM node)
            {
                node.ParentNode = this;
                childNodes.Add(node);
            }

            private Boolean? createHeavyObject = null;
            public Boolean CreateHeavyObject
            {
                set
                {
                    createHeavyObject = value;
                }
                get{
                    if (createHeavyObject == null)
                    {
                        if (ParentNode != null)
                        {
                            return ParentNode.CreateHeavyObject;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return createHeavyObject??false;
                    }
                }
            }

            IEnumerator<ATOM> IEnumerable<ATOM>.GetEnumerator()
            {
                return childNodes.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return childNodes.GetEnumerator();
            }
        }

        class ATOM_nodeList : ATOM
        {
            private const int NODE_LIST_HEADER_SIZE = 8;
            public ATOM_nodeList()
                : base()
            {
            }
            public override void BuildFromStream(Stream strm,int offset,long length){
                long p = 0;
                strm.Seek(offset, SeekOrigin.Current);

                while (p < length)
                {
                    byte[] header = new byte[NODE_LIST_HEADER_SIZE];
                    strm.Read(header, 0, NODE_LIST_HEADER_SIZE);
                    int atom_size = BEint(header, 0) - NODE_LIST_HEADER_SIZE;
                    string atom_name = Encoding.ASCII.GetString(header, 4, 4);
                    p += atom_size + NODE_LIST_HEADER_SIZE;

                    long initial_pos = strm.Position;
                    ATOM atom = null;
                    switch (atom_name)
                    {
// 今の用途では読んでも仕方ないので殺す
//                        case "ftyp":
//                            atom = new ATOM_ftyp(strm, atom_size);
//                            break;
                        case "moov":
//                            atom = new ATOM_moov(strm, atom_size);
                            atom = new ATOM_moov();
                            break;
                        case "udta":
                            atom = new ATOM_udta();
                            break;
                        case "meta":
                            atom = new ATOM_meta();
                            break;
                        case "ilst":
                            atom = new ATOM_ilst();
                            break;
                        case "data":
                            atom = new ATOM_data();
                            break;
                        case "trkn":
                            atom = new ATOM_trkn();
                            break;
                        case "disk":
                            atom = new ATOM_disk();
                            break;
                        case "covr":
                            atom = new ATOM_covr();
                            break;
                        default:
                            switch (BitConverter.ToUInt32(header, 4)) // NOTICE: for Little Endian
                            {
                                case 0x545241A9: // .ART Artist
                                    atom = new ATOM__ART();
                                    break;
                                case 0x6D616EA9: // .nam Track
                                    atom = new ATOM__nam();
                                    break;
                                case 0x626C61A9: // .alb Album
                                    atom = new ATOM__alb();
                                    break;
                                case 0x6E6567A9: // .gen Genre
                                    atom = new ATOM__gen();
                                    break;
                            }
                            break;
                    }
                    try
                    {
                        if (atom != null)
                        {
                            this.AddChild(atom);
                            atom.BuildFromStream(strm, 0, atom_size);
                        }
                    }
                    finally
                    {
                        strm.Seek(initial_pos += atom_size, SeekOrigin.Begin);
                    }
                }
            }

        }

        private class ATOM_ftyp : ATOM
        {
            public string[] CompatibleBrand;
            public override void BuildFromStream(Stream strm,int offset, long length)
            {
                CompatibleBrand = new string[length/4];
                byte[] buf = new byte[length];
                strm.Read(buf,offset,(int)length);
                for(int i=0;i<CompatibleBrand.Length;i++){
                    CompatibleBrand[i] = Encoding.ASCII.GetString(buf);
                }
            }
        }

        private class ATOM_moov : ATOM_nodeList
        {

        }

        private class ATOM_udta : ATOM_nodeList
        {
        }

        private class ATOM_meta : ATOM_nodeList
        {
            public override void BuildFromStream(Stream strm, int offset, long length)
            {
                base.BuildFromStream(strm, offset + 4, length);
            }
        }

        private class ATOM_ilst : ATOM_nodeList
        {
        }

        private class ATOM_trkn : ATOM_nodeList
        {
        }

        private class ATOM_disk : ATOM_nodeList
        {
        }

        private class ATOM__ART : ATOM_nodeList
        {
        }

        private class ATOM__nam : ATOM_nodeList
        {
        }

        private class ATOM__alb : ATOM_nodeList
        {
        }

        private class ATOM__gen : ATOM_nodeList
        {
        }

        private class ATOM_covr : ATOM_nodeList
        {
        }

        private class ATOM_data : ATOM
        {
            public string strValue;
            public object objValue;
            public override void BuildFromStream(Stream strm,int offset, long length)
            {
                byte[] buf = new byte[length];
                strm.Read(buf, offset, (int)length);
                switch (BitConverter.ToUInt64(buf, 0))
                {
                    case 0: // trknとかdiskの場合。nn/mm形式の数値
                        strValue = ((buf[10]<<8) + buf[11]) + "/" + ((buf[12] << 8) + buf[13]);
                        break;
                    case 0x000000000D000000: // 画像
                        if (CreateHeavyObject)
                        {
                            objValue = System.Drawing.Image.FromStream(new MemoryStream(buf, 8, buf.Length - 8));
                        }
                        break;
                    case 0x0000000001000000: // Text
                        strValue = Encoding.UTF8.GetString(buf, 8, buf.Length - 8);
                        break;
                }
            }
        }
    }
}
