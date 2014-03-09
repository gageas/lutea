using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using Gageas.Lutea.Library;
using Gageas.Wrapper.BASS;

namespace Gageas.Lutea.Core
{
    /// <summary>
    /// 入力(デコード側)ストリームクラス
    /// ファイルに直接対応する低レベルストリーム(LowLevelStream)をラップし，
    /// CUEやギャップレス情報のオフセット・レングスをここで吸収する
    /// </summary>
    class DecodeStream : IDisposable
    {
        #region フィールド・プロパティ
        /// <summary>
        /// 低レベルストリーム
        /// </summary>
        private BASS.Stream LowLevelStream; 

        /// <summary>
        /// ストリーム範囲の先頭(Byte)
        /// </summary>
        private ulong RangeOffset = 0;

        /// <summary>
        /// ストリーム範囲の長さ(Byte)
        /// </summary>
        private ulong RangeLength = 0;

        /// <summary>
        /// シーク時にストリーム範囲情報を破棄するかどうか
        /// </summary>
        private bool InvalidateRangeLengthOnSeek = false;

        /// <summary>
        /// PreScan有効でオープンしたストリームかどうか
        /// </summary>
        private bool IsPreScaned;

        /// <summary>
        /// メタデータ
        /// </summary>
        public object[] meta;

        /// <summary>
        /// リプレイゲイン
        /// </summary>
        public double? gain;

        /// <summary>
        /// 利用可能状態かどうか
        /// </summary>
        public bool Ready = false;

        /// <summary>
        /// 再生カウントの更新済みかどうか
        /// </summary>
        public bool PlaybackCounterUpdated = false;

        /// <summary>
        /// 浮動小数点(Float)型で出力する設定で作成されたかどうか
        /// </summary>
        public bool IsFloat
        {
            get;
            private set;
        }

        /// <summary>
        /// サンプルのサイズ(Byte)
        /// </summary>
        public uint SampleSize
        {
            get
            {
                return (uint)(IsFloat ? sizeof(float) : sizeof(Int16));
            }
        }

        /// <summary>
        /// 位置の取得または設定(Byte)
        /// </summary>
        public ulong PositionByte
        {
            get
            {
                return LowLevelStream.position - RangeOffset;
            }
            set
            {
                if (InvalidateRangeLengthOnSeek)
                {
                    RangeOffset = 0;
                    RangeLength = 0;
                }
                LowLevelStream.position = value + RangeOffset;
            }
        }

        /// <summary>
        /// 位置の取得または設定(秒)
        /// </summary>
        public double PositionSec
        {
            get
            {
                return LowLevelStream.Bytes2Seconds(PositionByte);
            }
            set
            {
                PositionByte = LowLevelStream.Seconds2Bytes(value);
            }
        }

        /// <summary>
        /// 長さの取得(Byte)
        /// </summary>
        public ulong LengthByte
        {
            get
            {
                return RangeLength > 0
                    ? RangeLength
                    : LowLevelStream.filesize;
            }
        }

        /// <summary>
        /// 長さの取得(秒)
        /// </summary>
        public double LengthSec
        {
            get
            {
                return LowLevelStream.Bytes2Seconds(LengthByte);
            }
        }

        /// <summary>
        /// 周波数の取得
        /// </summary>
        public uint Freq
        {
            get
            {
                return LowLevelStream.GetFreq();
            }
        }

        /// <summary>
        /// チャンネル数の取得
        /// </summary>
        public uint Chans
        {
            get
            {
                return LowLevelStream.GetChans();
            }
        }

        /// <summary>
        /// ファイル名の取得
        /// </summary>
        public string FileName
        {
            get;
            private set;
        }

        /// <summary>
        /// 実体ストリームのファイル名
        /// FileNameと同一の場合はnull
        /// </summary>
        public string CueStreamFileName
        {
            get;
            private set;
        }
        #endregion

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="lowLevelStream">低レベルストリーム</param>
        /// <param name="fileName">ファイル名</param>
        private DecodeStream(BASS.Stream lowLevelStream, string fileName)
        {
            this.LowLevelStream = lowLevelStream;
            this.FileName = fileName;  // DBのfile_nameをそのまま入れる。.cueの場合あり
        }

        /// <summary>
        /// デストラクタ
        /// </summary>
        ~DecodeStream()
        {
            Dispose();
        }

