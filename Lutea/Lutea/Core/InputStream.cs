using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gageas.Lutea.SoundStream;

namespace Gageas.Lutea.Core
{
    class InputStream : AbstractFilter
    {
        /// <summary>
        /// イベント
        /// </summary>
        private SortedList<ulong, Action> Events = new SortedList<ulong, Action>();

        /// <summary>
        /// データベース上のファイル名
        /// </summary>
        public string DatabaseFileName
        {
            get { return meta[0].ToString(); }
        }

        /// <summary>
        /// 再生が終了したかどうか
        /// </summary>
        public bool Finished = false;

        /// <summary>
        /// 利用可能かどうか
        /// </summary>
        public bool Ready = false;

        /// <summary>
        /// 再生カウントの更新済みかどうか
        /// </summary>
        public bool PlaybackCounterUpdated = false;

        /// <summary>
        /// メタ情報
        /// </summary>
        public readonly object[] meta;

        protected InputStream()
            : base(null)
        {
        }

        public InputStream(PullSoundStreamBase audioStream, object[] metaDataRow)
            : base(audioStream)
        {
            this.meta = metaDataRow;
        }

        /// <summary>
        /// イベントを設定する
        /// </summary>
        /// <param name="callback">コールバックのデリゲート</param>
        /// <param name="sec">位置(秒)</param>
        public void SetEvent(Action callback, double sec)
        {
            Events.Add((ulong)(sec * Freq), callback);
        }

        /// <summary>
        /// デコード出力を取得
        /// </summary>
        /// <param name="buffer">出力先バッファ</param>
        /// <param name="length">要求サンプル数</param>
        /// <returns>出力したサンプル数</returns>
        public override uint GetData(IntPtr buffer, uint length)
        {
            if (!Ready) return 0;
            var ret = base.GetData(buffer, length);
            while (Events.Count > 0)
            {
                if (Input.PositionSample >= Events.First().Key)
                {
                    Events.First().Value();
                    Events.RemoveAt(0);
                }
                else
                {
                    break;
                }
            }
            return ret;
        }

        /// <summary>
        /// IDisposableの実装
        /// </summary>
        public override void Dispose()
        {
            Ready = false;
            base.Dispose();
        }
    }
}
