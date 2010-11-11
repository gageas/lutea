using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.IO;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.Tags
{
    class ID3V2Tag
    {
        /* ヘッダフラグ */
        const uint FLAG_HEAD_UNSYNC = 0x80;
        const uint FLAG_HEAD_EXTENSION = 0x40;
        const uint FLAG_HEAD_TESTING = 0x20;
        const uint FLAG_HEAD_FOOTER = 0x10;


        /* フレームのフラグはv2.4準拠。 */
        const uint FLAG_FRAME_REMOVE_IF_TAG_MODIFY = 0x4000;
        const uint FLAG_FRAME_REMOVE_IF_FILE_MODIFY = 0x2000;
        const uint FLAG_FRAME_READONLY = 0x1000;

        const uint FLAG_FRAME_GROUPED = 0x0040;
        const uint FLAG_FRAME_COMPRESSED = 0x0008;
        const uint FLAG_FRAME_CRYPTED = 0x0004;
        const uint FLAG_FRAME_UNSYNC = 0x0002;
        const uint FLAG_FRAME_DATALENGTH = 0x0001;


        /* バージョン */
        const byte ID3V22 = 0x01;
        const byte ID3V23 = 0x02;
        const byte ID3V24 = 0x04;
        const byte ALL_VER = (ID3V22 | ID3V23 | ID3V24);

        enum eEncode { ISO_8859_1, UTF_16, UTF_16_BE, UTF_8, UNDEF };

        /* フレームのタイプ */
        public enum FRAME_TYPE { FR_TXT, FR_TXT_PURE, FR_TXT_EX, FR_TXT_EX_LNG, FR_BIN, FR_BIN_EX, FR_APIC };

        #region ID3V2 FRAMES
        public struct _frame_id
        {
            public string name2;
            public string name3;
            public string name4;
            public string asApe;
            public string desc;
            public FRAME_TYPE type;
            public byte ver;
            public _frame_id(string name2, string name3, string name4, string asApe, string desc, FRAME_TYPE type, byte ver)
            {
                this.name2 = name2;
                this.name3 = name3;
                this.name4 = name4;
                this.asApe = asApe;
                this.desc = desc;
                this.type = type;
                this.ver = ver;
            }
        };
        public static _frame_id[] FRAMES = new _frame_id[] {
	new _frame_id("","","","?","",FRAME_TYPE.FR_BIN,ALL_VER), /* どれでもない */
//	new _frame_id("CRA","AENC","AENC","","暗号化",FRAME_TYPE.FR_BIN,ALL_VER),
	new _frame_id("PIC","APIC","APIC","COVER ART","画像",FRAME_TYPE.FR_APIC,ALL_VER),
//	new _frame_id(""   ,""    ,"ASPI","","オーディオシーケンスポイントインデックス",FRAME_TYPE.FR_BIN,ID3V24),
	new _frame_id("COM","COMM","COMM","COMMENT","コメント",FRAME_TYPE.FR_TXT_EX_LNG,ALL_VER),
//	new _frame_id(""   ,"COMR","COMR","","コマーシャルフレーム",FRAME_TYPE.FR_BIN,ID3V23|ID3V24),
//	new _frame_id(""   ,"ENCR","ENCR","","暗号化の手法の登録",FRAME_TYPE.FR_BIN,ID3V23|ID3V24),
//	new _frame_id("EQU","EQUA","EQU2","均一化",FRAME_TYPE.FR_BIN,ALL_VER),
//	new _frame_id("ETC","ETCO","ETCO","イベントタイムコード",FRAME_TYPE.FR_BIN,ALL_VER),
//	new _frame_id("GEO","GEOB","GEOB","パッケージ化された一般的なオブジェクト",FRAME_TYPE.FR_BIN,ALL_VER),
//	new _frame_id(""   ,"GRID","GRID","グループ識別子の登録",FRAME_TYPE.FR_BIN,ID3V23|ID3V24),
//		new _frame_id("IPL","IPLS","TIPL","協力者",FRAME_TYPE.FR_TXT,ALL_VER), //
//		new _frame_id("LINK","LINK","LINK","リンク情報",FRAME_TYPE.FR_TXT_EX,ALL_VER), //
//	new _frame_id("MCI","MCDI","MCDI","音楽CD識別子",FRAME_TYPE.FR_BIN,ALL_VER),
//	new _frame_id("MLL","MLLT","MLLT","MPEGロケーションルックアップテーブル",FRAME_TYPE.FR_BIN,ALL_VER),

//	new _frame_id(""   ,"PRIV","PRIV","プライベートフレーム",FRAME_TYPE.FR_BIN_EX,ID3V23|ID3V24),
//	new _frame_id(""   ,"GRID","GRID","グループ識別子の登録",FRAME_TYPE.FR_BIN,ID3V23|ID3V24),
	new _frame_id("TAL","TALB","TALB","ALBUM","アルバム",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TBP","TBPM","TBPM","BPM",FRAME_TYPE.FR_TXT,ALL_VER),
	new _frame_id("TCM","TCOM","TCOM","COMPOSER","作曲者(Composer)",FRAME_TYPE.FR_TXT,ALL_VER),
		new _frame_id("TXT","TEXT","TEXT","TEXT","作詞家/文書作成者(TEXT)",FRAME_TYPE.FR_TXT,ALL_VER),//
	new _frame_id("TCO","TCON","TCON","GENRE","ジャンル",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TCR","TCOP","TCOP","著作権情報",FRAME_TYPE.FR_TXT,ALL_VER),
	new _frame_id("TDA","TDAT","TDRC","DATE","日付(録音)",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id(""   ,""    ,"TDEN","日付(エンコード)",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id("TOR","TORY","TDOR","日付(オリジナルリリース)",FRAME_TYPE.FR_TXT,ALL_VER), //TORYとTDORですが統合してしまいます
//	new _frame_id(""   ,""    ,"TDRL","日付(リリース)",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id(""   ,""    ,"TDTG","日付(タグ付け)",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id("TEN","TENC","TENC","エンコードした人(Encoder)",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TFL","TFLT","TFLT","ファイルタイプ",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id(""   ,""    ,"TIPL","関わった人々の一覧",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id("TIM","TIME",""    ,"時間(V2.4では日付(録音)の方へ)",FRAME_TYPE.FR_TXT,ID3V22|ID3V23),
	  new _frame_id("TT1","TIT1","TIT1","GROUP","内容の属するグループ",FRAME_TYPE.FR_TXT,ALL_VER),
	new _frame_id("TT2","TIT2","TIT2","TITLE","タイトル",FRAME_TYPE.FR_TXT,ALL_VER),
	  new _frame_id("TT3","TIT3","TIT3","SUBTITLE","サブタイトル",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TKE","TKEY","TKEY","始めの調",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TLA","TLAN","TLAN","言語",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TLE","TLEN","TLEN","長さ",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id(""   ,""    ,"TMCL","ミュージシャンクレジットリスト",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id("TMT","TMED","TMED","メディアタイプ",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id(""   ,""    ,"TMOO","ムード",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id("TOT","TOAL","TOAL","オリジナルのアルバム/映画/ショーのタイトル",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TOF","TOFN","TOFN","オリジナルファイル名",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TOL","TOLY","TOLY","オリジナルの作詞家/文書作成者",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id(""   ,"TOWN","TOWN","ファイルの所有者/ライセンシー",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
	new _frame_id("TP1","TPE1","TPE1","ARTIST","アーティスト",FRAME_TYPE.FR_TXT,ALL_VER),
	  new _frame_id("TP2","TPE2","TPE2","ALBUM ARTIST","バンド/オーケストラ/伴奏",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TP3","TPE3","TPE3","指揮者/演奏者詳細情報",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TP4","TPE4","TPE4","翻訳者/リミックス/その他の修正",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TPA","TPOS","TPOS","セット中の位置",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id(""   ,""    ,"TPRO","Produced notice",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TPB","TPUB","TPUB","出版者",FRAME_TYPE.FR_TXT,ALL_VER),
	new _frame_id("TRK","TRCK","TRCK","TRACK","トラックの番号/セット中の位置",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TRD","TRDA",""    ,"録音日時",FRAME_TYPE.FR_TXT,ID3V22|ID3V23), //とりあえず
//	new _frame_id(""   ,"TRSN","TRSN","インターネットラジオ局の名前",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
//	new _frame_id(""   ,"TRSO","TRSO","インターネットラジオ局の所有者",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
//	new _frame_id(""   ,""    ,"TSOA","アルバムのソートオーダー",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id(""   ,""    ,"TSOP","演奏者のソートオーダー",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id(""   ,""    ,"TSOT","タイトルのソートオーダー",FRAME_TYPE.FR_TXT,ID3V24),
//	new _frame_id("TSI","TSIZ",""    ,"サイズ",FRAME_TYPE.FR_TXT,ID3V22|ID3V23),
//	new _frame_id("TRC","TSRC","TSRC","ISRC",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id("TSS","TSSE","TSSE","エンコードに使用したソフトウェア/ハードウェアとセッティング",FRAME_TYPE.FR_TXT,ALL_VER),
//	new _frame_id(""   ,""    ,"TSST","セットのサブタイトル",FRAME_TYPE.FR_TXT,ID3V24),
	new _frame_id("TYE","TYER",""    ,"DATE","年",FRAME_TYPE.FR_TXT,ID3V22|ID3V23),
//	new _frame_id(""   ,"TCMP","TCMP","コンピレーション(iTunes)",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
	new _frame_id("TXX","TXXX","TXXX","","ユーザー定義文字情報フレーム",FRAME_TYPE.FR_TXT_EX,ALL_VER),
//	new _frame_id("UFI","UFID","UFID","一意的なファイル識別子",FRAME_TYPE.FR_BIN_EX,ALL_VER),
//	new _frame_id(""   ,"USER","USER","使用条件",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
	new _frame_id("ULT","USLT","USLT","LYRICS","非同期歌詞/文書のコピー",FRAME_TYPE.FR_TXT_EX_LNG,ALL_VER),
//	new _frame_id("WCM","WCOM","WCOM","商業上の情報",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
//	new _frame_id("WCP","WCOP","WCOP","著作権/法的情報",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
//	new _frame_id("WAF","WOAF","WOAF","オーディオファイルの公式Webページ",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
//	new _frame_id("WAR","WOAR","WOAR","アーティスト/演奏者の公式Webページ",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
//	new _frame_id("WAS","WOAS","WOAS","音源の公式Webページ",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
//	new _frame_id(""   ,"WORS","WORS","インターネットラジオ局の公式Webページ",FRAME_TYPE.FR_TXT_PURE,ID3V23|ID3V24),
//	new _frame_id(""   ,"WPAY","WPAY","支払い",FRAME_TYPE.FR_TXT_PURE,ID3V23|ID3V24),
//	new _frame_id("WPB","WPUB","WPUB","出版者の公式Webページ",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
//	new _frame_id("WXX","WXXX","WXXX","出版者の公式Webページ",FRAME_TYPE.FR_TXT_EX,ALL_VER),
//	new _frame_id("CRM",""    ,""    ,"暗号化メタフレーム",FRAME_TYPE.FR_BIN,ID3V22),

//	new _frame_id("TDY","TDLY","TDLY","プレイリスト遅延時間",FRAME_TYPE.FR_TXT,ALL_VER),
};
        #endregion

        public class id3v2header
        {
            public short version;
            public int flag; // byte inn original
            public int size;
        };


        public class frame
        {
            public string id; /* eg. TIT2 */
            public int id_x;
            public int size; // original uint
            public uint flag;
            public object value; // データ実体

            public string extid; // TXXX,COMM等の拡張ID
            public string extvalue; // TXXX,COMM等の値

            public byte picture_type;
            public System.Drawing.Image imagebody;
        };

        public class id3tag
        {
            public id3v2header head;
            public List<frame> frame = new List<frame>();
        };

        public static id3v2header read_header(byte[] headerstr)
        {
            id3v2header header = new id3v2header();
            if (Encoding.ASCII.GetString(headerstr, 0, 3) != "ID3") return null;

            switch ((headerstr[3] << 8) + headerstr[4])
            {
                case 0x0200:
                    header.version = 2;
                    if ((headerstr[5] & 0x40) > 0) return null;
                    header.flag = headerstr[5] & 0x80;
                    break;
                case 0x0300:
                    header.version = 3;
                    header.flag = headerstr[5] & (0x80 + 0x40 + 0x20);
                    break;
                case 0x0301:
                    ;
                    goto case 0x0400;
                case 0x0400:
                    header.version = 4;
                    header.flag = headerstr[5] & (0x80 + 0x40 + 0x20 + 0x10);
                    break;
                default:
                    return null;
            }
            header.size = (headerstr[6] << 21) + (headerstr[7] << 14) + (headerstr[8] << 7) + headerstr[9];

            return header;
        }

        static frame read_frame(Stream strm, id3tag tag, bool createImageObject)
        {
            int i;
            int headsize = (tag.head.version == 2) ? 6 : 10;
            if (strm.Length < headsize) return null;
            byte[] buf = new byte[headsize];
            strm.Read(buf, 0, headsize);
            frame fr = new frame();
            fr.id_x = 0; // どれでもない
            uint _size;
            switch (tag.head.version)
            {
                case 2:
                    fr.id = Encoding.ASCII.GetString(buf, 0, 3);
                    _size = ((uint)buf[3] * 256 + buf[4]) * 256 + buf[5];
                    fr.flag = 0;
                    for (i = 0; i < FRAMES.Length; i++) if (fr.id == FRAMES[i].name2) { fr.id_x = i; break; }
                    break;
                case 3:
                    fr.id = Encoding.ASCII.GetString(buf, 0, 4);
                    _size = (((uint)buf[4] * 256 + buf[5]) * 256 + buf[6]) * 256 + buf[7];
                    fr.flag = ((uint)buf[8] << 7) + (((uint)buf[9] >> 4) & 0x0F) + (((uint)buf[9] << 1) & 0x40);
                    for (i = 0; i < FRAMES.Length; i++) if (fr.id == FRAMES[i].name3) { fr.id_x = i; break; }
                    break;
                case 4:
                    fr.id = Encoding.ASCII.GetString(buf, 0, 4);
                    _size = (((uint)buf[4] * 128 + buf[5]) * 128 + buf[6]) * 128 + buf[7];
                    fr.flag = ((uint)buf[8] << 8) + buf[9];
                    for (i = 0; i < FRAMES.Length; i++) if (fr.id == FRAMES[i].name4) { fr.id_x = i; break; }
                    break;
                default:
                    return null;
            }
            if (_size > strm.Length) return null;
            fr.size = (int)_size;
            if ((fr.flag & FLAG_FRAME_CRYPTED) > 0) return null; // 暗号化なんて知りませんよっと
            if ((fr.flag & FLAG_FRAME_COMPRESSED) > 0) return null; // 圧縮は実装してない
            if (fr.size == 0) return null;
            if (fr.id == "") return null;

            int readsize = headsize + fr.size;
            if (readsize > strm.Length) return null;
            int offset = 0;
            byte[] tmp = new byte[fr.size]; // + 2

            strm.Read(tmp, 0, fr.size);
            if (((fr.flag & FLAG_FRAME_UNSYNC) > 0) && tag.head.version == 4)
            {
                tmp = DecodeUnsync(tmp);
            }

            if ((fr.flag & FLAG_FRAME_DATALENGTH) > 0)  // 元サイズ情報を飛ばす
            {
                offset = 4;
                fr.size -= 4;
            }

            /*
            if ((fr.flag & FLAG_FRAME_CRYPTED) > 0) // 暗号方式を飛ばす
            {
                offset += 1;
                fr.size -= 1;
            }
             */

            MemoryStream frameBodyStream = new MemoryStream(tmp, offset, tmp.Length - offset);
            readFrameBody(frameBodyStream, fr, createImageObject);
            return fr;
        }
        static void readFrameBody(Stream sr, frame fr, bool createImageObject)
        {
            byte[] tmp;
            switch ((FRAME_TYPE)FRAMES[fr.id_x].type)
            {
                case FRAME_TYPE.FR_APIC:
                    if (!createImageObject) return;
                    tmp = new byte[sr.Length];
                    sr.Read(tmp, 0, (int)sr.Length);
                    fr.value = null;

                    string imgtype = Encoding.ASCII.GetString(tmp, 1, 3);
                    if ((imgtype == "JPG")||(imgtype == "PNG"))
                    {
                        fr.picture_type = tmp[4];
                        int imgbodyOfset = tmp.IndexOf(5, 0) + 1;
                        MemoryStream ms = new MemoryStream(tmp, imgbodyOfset, tmp.Length - imgbodyOfset);
                        try
                        {
                            fr.value = System.Drawing.Image.FromStream(ms);
                        }
                        catch { }
                    }
                    else // mime
                    {
                        int pictureTypeIndex = tmp.IndexOf(1, 0) + 1;
                        fr.picture_type = tmp[pictureTypeIndex];
                        int imgbodyOfset = tmp.IndexOf(pictureTypeIndex + 1, 0) + 1;
                        MemoryStream ms = new MemoryStream(tmp, imgbodyOfset, tmp.Length - imgbodyOfset);
                        try
                        {
                            fr.value = System.Drawing.Image.FromStream(ms);
                        }
                        catch { }
                    }
                    break;
                default:
                    tmp = new byte[sr.Length];
                    fr.imagebody = null;
                    fr.picture_type = 0;

                    sr.Read(tmp, 0, (int)sr.Length);

                    if (tmp[0] < 0x04)
                    {
                        int offset = 1;
                        if (FRAMES[fr.id_x].type == FRAME_TYPE.FR_TXT_EX_LNG)
                        {
                            offset = 4;
                        }
                        switch ((eEncode)tmp[0])
                        {
                            case eEncode.ISO_8859_1:
                                fr.value = Encoding.Default.GetString(tmp, offset, tmp.Length - offset).Trim();  // ほんとはASCIIにするところなんだけど
                                break;
                            case eEncode.UTF_16:
                                fr.value = Encoding.Unicode.GetString(tmp, offset, tmp.Length - offset).Trim();
                                break;
                            case eEncode.UTF_16_BE:
                                fr.value = Encoding.BigEndianUnicode.GetString(tmp, offset, tmp.Length - offset).Trim();
                                break;
                            case eEncode.UTF_8:
                                fr.value = Encoding.UTF8.GetString(tmp, offset, tmp.Length - offset).Trim();
                                break;
                        }
                        if (FRAMES[fr.id_x].name4 == "TCON") //genreのとき
                        {
                            var re = new System.Text.RegularExpressions.Regex(@"^\((?<1>\d+)\)(?<2>.*)$");
                            var match = re.Match((string)fr.value);
                            if(match.Success){
                                var genrestr = ID3.GetGenreString(int.Parse(match.Groups[1].Value));
                                fr.value = (genrestr != null?genrestr:match.Groups[2].Value);
                            }
                        }
                    }
                    else
                    {
                        fr.value = Encoding.ASCII.GetString(tmp);
                    }

                    if (FRAMES[fr.id_x].type == FRAME_TYPE.FR_TXT_EX || FRAMES[fr.id_x].type == FRAME_TYPE.FR_TXT_EX_LNG)
                    { /* TXXX,COMMなどのとき */
                        int idx = ((string)fr.value).IndexOf('\0');
                        fr.extid = ((string)fr.value).Substring(0, idx).ToUpper();
                        fr.extvalue = ((string)fr.value).Substring(idx + 1).TrimEnd(new char[] { '\0' }).Replace("\0","\r\n"); // FIXME?: foobar2kでいうmultiple Valueの時、区切りの\0を改行で置換（暫定処置）
                        fr.value = fr.extvalue;
                    }
                    else
                    {
                        fr.value = ((string)fr.value).TrimEnd(new char[] { '\0' });
                        fr.extid = fr.extvalue = null;
                    }

                    break;
            }
        }

        public static id3tag read_id3tag(System.IO.Stream strm,bool createImageObject)
        {
            id3tag tag = new id3tag();
            byte[] id3_header = new byte[10];// ID3タグヘッダ用
            strm.ReadOrThrow(id3_header, 0, 10);
            id3v2header header = read_header(id3_header);
            if (header == null) return null;
            if (header.size >= strm.Length) return null;
            tag.head = header;

            if ((tag.head.flag & FLAG_HEAD_EXTENSION) > 0)// 拡張ヘッダがあるときサイズだけ読んでスキップ
            {
                byte[] ext_header = new byte[4];
                strm.Read(ext_header, 0, 4);
                int size;
                if (tag.head.version == 3) // v2.3
                {
                    size = ((ext_header[0] * 256 + ext_header[1]) * 256 + ext_header[2]) * 256 + ext_header[3];
                }
                else // v2.4
                {
                    size = ((ext_header[0] * 128 + ext_header[1]) * 128 + ext_header[2]) * 128 + ext_header[3] - 4;
                }
                strm.Seek(size, System.IO.SeekOrigin.Current);
            }

            /* frame */
            byte[] frame_buf = new byte[tag.head.size];
            strm.Read(frame_buf, 0, tag.head.size);


            if (((tag.head.flag & FLAG_HEAD_UNSYNC) > 0) && tag.head.version == 3)
            {
                frame_buf = DecodeUnsync(frame_buf);
            }

            MemoryStream tagBodyStream = new MemoryStream(frame_buf, 0, frame_buf.Length);
            int count = 0;
            while (true)
            { //offset < tag.head.size
                frame fr = read_frame(tagBodyStream, tag, createImageObject);
                if (fr == null) break;
                count++;
                tag.frame.Add(fr);
            }
            return tag;
        }

        static byte[] DecodeUnsync(byte[] src)
        {/* 非同期化を解除する */
            int i = 0, pos = 0;
            byte[] work = new byte[src.Length];
            for (i = 0; i < src.Length; i++, pos++)
            {
                work[pos] = src[i];
                if (src[i] == 0xFF){
                    if ((i + 1 < src.Length) && (src[i + 1] == 0x00)) { i++; }
                }
            }
            byte[] ret = new byte[pos];
            Buffer.BlockCopy(work, 0, ret, 0, pos);
            return ret;
        }
    }
}
