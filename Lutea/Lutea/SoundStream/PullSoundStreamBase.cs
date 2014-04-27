using System;

namespace Gageas.Lutea.SoundStream
{
    /// <summary>
    /// 二次側からの要求(pull)をベースに駆動する音声ストリームの基底クラス
    /// サンプルのデータ型は32bit-float固定
    /// </summary>
    abstract class PullSoundStreamBase : IDisposable
    {
        /// <summary>
        /// ストリームの元となった場所を示す文字列。ファイル名,URL。
        /// </summary>
        public abstract string Location { get; }

        /// <summary>
        /// 出力側が再生時に適用すべきリプレイゲイン。nullの場合は指定なし。
        /// </summary>
        public abstract double? ReplayGain { get; }

        /// <summary>
        /// チャンネル数
        /// </summary>
        public abstract uint Chans { get; }

        /// <summary>
        /// サンプリング周波数
        /// </summary>
        public abstract uint Freq { get; }

        /// <summary>
        /// PCMデータ取得(pull)
        /// 出力データ形式はfloat型で,ステレオの場合はLRLRLR...のインタリーブ
        /// </summary>
        /// <param name="buffer">出力先バッファ</param>
        /// <param name="length">要求データ長(サンプル数)</param>
        /// <returns>出力データ長(サンプル数)</returns>
        public abstract uint GetData(IntPtr buffer, uint length);

        /// <summary>
        /// ストリーム長(サンプル数)
        /// </summary>
        public abstract ulong LengthSample { get; }

        /// <summary>
        /// 現在位置(サンプル数)
        /// </summary>
        public abstract ulong PositionSample { get; set; }

        /// <summary>
        /// ストリーム長(秒)
        /// </summary>
        public double LengthSec
        {
            get
            {
                return LengthSample / ((double)Freq);
            }
        }

        /// <summary>
        /// 現在位置(秒)
        /// </summary>
        public double PositionSec
        {
            get
            {
                return PositionSample / ((double)Freq);
            }
            set
            {
                PositionSample = (ulong)(value * Freq);
            }
        }

        /// <summary>
        /// サンプルあたりのバイト数
        /// </summary>
        public uint SampleBytes
        {
            get
            {
                return sizeof(float) * Chans;
            }
        }

        /// <summary>
        /// IDisposableの実装
        /// </summary>
        public abstract void Dispose();
    }
}