using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.OutputDevice
{
    static class OutputDevice
    {
        /// <summary>
        /// ストリームプロシージャのデリゲート
        /// </summary>
        /// <param name="bffer">バッファへのポインタ</param>
        /// <param name="length">バッファ長</param>
        /// <returns>バッファへ出力したデータ長</returns>
        public delegate UInt32 StreamProc(IntPtr bffer, UInt32 length);
    }
}
