using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Gageas.Lutea.DefaultUI
{
    class RatingRenderer
    {
        private const int RatesN = 5;
        private Bitmap[] StarImages = new Bitmap[RatesN + 1];
        public int Height
        {
            get { return StarImages[0].Height; }
        }

        public int Width
        {
            get { return StarImages[0].Width; }
        }

        public int EachWidth
        {
            get { return Width / RatesN; }
        }

        public RatingRenderer(string filename_on, string filename_off)
        {
            // レーティング用の画像を準備
            Image StarImage_on, StarImage_off;

            try
            {
                StarImage_on = Image.FromFile(filename_on);
            }
            catch
            {
                StarImage_on = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(StarImage_on))
                {
                    g.FillEllipse(SystemBrushes.ControlText, 2, 2, 12, 12);
                }
            }

            try
            {
                StarImage_off = Image.FromFile(filename_off);
            }
            catch
            {
                StarImage_off = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(StarImage_off))
                {
                    g.FillRectangle(SystemBrushes.GrayText, 6, 6, 4, 4);
                }
            }

            for (int i = 0; i <= RatesN; i++)
            {
                StarImages[i] = new Bitmap(StarImage_on.Width * RatesN, StarImage_on.Height);
                using (var g = Graphics.FromImage(StarImages[i]))
                {
                    for (int j = 0; j < RatesN; j++)
                    {
                        g.DrawImage(i > j ? StarImage_on : StarImage_off, j * StarImage_on.Width, 0);
                    }
                }
            }
        }

        public void Draw(int rate, Graphics g, int x, int y)
        {
            Draw(rate, g, x, y, Width, Height);
        }
        public void Draw(int rate, Graphics g, int x, int y, int width, int height)
        {
            if (rate < 0 || rate > RatesN) return;
            int _y = (Height - height) / 2;
            g.DrawImage(StarImages[rate], x, y, new Rectangle(0, _y, Math.Min(width, Width), height), GraphicsUnit.Pixel);
        }

        public void Draw(double rate, Graphics g, int x, int y)
        {
            Draw(rate, g, x, y, Width, Height);
        }
        public void Draw(double rate, Graphics g, int x, int y, int width, int height)
        {
            if (rate < 0 || rate > RatesN) return;
            int _y = (Height - height) / 2;
            g.DrawImage(StarImages[0], x, y, new Rectangle(0, _y, Math.Min(width, Width), height), GraphicsUnit.Pixel);
            int xn = (int)(StarImages[0].Width * rate / RatesN);
            g.DrawImage(StarImages[RatesN], x, y, new Rectangle(0, _y, Math.Min(width, xn), height), GraphicsUnit.Pixel);
        }
    }
}
