using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.Tags
{
    class ID3V2Tag
    {
        #region 列挙体定義
        /// <summary>
        /// ヘッダフラグ
        /// </summary>
        [Flags]
        public enum HEADER_FLAG : byte
        {
            UNSYNC = 0x80,
            EXTENSION = 0x40,
            COMPRESSION = 0x40,
            TESTING = 0x20,
            FOOTER = 0x10,
        }

        /// <summary>
        /// フレームのフラグ。v2.4準拠
        /// </summary>
        [Flags]
        public enum FRAME_FLAG : int
        {
            REMOVE_IF_TAG_MODIFY = 0x4000,
            REMOVE_IF_FILE_MODIFY = 0x2000,
            READONLY = 0x1000,

            GROUPED = 0x0040,
            COMPRESSED = 0x0008,
            CRYPTED = 0x0004,
            UNSYNC = 0x0002,
            DATALENGTH = 0x0001,
        }

        /// <summary>
        /// フレームタイプ
        /// </summary>
        public enum FRAME_TYPE { 
            FR_TXT, 
            FR_TXT_PURE, 
            FR_TXT_EX, 
            FR_TXT_EX_LNG, 
            FR_BIN, 
            FR_BIN_EX, 
            FR_APIC 
        };

        /// <summary>
        /// ID3V2のバージョン
        /// </summary>
        [Flags]
        public enum ID3V2_VER { 
            ID3V22, 
            ID3V23, 
            ID3V24 
        };
        #endregion

        #region クラス定義
        public class FrameID
        {
            public readonly string Name2;
            public readonly string Name3;
            public readonly string Name4;
            public readonly string AsApe;
            public readonly string Desc;
            public readonly FRAME_TYPE Type;
            //public readonly ID3V2Ver Ver;
            public FrameID(string name2, string name3, string name4, string asApe, string desc, FRAME_TYPE type, ID3V2_VER ver)
            {
                this.Name2 = name2;
                this.Name3 = name3;
                this.Name4 = name4;
                this.AsApe = asApe;
                this.Desc = desc;
                this.Type = type;
                //this.Ver = ver;
            }
        };

        public class ID3tag
        {
            public Header head;
            public List<Frame> frame = new List<Frame>();
        };

        public class Header
        {
            public ID3V2_VER Version;
            public HEADER_FLAG Flag;
            public int Size;
        };

        public class Frame
        {
            public FrameID ID;
            public FRAME_FLAG Flag = 0;
            public int Size;
            public string Value; // データ実体
            public string Extid; // TXXX,COMM等の拡張ID
            public byte Picture_type;
            public System.Drawing.Image Image;
        };

        public class EndOfTagException : Exception { };
        #endregion

        #region 定数定義
        private const int HEADER_LEN = 10;
        private const int EXT_HEADER_LEN = 4;
        private const int FRAME_HEADER_LEN_2 = 6;
        private const int FRAME_HEADER_LEN_34 = 10;

        private const byte HEADER_FLAG_MASK_2 = (byte)(HEADER_FLAG.UNSYNC | HEADER_FLAG.COMPRESSION);
        private const byte HEADER_FLAG_MASK_3 = (byte)(HEADER_FLAG.UNSYNC | HEADER_FLAG.EXTENSION | HEADER_FLAG.TESTING);
        private const byte HEADER_FLAG_MASK_4 = (byte)(HEADER_FLAG.UNSYNC | HEADER_FLAG.EXTENSION | HEADER_FLAG.TESTING | HEADER_FLAG.FOOTER);

        private const ID3V2_VER ALL_VER = ID3V2_VER.ID3V22 | ID3V2_VER.ID3V23 | ID3V2_VER.ID3V24;

        private static readonly Regex GenreRegex = new Regex(@"^\((?<1>\d+)\)(?<2>.*)$");

        private static readonly Encoding[] ID32Encodings = new Encoding[] { 
			Encoding.Default, // ほんとはASCIIにするところなんだけど
			Encoding.Unicode, 
			Encoding.BigEndianUnicode, 
			Encoding.UTF8,
		};

        #region ID3V2 FRAMES
        public static readonly FrameID[] FRAMES = new FrameID[] {
	        new FrameID("","","","?","",FRAME_TYPE.FR_BIN,ALL_VER), /* どれでもない */
            //new FrameID("CRA","AENC","AENC","","暗号化",FRAME_TYPE.FR_BIN,ALL_VER),
	        new FrameID("PIC","APIC","APIC","COVER ART","画像",FRAME_TYPE.FR_APIC,ALL_VER),
            //new FrameID(""   ,""	,"ASPI","","オーディオシーケンスポイントインデックス",FRAME_TYPE.FR_BIN,ID3V24),
	        new FrameID("COM","COMM","COMM","COMMENT","コメント",FRAME_TYPE.FR_TXT_EX_LNG,ALL_VER),
            //new FrameID(""   ,"COMR","COMR","","コマーシャルフレーム",FRAME_TYPE.FR_BIN,ID3V23|ID3V24),
            //new FrameID(""   ,"ENCR","ENCR","","暗号化の手法の登録",FRAME_TYPE.FR_BIN,ID3V23|ID3V24),
            //new FrameID("EQU","EQUA","EQU2","均一化",FRAME_TYPE.FR_BIN,ALL_VER),
            //new FrameID("ETC","ETCO","ETCO","イベントタイムコード",FRAME_TYPE.FR_BIN,ALL_VER),
            //new FrameID("GEO","GEOB","GEOB","パッケージ化された一般的なオブジェクト",FRAME_TYPE.FR_BIN,ALL_VER),
            //new FrameID(""   ,"GRID","GRID","グループ識別子の登録",FRAME_TYPE.FR_BIN,ID3V23|ID3V24),
            //new FrameID("IPL","IPLS","TIPL","協力者",FRAME_TYPE.FR_TXT,ALL_VER), //
            //new FrameID("LINK","LINK","LINK","リンク情報",FRAME_TYPE.FR_TXT_EX,ALL_VER), //
            //new FrameID("MCI","MCDI","MCDI","音楽CD識別子",FRAME_TYPE.FR_BIN,ALL_VER),
            //new FrameID("MLL","MLLT","MLLT","MPEGロケーションルックアップテーブル",FRAME_TYPE.FR_BIN,ALL_VER),

            //new FrameID(""   ,"PRIV","PRIV","プライベートフレーム",FRAME_TYPE.FR_BIN_EX,ID3V23|ID3V24),
            //new FrameID(""   ,"GRID","GRID","グループ識別子の登録",FRAME_TYPE.FR_BIN,ID3V23|ID3V24),
	        new FrameID("TAL","TALB","TALB","ALBUM","アルバム",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TBP","TBPM","TBPM","BPM",FRAME_TYPE.FR_TXT,ALL_VER),
	        new FrameID("TCM","TCOM","TCOM","COMPOSER","作曲者(Composer)",FRAME_TYPE.FR_TXT,ALL_VER),
            new FrameID("TXT","TEXT","TEXT","TEXT","作詞家/文書作成者(TEXT)",FRAME_TYPE.FR_TXT,ALL_VER),//
	        new FrameID("TCO","TCON","TCON","GENRE","ジャンル",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TCR","TCOP","TCOP","著作権情報",FRAME_TYPE.FR_TXT,ALL_VER),
	        new FrameID("TDA","TDAT","TDRC","DATE","日付(録音)",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID(""   ,""	,"TDEN","日付(エンコード)",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID("TOR","TORY","TDOR","日付(オリジナルリリース)",FRAME_TYPE.FR_TXT,ALL_VER), //TORYとTDORですが統合してしまいます
            //new FrameID(""   ,""	,"TDRL","日付(リリース)",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID(""   ,""	,"TDTG","日付(タグ付け)",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID("TEN","TENC","TENC","エンコードした人(Encoder)",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TFL","TFLT","TFLT","ファイルタイプ",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID(""   ,""	,"TIPL","関わった人々の一覧",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID("TIM","TIME",""	,"時間(V2.4では日付(録音)の方へ)",FRAME_TYPE.FR_TXT,ID3V22|ID3V23),
            new FrameID("TT1","TIT1","TIT1","GROUP","内容の属するグループ",FRAME_TYPE.FR_TXT,ALL_VER),
	        new FrameID("TT2","TIT2","TIT2","TITLE","タイトル",FRAME_TYPE.FR_TXT,ALL_VER),
            new FrameID("TT3","TIT3","TIT3","SUBTITLE","サブタイトル",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TKE","TKEY","TKEY","始めの調",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TLA","TLAN","TLAN","言語",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TLE","TLEN","TLEN","長さ",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID(""   ,""	,"TMCL","ミュージシャンクレジットリスト",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID("TMT","TMED","TMED","メディアタイプ",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID(""   ,""	,"TMOO","ムード",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID("TOT","TOAL","TOAL","オリジナルのアルバム/映画/ショーのタイトル",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TOF","TOFN","TOFN","オリジナルファイル名",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TOL","TOLY","TOLY","オリジナルの作詞家/文書作成者",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID(""   ,"TOWN","TOWN","ファイルの所有者/ライセンシー",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
            new FrameID("TP1","TPE1","TPE1","ARTIST","アーティスト",FRAME_TYPE.FR_TXT,ALL_VER),
            new FrameID("TP2","TPE2","TPE2","ALBUM ARTIST","バンド/オーケストラ/伴奏",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TP3","TPE3","TPE3","指揮者/演奏者詳細情報",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TP4","TPE4","TPE4","翻訳者/リミックス/その他の修正",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TPA","TPOS","TPOS","セット中の位置",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID(""   ,""	,"TPRO","Produced notice",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TPB","TPUB","TPUB","出版者",FRAME_TYPE.FR_TXT,ALL_VER),
            new FrameID("TRK","TRCK","TRCK","TRACK","トラックの番号/セット中の位置",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TRD","TRDA",""	,"録音日時",FRAME_TYPE.FR_TXT,ID3V22|ID3V23), //とりあえず
            //new FrameID(""   ,"TRSN","TRSN","インターネットラジオ局の名前",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
            //new FrameID(""   ,"TRSO","TRSO","インターネットラジオ局の所有者",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
            //new FrameID(""   ,""	,"TSOA","アルバムのソートオーダー",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID(""   ,""	,"TSOP","演奏者のソートオーダー",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID(""   ,""	,"TSOT","タイトルのソートオーダー",FRAME_TYPE.FR_TXT,ID3V24),
            //new FrameID("TSI","TSIZ",""	,"サイズ",FRAME_TYPE.FR_TXT,ID3V22|ID3V23),
            //new FrameID("TRC","TSRC","TSRC","ISRC",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID("TSS","TSSE","TSSE","エンコードに使用したソフトウェア/ハードウェアとセッティング",FRAME_TYPE.FR_TXT,ALL_VER),
            //new FrameID(""   ,""	,"TSST","セットのサブタイトル",FRAME_TYPE.FR_TXT,ID3V24),
            new FrameID("TYE","TYER",""	,"DATE","年",FRAME_TYPE.FR_TXT,ID3V2_VER.ID3V22|ID3V2_VER.ID3V23),
            //new FrameID(""   ,"TCMP","TCMP","コンピレーション(iTunes)",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
            new FrameID("TXX","TXXX","TXXX","","ユーザー定義文字情報フレーム",FRAME_TYPE.FR_TXT_EX,ALL_VER),
            //new FrameID("UFI","UFID","UFID","一意的なファイル識別子",FRAME_TYPE.FR_BIN_EX,ALL_VER),
            //new FrameID(""   ,"USER","USER","使用条件",FRAME_TYPE.FR_TXT,ID3V23|ID3V24),
            new FrameID("ULT","USLT","USLT","LYRICS","非同期歌詞/文書のコピー",FRAME_TYPE.FR_TXT_EX_LNG,ALL_VER),
            //new FrameID("WCM","WCOM","WCOM","商業上の情報",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
            //new FrameID("WCP","WCOP","WCOP","著作権/法的情報",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
            //new FrameID("WAF","WOAF","WOAF","オーディオファイルの公式Webページ",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
            //new FrameID("WAR","WOAR","WOAR","アーティスト/演奏者の公式Webページ",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
            //new FrameID("WAS","WOAS","WOAS","音源の公式Webページ",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
            //new FrameID(""   ,"WORS","WORS","インターネットラジオ局の公式Webページ",FRAME_TYPE.FR_TXT_PURE,ID3V23|ID3V24),
            //new FrameID(""   ,"WPAY","WPAY","支払い",FRAME_TYPE.FR_TXT_PURE,ID3V23|ID3V24),
            //new FrameID("WPB","WPUB","WPUB","出版者の公式Webページ",FRAME_TYPE.FR_TXT_PURE,ALL_VER),
            //new FrameID("WXX","WXXX","WXXX","出版者の公式Webページ",FRAME_TYPE.FR_TXT_EX,ALL_VER),
            //new FrameID("CRM",""	,""	,"暗号化メタフレーム",FRAME_TYPE.FR_BIN,ID3V22),
            //new FrameID("TDY","TDLY","TDLY","プレイリスト遅延時間",FRAME_TYPE.FR_TXT,ALL_VER),
        };
        #endregion
        #endregion

        /// <summary>
        /// ID3v2タグを読む
        /// </summary>
        /// <param name="strm"></param>
        /// <param name="createImageObject"></param>
        /// <returns></returns>
        public static ID3tag readID3tag(System.IO.Stream strm, bool createImageObject)
        {
            // ヘッダ読み込み
            byte[] id3_header = strm.ReadBytes(HEADER_LEN);
            Header header = readHeader(id3_header);
            if (header == null) return null;
            if (header.Size >= strm.Length) return null;
            ID3tag tag = new ID3tag();
            tag.head = header;

            // 拡張ヘッダがあるときサイズだけ読んでスキップ
            if (tag.head.Flag.HasFlag(HEADER_FLAG.EXTENSION))
            {
                byte[] ext_header = strm.ReadBytes(EXT_HEADER_LEN);
                var size = tag.head.Version == ID3V2_VER.ID3V23
                    ? ReadUInt32(ext_header, 0)
                    : ReadUInt28(ext_header, 0) - EXT_HEADER_LEN; // v3とv4でEXT_HEADER_LENの扱いが違う
                strm.Seek(size, System.IO.SeekOrigin.Current);
            }

            // 全Frameのデータ領域を読み出す
            byte[] frame_buf = strm.ReadBytes(header.Size);

            // .2, .3の場合は非同期化の解除
            if (header.Version != ID3V2_VER.ID3V24 && header.Flag.HasFlag(HEADER_FLAG.UNSYNC))
            {
                decodeUnsync(ref frame_buf);
            }

            // 各Frameのデータ読み出し
            var tagBodyStream = new MemoryStream(frame_buf, 0, frame_buf.Length);
            while (true)
            {
                try
                {
                    Frame fr = readFrame(tagBodyStream, tag, createImageObject);
                    if (fr != null) tag.frame.Add(fr);
                }
                catch (EndOfTagException)
                {
                    break;
                }
            }
            return tag;
        }

        /// <summary>
        /// ID3V2タグヘッダを解析
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private static Header readHeader(byte[] buffer)
        {
            Header header = new Header();
            if (buffer[0] != 'I' || buffer[1] != 'D' || buffer[2] != '3') return null;

            switch ((buffer[3] << 8) + buffer[4])
            {
                case 0x0200:
                    header.Version = ID3V2_VER.ID3V22;
                    header.Flag = (HEADER_FLAG)(buffer[5] & HEADER_FLAG_MASK_2);
                    if (header.Flag.HasFlag(HEADER_FLAG.COMPRESSION)) return null;
                    break;
                case 0x0300:
                    header.Version = ID3V2_VER.ID3V23;
                    header.Flag = (HEADER_FLAG)(buffer[5] & HEADER_FLAG_MASK_3);
                    break;
                case 0x0301:
                case 0x0400:
                    header.Version = ID3V2_VER.ID3V24;
                    header.Flag = (HEADER_FLAG)(buffer[5] & HEADER_FLAG_MASK_4);
                    break;
                default:
                    return null;
            }
            header.Size = ReadUInt28(buffer, 6);

            return header;
        }

        /// <summary>
        /// ID3V2タグフレームを読む
        /// </summary>
        /// <param name="strm"></param>
        /// <param name="tag"></param>
        /// <param name="createImageObject"></param>
        /// <returns></returns>
        private static Frame readFrame(Stream strm, ID3tag tag, bool createImageObject)
        {
            int frameHeaderSize = (tag.head.Version == ID3V2_VER.ID3V22) ? FRAME_HEADER_LEN_2 : FRAME_HEADER_LEN_34;
            if (strm.Length-strm.Position < frameHeaderSize) throw new EndOfTagException();
            byte[] buf = strm.ReadBytes(frameHeaderSize);
            var frame = readFrameHeader(buf, tag.head.Version);

            if (frame.Size <= 0) throw new EndOfTagException();
            if (strm.Length - strm.Position < frame.Size) throw new EndOfTagException();

            int offset = 0;
            bool unsupported = false;
            if (frame.ID == null)
            {
                unsupported = true;
            }

            // 暗号化なんて知りませんよっと
            if (frame.Flag.HasFlag(FRAME_FLAG.CRYPTED))
            {
                unsupported = true;
            }

            // 圧縮は実装してない
            if (frame.Flag.HasFlag(FRAME_FLAG.COMPRESSED))
            {
                unsupported = true;
            }

            // グループ識別子を無視
            if (frame.Flag.HasFlag(FRAME_FLAG.GROUPED))
            {
                // unsupportedにはしない
                offset += 1;
            }

            // DATALENGTHを飛ばす
            if (frame.Flag.HasFlag(FRAME_FLAG.DATALENGTH))  
            {
                offset += 4;
            }

            if (unsupported)
            {
                strm.Seek(frame.Size, SeekOrigin.Current);
                return null;
            }

            strm.Seek(offset, SeekOrigin.Current);
            byte[] buffer = strm.ReadBytes(frame.Size - offset);
            if (frame.Flag.HasFlag(FRAME_FLAG.UNSYNC) && tag.head.Version == ID3V2_VER.ID3V24)
            {
                decodeUnsync(ref buffer);
            }

            MemoryStream frameBodyStream = new MemoryStream(buffer);
            readFrameBody(frameBodyStream, frame, createImageObject);
            return frame;
        }

        /// <summary>
        /// ID3V2フレームヘッダを解析
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        private static Frame readFrameHeader(byte[] buffer, ID3V2_VER version)
        {
            if (buffer[0] == 0) throw new EndOfTagException();
            FrameID fid;
            int size;
            FRAME_FLAG flag = 0;
            switch (version)
            {
                case ID3V2_VER.ID3V22:
                    fid = FRAMES.FirstOrDefault(_ => _.Name2 == Encoding.ASCII.GetString(buffer, 0, 3));
                    size = (int)ReadUInt24(buffer, 3);
                    break;
                case ID3V2_VER.ID3V23:
                    fid = FRAMES.FirstOrDefault(_ => _.Name3 == Encoding.ASCII.GetString(buffer, 0, 4));
                    size = (int)ReadUInt32(buffer, 4);
                    flag = (FRAME_FLAG)(((uint)buffer[8] << 7) + (((uint)buffer[9] >> 4) & 0x0F) + (((uint)buffer[9] << 1) & 0x40));
                    break;
                case ID3V2_VER.ID3V24:
                    fid = FRAMES.FirstOrDefault(_ => _.Name4 == Encoding.ASCII.GetString(buffer, 0, 4));
                    size = (int)ReadUInt28(buffer, 4);
                    flag = (FRAME_FLAG)(((uint)buffer[8] << 8) + buffer[9]);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return new Frame() { ID = fid, Size = size, Flag = flag };
        }

        /// <summary>
        /// ID3V2フレームのデータを読む
        /// </summary>
        /// <param name="sr"></param>
        /// <param name="fr"></param>
        /// <param name="createImageObject"></param>
        private static void readFrameBody(Stream sr, Frame fr, bool createImageObject)
        {
            if (fr.ID.Type == FRAME_TYPE.FR_APIC)
            {
                if (!createImageObject)
                {
                    sr.Seek(sr.Length, SeekOrigin.Current);
                    return;
                }
                var tmp = sr.ReadBytes((int)sr.Length);
                int offset = 0;
                string imgtype = Encoding.ASCII.GetString(tmp, 1, 3);
                if ((imgtype == "JPG") || (imgtype == "PNG"))
                {
                    fr.Picture_type = tmp[4];
                    offset = tmp.IndexOf(5, 0) + 1;
                }
                else // mime
                {
                    int pictureTypeIndex = tmp.IndexOf(1, 0) + 1;
                    fr.Picture_type = tmp[pictureTypeIndex];
                    offset = tmp.IndexOf(pictureTypeIndex + 1, 0) + 1;
                }
                MemoryStream ms = new MemoryStream(tmp, offset, tmp.Length - offset);
                try
                {
                    fr.Image = System.Drawing.Image.FromStream(ms);
                }
                catch { }
            }
            else
            {
                var tmp = sr.ReadBytes((int)sr.Length);
                Encoding enc = ID32Encodings[0]; // default
                int offset = 0;
                if (tmp[0] < ID32Encodings.Length)
                {
                    offset += 1;
                    if (fr.ID.Type == FRAME_TYPE.FR_TXT_EX_LNG) offset += 3;
                    enc = ID32Encodings[tmp[0]];
                }
                fr.Value = enc.GetString(tmp, offset, tmp.Length - offset).Trim().TrimEnd(new char[] { '\0' });
                //genreのとき
                if (fr.ID.Name4 == "TCON")
                {
                    var match = GenreRegex.Match(fr.Value);
                    if (match.Success)
                    {
                        fr.Value = ID3.GetGenreString(int.Parse(match.Groups[1].Value)) ?? match.Groups[2].Value;
                    }
                }

                // TXXX,COMMなどのとき
                if (fr.ID.Type == FRAME_TYPE.FR_TXT_EX || fr.ID.Type == FRAME_TYPE.FR_TXT_EX_LNG)
                {
                    int idx = fr.Value.IndexOf('\0');
                    if (idx == -1)
                    {
                        fr.Extid = "";
                        fr.Value = "";
                    }
                    else
                    {
                        fr.Extid = fr.Value.Substring(0, idx).ToUpper();
                        fr.Value = fr.Value.Substring(idx + 1).TrimEnd(new char[] { '\0' }).Replace("\0", "\r\n"); // FIXME?: foobar2kでいうmultiple Valueの時、区切りの\0を改行で置換（暫定処置）
                    }
                }
            }
        }

        /// <summary>
        /// 非同期化を解除する
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        static void decodeUnsync(ref byte[] src)
        {
            int pos = 0;
            bool lastByteIsEscaped = (src[src.Length - 2] == 0xFF) && (src[src.Length - 1] == 0x00);
            for (var i = 0; i < src.Length - 1; i++, pos++)
            {
                src[pos] = src[i];
                if ((src[i] == 0xFF) && (src[i + 1] == 0x00)) { i++; }
            }
            if (!lastByteIsEscaped)
            {
                src[pos++] = src[src.Length - 1]; // 最後の1バイトはここでコピー
            }
            Array.Resize(ref src, pos);
        }

        static int ReadUInt24(byte[] buffer, int offset)
        {
            return (buffer[offset] << 16) + (buffer[offset + 1] << 8) + buffer[offset + 2];
        }

        static int ReadUInt32(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24) + (buffer[offset + 1] << 16) + (buffer[offset + 2] << 8) + buffer[offset + 3];
        }

        static int ReadUInt28(byte[] buffer, int offset)
        {
            return (buffer[offset] << 21) + (buffer[offset + 1] << 14) + (buffer[offset + 2] << 7) + buffer[offset + 3];
        }
    }
}
