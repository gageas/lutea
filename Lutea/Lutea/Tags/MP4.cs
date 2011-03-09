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
            ATOM_nodeList rootAtom = new ATOM_nodeList();
            rootAtom.CreateHeavyObject = createImageObject;
            rootAtom.BuildFromStream(strm,0, strm.Length);

            var artist = getTagValue<ATOM__ART>(rootAtom);
            if (artist != null) tag.Add(new KeyValuePair<string, object>("ARTIST", artist));

            var title = getTagValue<ATOM__nam>(rootAtom);
            if (title != null) tag.Add(new KeyValuePair<string, object>("TITLE", title));

            var album = getTagValue<ATOM__alb>(rootAtom);
            if (album != null) tag.Add(new KeyValuePair<string, object>("ALBUM", album));

            var track = getTagValue<ATOM_trkn>(rootAtom);
            if (track != null) tag.Add(new KeyValuePair<string, object>("TRACK", track));

            var date = getTagValue<ATOM__dat>(rootAtom);
            if (date != null) tag.Add(new KeyValuePair<string, object>("DATE", date));

            var cover = getTagValue<ATOM_covr>(rootAtom);
            if (cover != null) tag.Add(new KeyValuePair<string, object>("COVER ART", cover));

            var tmp = rootAtom.GetChildNodes<ATOM_____>();
            foreach (var e in tmp)
            {
                var s = e.GetChildNode<ATOM_name>().Value;
                if (s == "iTunSMPB")
                {
                    tag.Add(new KeyValuePair<string, object>("ITUNSMPB", e.GetChildNode<ATOM_data>().strValue));
                }
            }

            return tag;
        }

        abstract class ATOM : IEnumerable<ATOM>
        {
            // nullのとき、親ノードの値を継承する
            private Boolean? createHeavyObject = null;
            protected List<ATOM> childNodes;
            public ATOM ParentNode = null;

            public abstract void BuildFromStream(Stream strm, int offset, long length);

            public ATOM()
            {
                this.childNodes = new List<ATOM>();
            }

            public T GetChildNode<T>() where T:ATOM {
                if (childNodes == null) return null;
                foreach (ATOM atom in childNodes)
                {
                    if (atom is T) return (T)atom;
                    T sub = atom.GetChildNode<T>();
                    if (sub != null) return sub;
                }
                return null;
            }

            public IEnumerable<T> GetChildNodes<T>() where T : ATOM
            {
                if (childNodes == null) return null;
                List<T> list = new List<T>();
                foreach (ATOM atom in childNodes)
                {
                    if (atom is T) list.Add((T)atom);
                    var sub = atom.GetChildNodes<T>();
                    list.AddRange(sub);
                }
                return list;
            }

            public void AddChild(ATOM node)
            {
                node.ParentNode = this;
                childNodes.Add(node);
            }

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
            private static readonly Regex linehead = new Regex(@"^", RegexOptions.Multiline);
            public ATOM_nodeList()
                : base()
            {
            }
            public override string ToString()
            {
                List<string> buf = new List<string>();
                foreach (var node in childNodes)
                {
                    buf.Add(linehead.Replace(node.GetType().Name + " : " + node.ToString(), "    "));
                }
                return this.GetType().Name + Environment.NewLine + String.Join(Environment.NewLine, buf.ToArray());
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
                        case "----":
                            atom = new ATOM_____();
                            break;
                        case "name":
                            atom = new ATOM_name();
                            break;
                        case "mean":
                            atom = new ATOM_mean();
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
                                case 0x796164A9: // .dat Date
                                    atom = new ATOM__dat();
                                    break;
                                default:
//                                    Logger.Debug("There's no rule to read " + atom_name);
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

        private class ATOM_moov : ATOM_nodeList { }

        private class ATOM_udta : ATOM_nodeList { }

        private class ATOM_meta : ATOM_nodeList
        {
            public override void BuildFromStream(Stream strm, int offset, long length)
            {
                base.BuildFromStream(strm, offset + 4, length);
            }
        }

        private class ATOM_ilst : ATOM_nodeList { }

        private class ATOM_trkn : ATOM_nodeList { }

        private class ATOM_disk : ATOM_nodeList { }

        private class ATOM__ART : ATOM_nodeList { }

        private class ATOM__nam : ATOM_nodeList { }

        private class ATOM__alb : ATOM_nodeList { }

        private class ATOM__gen : ATOM_nodeList { }

        private class ATOM__dat : ATOM_nodeList { }

        private class ATOM_covr : ATOM_nodeList { }

        private class ATOM_____ : ATOM_nodeList { } // ハイフン4つ

        private class ATOM_name : ATOM
        {
            public string Value;
            public override string ToString()
            {
                return Value.ToString();
            }
            public override void BuildFromStream(Stream strm, int offset, long length)
            {
                byte[] buf = new byte[length];
                strm.Read(buf, offset, (int)length);
                Value = Encoding.ASCII.GetString(buf, 4, (int)length - 4);
            }
        }

        private class ATOM_mean : ATOM
        {
            public string Value;
            public override string ToString()
            {
                return Value.ToString();
            }
            public override void BuildFromStream(Stream strm, int offset, long length)
            {
                byte[] buf = new byte[length];
                strm.Read(buf, offset, (int)length);
                Value = Encoding.ASCII.GetString(buf, 4, (int)length - 4);
            }
        }

        private class ATOM_data : ATOM
        {
            public string strValue;
            public object objValue;
            public override string ToString()
            {
                if (strValue != null) return strValue;
                if (objValue != null) return "BinaryObject";
                return "null";
            }
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
