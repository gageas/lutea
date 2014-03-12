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

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "Rectangle")]
        public static extern bool Rectangle(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "Polygon")]
        private static extern bool Polygon(IntPtr hdc, Point[] points, int nCount);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetTextExtentPoint32")]
        public static extern bool GetTextExtentPoint32(IntPtr hDC, String str, int length, out Size sz);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetStockObject")]
        public static extern IntPtr GetStockObject(StockObjects id);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetDCPenColor")]
        public static extern UInt32 SetDCPenColor(IntPtr hdc, UInt32 crColor);

        // 引数のrgb値のチェックしてない
        public static UInt32 SetDCPenColor(IntPtr hdc, int r, int g, int b)
        {
            return SetDCPenColor(hdc, (UInt32)(r | g << 8 | b << 16));
        }

        public static UInt32 SetDCPenColor(IntPtr hdc, Color color)
        {
            return SetDCPenColor(hdc, (UInt32)(color.R | color.G << 8 | color.B << 16));
        }

        public static bool Polygon(IntPtr hdc, Point[] points)
        {
            return Polygon(hdc, points, points.Length);
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetDCBrushColor")]
        public static extern UInt32 SetDCBrushColor(IntPtr hdc, UInt32 crColor);

        public static UInt32 SetDCBrushColor(IntPtr hdc, Color color)
        {
            return SetDCBrushColor(hdc, (UInt32)(color.R | color.G << 8 | color.B << 16));
        }

        // 引数のrgb値のチェックしてない
        public static UInt32 SetDCBrushColor(IntPtr hdc, int r, int g, int b)
        {
            return SetDCBrushColor(hdc, (UInt32)(r | g << 8 | b << 16));
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SelectObject")]
        public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGDIOBJ);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "DeleteObject")]
        public static extern bool DeleteObject(IntPtr hGDIOBJ);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetTextColor")]
        public static extern uint SetTextColor(IntPtr hDC, UInt32 COLORREF);

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

        public enum BkMode : int
        {
            OPAQUE = 0,
            TRANSPARENT	= 1,
        }
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetBkMode")]
        public static extern int SetBkMode(IntPtr hDC, BkMode bkMode);

        public enum StockObjects
        {
            WHITE_BRUSH = 0,
            LTGRAY_BRUSH = 1,
            GRAY_BRUSH = 2,
            DKGRAY_BRUSH = 3,
            BLACK_BRUSH = 4,
            NULL_BRUSH = 5,
            HOLLOW_BRUSH = NULL_BRUSH,
            WHITE_PEN = 6,
            BLACK_PEN = 7,
            NULL_PEN = 8,
            OEM_FIXED_FONT = 10,
            ANSI_FIXED_FONT = 11,
            ANSI_VAR_FONT = 12,
            SYSTEM_FONT = 13,
            DEVICE_DEFAULT_FONT = 14,
            DEFAULT_PALETTE = 15,
            SYSTEM_FIXED_FONT = 16,
            DEFAULT_GUI_FONT = 17,
            DC_BRUSH = 18,
            DC_PEN = 19,
        }

        public class GDIBitmap : IDisposable
        {
            private Bitmap bitmap;
            private Graphics g;
            private IntPtr hDC;
            public readonly int Width;
            public readonly int Height;
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
                this.bitmap = bitmap;
                this.Width = bitmap.Width;
                this.Height = bitmap.Height;
                this.g = Graphics.FromImage(this.bitmap);
                this.hDC = this.g.GetHdc();
                this.hBMP = this.bitmap.GetHbitmap();
                SelectObject(this.hDC, this.hBMP);
            }
            public void Dispose()
            {
                if (this.hBMP != IntPtr.Zero)
                {
                    try
                    {
                        DeleteObject(this.hBMP);
                    }
                    catch { }
                    finally { this.hBMP = IntPtr.Zero; }
                }
                if (this.hDC != IntPtr.Zero)
                {
                    try
                    {
                        this.g.ReleaseHdc(this.hDC);
                    }
                    catch { }
                    finally { this.hDC = IntPtr.Zero; }
                }
                if (this.g != null)
                {
                    try
                    {
                        this.g.Dispose();
                    }
                    catch { }
                    finally { this.g = null; }
                }
                if (this.bitmap != null)
                {
                    try
                    {
                        this.bitmap.Dispose();
                    }
                    catch { }
                    finally { this.bitmap = null; }
                }
                GC.SuppressFinalize(this);
            }
            ~GDIBitmap(){
                this.Dispose();
            }
        }
    }
}