using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.DefaultUI
{
    class GDI
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "MoveToEx")]
        public static extern bool MoveToEx(IntPtr hDC, int x, int y, IntPtr lpPoint);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "LineTo")]
        public static extern bool LineTo(IntPtr hDC, int xEnd, int yEnd);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetTextExtentPoint32")]
        public static extern bool GetTextExtentPoint32(IntPtr hDC, String str, int length, out Size sz);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetStockObject")]
        public static extern IntPtr GetStockObject(int id);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SelectObject")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGDIOBJ);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "DeleteObject")]
        public static extern bool DeleteObject(IntPtr hGDIOBJ);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetTextColor")]
        public static extern uint SetTextColor(IntPtr hDC, uint COLORREF);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "TextOutW")]
        public static extern bool TextOut(IntPtr hDC, int nXStart, int nYStart, string str, int length);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "BitBlt")]
        public static extern bool BitBlt(
            IntPtr hdcDest,    // コピー先デバイスコンテキスト
            int nXDest,     // コピー先x座標
            int nYDest,     // コピー先y座標
            int nWidth,     // コピーする幅
            int nHeight,    // コピーする高さ
            IntPtr hdcSource,  // コピー元デバイスコンテキスト
            int nXSource,   // コピー元x座標
            int nYSource,   // コピー元y座標
            uint dwRaster    // ラスタオペレーションコード
        );

        public const int WHITE_PEN = 6;
        public class GDIBitmap : IDisposable
        {
            public Image orig;
            private Bitmap bitmap;
            private Graphics g;
            private IntPtr hDC;
            public IntPtr HDC
            {
                get
                {
                    return hDC;
                }
            }
            private IntPtr hBMP;
            public GDIBitmap(Bitmap bitmap)
            {
                this.orig = bitmap;
                this.bitmap = new Bitmap(bitmap);
                using (var g = Graphics.FromImage(this.bitmap))
                {
                    g.DrawImage(bitmap, 0, 0);
                }
                //                this.bitmap = bitmap;
                this.g = Graphics.FromImage(this.bitmap);
                this.hDC = this.g.GetHdc();
                this.hBMP = this.bitmap.GetHbitmap();
                SelectObject(this.hDC, this.hBMP);
            }
            public void Dispose()
            {
                DeleteObject(this.hBMP);
                g.ReleaseHdc();
                g.Dispose();
                bitmap.Dispose();
                GC.SuppressFinalize(this);
            }
            ~GDIBitmap(){
                this.Dispose();
            }
        }
    }
}