using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Gageas.Lutea.Util
{
    public class ImageUtil
    {
        /// <summary>
        /// 画像を指定したwidth*heightに収まるようにアスペクト比を保ったまま縮小する。
        /// Imageのサイズがwidth*heightになるように画像の周囲には余白をつける。
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static Image GetResizedImageWithPadding(Image image, int width, int height)
        {
            return GetResizedImageWithPadding(image, width, height, Color.White);
        }

        public static Image GetResizedImageWithPadding(Image image, int width, int height, Color backgroundColor)
        {
            double xZoomMax = (double)width / image.Width;
            double yZoomMax = (double)height / image.Height;

            double zoom = Math.Min(xZoomMax, yZoomMax);

            int resizedWidth = 0;
            int resizedHeight = 0;

            int padX = 0;
            int padY = 0;

            if (xZoomMax > yZoomMax)
            {
                resizedWidth = (int)(yZoomMax * image.Width);
                resizedHeight = height;
                padY = 0;
                padX = (width - resizedWidth) / 2;
            }
            else
            {
                resizedWidth = width;
                resizedHeight = (int)(xZoomMax * image.Height);
                padX = 0;
                padY = (height - resizedHeight) / 2;
            }

            Image dest = new Bitmap(width, height);
            using (var g = Graphics.FromImage(dest))
            {
                g.Clear(backgroundColor);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, padX, padY, resizedWidth, resizedHeight);
            }
            return dest;
        }

        /// <summary>
        /// fromを背景にtoをopacityの不透明度で描画したImageオブジェクトを返す
        /// </summary>
        /// <param name="from">背景画像</param>
        /// <param name="to">オーバーレイする画像</param>
        /// <param name="opacity">不透明度</param>
        /// <returns>描画結果のImage(Bitmap)オブジェクト</returns>
        public static Image GetAlphaComposedImage(Image from, Image to, float opacity)
        {
            Image ret = new Bitmap(to.Width, to.Height);
            using (var g = Graphics.FromImage(ret))
            {
                g.DrawImage(from, 0, 0);
            }
            AlphaComposedImage(ret, to, opacity);
            return ret;
        }

        /// <summary>
        /// fromの画像上にtoの画像をopacityの不透明度で描画する
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="opacity"></param>
        public static void AlphaComposedImage(Image from, Image to, float opacity)
        {
            System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix();
            System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes();

            cm.Matrix33 = opacity;
            ia.SetColorMatrix(cm);
            SolidBrush b = new SolidBrush(Color.FromArgb((int)(Math.Sin(opacity * Math.PI) * 40), Color.White));
            using (var gg = Graphics.FromImage(from))
            {
                gg.DrawImage(to, new Rectangle(0, 0, to.Width, to.Height), 0, 0, to.Width, to.Height, GraphicsUnit.Pixel, ia);
                gg.FillRectangle(b, 0, 0, to.Width, to.Height);
                gg.DrawRectangle(Pens.Gray, 0, 0, to.Width - 1, to.Height - 1);
            }
            return;
        }

        public static Image GetResizedImageWithoutPadding(Image image, int width, int height)
        {
            double xZoomMax = (double)width / image.Width;
            double yZoomMax = (double)height / image.Height;

            double zoom = Math.Min(xZoomMax, yZoomMax);

            int resizedWidth = 0;
            int resizedHeight = 0;

            if (xZoomMax > yZoomMax)
            {
                resizedWidth = (int)(yZoomMax * image.Width);
                resizedHeight = height;
            }
            else
            {
                resizedWidth = width;
                resizedHeight = (int)(xZoomMax * image.Height);
            }

            Image dest = new Bitmap(resizedWidth, resizedHeight);
            using (var g = Graphics.FromImage(dest))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, 0, 0, resizedWidth, resizedHeight);
            }
            return dest;
        }
    }
}