        /// <summary>
        /// IDisposableの実装
        /// </summary>
        public void Dispose()
        {
            if (LowLevelStream != null)
            {
                LowLevelStream.Dispose();
                LowLevelStream = null;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// CDのフレームからサンプル数に変換する
        /// </summary>
        /// <param name="frames">フレーム数</param>
        /// <returns>サンプル数</returns>
        private ulong GetFrame2Sample(int frames)
        {
            return (ulong)frames * (LowLevelStream.GetFreq() / 75) * LowLevelStream.GetChans();
        }

        /// <summary>
        /// 再生位置にコールバックを設定(Byte)
        /// </summary>
        /// <param name="callback">コールバック</param>
        /// <param name="bytes">位置(Byte)</param>
        public void SetSyncByte(Action<object> callback, ulong bytes)
        {
            LowLevelStream.setSync(BASS.SYNC_TYPE.POS, callback, bytes);
        }

        /// <summary>
        /// 再生位置にコールバックを設定(秒)
        /// </summary>
        /// <param name="callback">コールバック</param>
        /// <param name="sec">位置(秒)</param>
        public void SetSyncSec(Action<object> callback, double sec)
        {
            SetSyncByte(callback, LowLevelStream.Seconds2Bytes(sec));
        }

        /// <summary>
        /// デコードデータを取得
        /// </summary>
        /// <param name="buffer">バッファポインタ</param>
        /// <param name="length">要求データ長(Byte)</param>
        /// <returns>出力したデータ長(Byte)</returns>
        public uint GetData(IntPtr buffer, uint length)
        {
            if (!Ready) return 0;
            if (LowLevelStream == null) return 0;
            if (length + PositionByte > LengthByte)
            {
                length = (uint)(LengthByte - PositionByte);
            }
            return LowLevelStream.GetData(buffer, length);
        }

        /// <summary>
        /// 再生開始位置のオフセットまでシークする
        /// 再生開始前に一度だけ呼ぶ
        /// </summary>
        private void SeekToInitialPos()
        {
            if (RangeOffset < 30000)
            {
                ulong left = RangeOffset;
                if (left > 0)
                {
                    var mem = Marshal.AllocHGlobal(1000);
                    while (left > 0)
                    {
                        int toread = (int)Math.Min(1000, left);
                        LowLevelStream.GetData(mem, (uint)toread);
                        left -= (uint)toread;
                    }
                    Marshal.FreeHGlobal(mem);
                }
            }
            else
            {
                LowLevelStream.position = RangeOffset;
            }
        }

        /// <summary>
        /// ギャップレス再生のための正確なオフセットとレングス情報を設定する
        /// ライブラリ側のデコーダで既に補正されていると思われる場合は設定を行わない
        /// </summary>
        /// <param name="offset">Sample number</param>
        /// <param name="length">Sample number</param>
        private void AdjustIfNeeded(ulong offset, ulong length)
        {
            ulong sampleSize = LowLevelStream.GetChans() * SampleSize;
            if (LowLevelStream.filesize > length * sampleSize)
            {
                RangeOffset = offset * sampleSize;
                RangeLength = length * sampleSize;
                if (!IsPreScaned)
                {
                    InvalidateRangeLengthOnSeek = true;
                }
            }
        }

        /// <summary>
        /// 正確なオフセットとレングス情報を取得して補正値に設定する
        /// </summary>
        /// <param name="tag"></param>
        private void RetrieveAccurateRange(List<KeyValuePair<string, object>> tag)
        {
            KeyValuePair<string, object> iTunSMPB = tag.Find((match) => match.Key.ToUpper() == "ITUNSMPB");
            if (iTunSMPB.Value != null)
            {
                var smpbs = iTunSMPB.Value.ToString().Trim().Split(new char[] { ' ' }).Select(_ => System.Convert.ToUInt64(_, 16)).ToArray();
                // ref. http://nyaochi.sakura.ne.jp/archives/2006/09/15/itunes-v70070%E3%81%AE%E3%82%AE%E3%83%A3%E3%83%83%E3%83%97%E3%83%AC%E3%82%B9%E5%87%A6%E7%90%86/
                AdjustIfNeeded((smpbs[1] + smpbs[2]), (smpbs[3]));
            }
            else
            {
                var lametag = Lametag.Read(FileName);
                if (lametag != null)
                {
                    AdjustIfNeeded((ulong)(lametag.delay), LowLevelStream.filesize - (ulong)(lametag.delay + lametag.padding));
                }
            }
        }

        #region ファクトリ(private)
        /// <summary>
        /// BASSのStreamFlagを生成する
        /// </summary>
        /// <param name="floatingPoint">float出力かどうか</param>
        /// <param name="preScan">preScanをするかどうか</param>
        /// <returns>BASS.Stream.StreamFlag</returns>
        private static BASS.Stream.StreamFlag MakeFlag(bool floatingPoint, bool preScan)
        {
            BASS.Stream.StreamFlag flag = BASS.Stream.StreamFlag.BASS_STREAM_DECODE | BASS.Stream.StreamFlag.BASS_STREAM_ASYNCFILE;
            if (floatingPoint) flag |= BASS.Stream.StreamFlag.BASS_STREAM_FLOAT;
            if (preScan) flag |= BASS.Stream.StreamFlag.BASS_STREAM_PRESCAN;
            return flag;
        }

        /// <summary>
        /// 通常ファイルまたは埋め込みCUEのデコードストリームを生成
        /// </summary>
        private static DecodeStream CreateStreamRegular(string filename, int tracknumber, bool floatingPoint, bool preScan, List<KeyValuePair<string, object>> tag)
        {
            BASS.Stream llStream = new BASS.FileStream(filename, MakeFlag(floatingPoint, preScan));
            if (llStream == null) return null;
            if (tag == null)
            {
                tag = Tags.MetaTag.readTagByFilename(filename, false);
            }
            KeyValuePair<string, object> cue = tag.Find((match) => match.Key == "CUESHEET");

            // case for Internal CUESheet
            if (cue.Key != null)
            {
                CD cd = CUEReader.ReadFromString(cue.Value.ToString(), filename, false);
                var nextStream = CreateStreamCue(cd.tracks[tracknumber - 1], floatingPoint, preScan, llStream);
                if (nextStream == null) return null;
                nextStream.CueStreamFileName = filename;
                return nextStream;
            }
            else
            {
                var nextStream = new DecodeStream(llStream, filename);
                nextStream.IsFloat = floatingPoint;
                nextStream.IsPreScaned = preScan;
                KeyValuePair<string, object> gain = tag.Find((match) => match.Key == "REPLAYGAIN_ALBUM_GAIN");
                if (gain.Value != null)
                {
                    nextStream.gain = Util.Util.parseDouble(gain.Value.ToString());
                }
                nextStream.RetrieveAccurateRange(tag);
                return nextStream;
            }
        }

        /// <summary>
        /// CUEシートのTrack情報からストリームを生成
        /// </summary>
        /// <param name="track">CUEのTrack情報</param>
        /// <param name="floatingPoint">float出力かどうか</param>
        /// <param name="preScan">preScanを行うかどうか</param>
        /// <param name="lowLevelStream">オープン済みの低レベルストリーム(InCUE)の場合，またはNULL(.CUEファイル)</param>
        /// <returns></returns>
        private static DecodeStream CreateStreamCue(CD.Track track, bool floatingPoint, bool preScan, BASS.Stream lowLevelStream = null)
        {
            String streamFullPath = System.IO.Path.IsPathRooted(track.file_name_CUESheet)
                ? track.file_name_CUESheet
                : Path.GetDirectoryName(track.file_name) + Path.DirectorySeparatorChar + track.file_name_CUESheet;
            if (lowLevelStream == null)
            {
                lowLevelStream = new BASS.FileStream(streamFullPath, MakeFlag(floatingPoint, preScan));
            }
            if (lowLevelStream == null) return null;

            DecodeStream nextStream = new DecodeStream(lowLevelStream, track.file_name);
            nextStream.IsFloat = floatingPoint;
            nextStream.IsPreScaned = preScan;
            nextStream.RangeOffset = nextStream.GetFrame2Sample(track.Start) * nextStream.SampleSize;
            nextStream.RangeLength = track.End > track.Start
                ? nextStream.GetFrame2Sample(track.End - track.Start) * nextStream.SampleSize
                : lowLevelStream.filesize - nextStream.RangeOffset;
            nextStream.CueStreamFileName = streamFullPath;

            var gain = track.getTagValue("ALBUM GAIN");
            if (gain != null)
            {
                nextStream.gain = Util.Util.parseDouble(gain.ToString());
            }
            return nextStream;
        }
        #endregion

        #region ファクトリ(public)
        /// <summary>
        /// デコードストリームを生成する
        /// </summary>
        /// <param name="filename">ファイル名(データベースのfile_name)</param>
        /// <param name="tracknumber">トラック番号</param>
        /// <param name="floatingPoint">FloatingPointかどうか</param>
        /// <param name="preScan">PreScanを行うかどうか</param>
        /// <param name="tag">タグ情報(あれば)</param>
        /// <returns>デコードストリーム</returns>
        public static DecodeStream CreateStream(string filename, int tracknumber, bool floatingPoint, bool preScan, List<KeyValuePair<string, object>> tag = null)
        {
            Logger.Log(String.Format("Trying to play file {0}", filename));

            filename = filename.Trim();

            DecodeStream nextStream;
            if (Path.GetExtension(filename).ToUpper() == ".CUE")
            {
                // case for CUE sheet
                CD cd = CUEReader.ReadFromFile(filename, false);
                nextStream = CreateStreamCue(cd.tracks[tracknumber - 1], floatingPoint, preScan);
            }
            else
            {
                // case for Regular or Internal CUE sheet
                nextStream = CreateStreamRegular(filename, tracknumber, floatingPoint, preScan, tag);
            }
            if (nextStream == null) return null;

            nextStream.SeekToInitialPos();
            return nextStream;
        }

        /// <summary>
        /// 再生停止を要求するためのダミーストリームオブジェクトを生成する
        /// やりかたがきたないけどこれだけなので勘弁
        /// </summary>
        /// <returns>再生停止を要求するダミーストリーム。FileNameが":STOP:"になっている</returns>
        public static DecodeStream CreateStopRequestStream()
        {
            return new DecodeStream(null, @":STOP:");
        }
        #endregion
    }
}
