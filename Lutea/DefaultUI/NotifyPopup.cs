using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    public partial class NotifyPopupForm : Form
    {
        private float CoverArtSizeRatio = 1.8F;
        private const int MaxWidth = 10;
        private const int OverSample = 4;
        private const int Pad = 5;
        private const float Title1Size = 18.0F;
        private const float Title2Size = 14.0F;
        private const float StartOpacity = 1.0F;
        private const int BeforeFadeInWaitTicks = 10;
        private const int BeforeFadeOutWaitTicks = 120;
        private readonly Color BorderColor = SystemColors.ActiveBorder;
        private readonly Color NotifyBackgroundColor = SystemColors.Window;
        private const TextFormatFlags TFFlags = TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix;

        private Timer timer;

        enum State
        {
            BeforeFadein,
            FadeIn,
            Show,
            Fadeout,
        }

        private class TransitContext
        {
            public int beforeFadeInRemain = BeforeFadeInWaitTicks;
            public int beforeFadeOutRemain = BeforeFadeOutWaitTicks;
            public double Offset;
            public State state;
            public Bitmap PrerenderImage;
        }

        /// <summary>
        /// Alt+Tabで列挙させない
        /// ref. http://youryella.wankuma.com/Library/Extensions/Form/HideAltTabDialog.aspx
        /// </summary>
        protected override System.Windows.Forms.CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;

                // ExStyle に WS_EX_TOOLWINDOW ビットを立てる
                CreateParams cp = base.CreateParams;
                cp.ExStyle = cp.ExStyle | WS_EX_TOOLWINDOW;

                return cp;
            }
        }

        public NotifyPopupForm()
        {
            InitializeComponent();
            this.TransparencyKey = BackColor;
        }

        public void DoNotify(string title, string title2, Image coverArt, Font font)
        {
            EndNotify();

            title = title.Replace("\n", "; ").TrimEnd();
            title2 = title2.Replace("\n", "; ").TrimEnd();

            Bitmap renderedImage = DrawPrerenderImage(title, title2, coverArt, font);

            this.Invoke((Action)(() =>
            {
                var workingArea = Screen.PrimaryScreen.WorkingArea;
                this.Opacity = 0.0F;
                this.Location = new Point(workingArea.Right - renderedImage.Width - Pad, workingArea.Bottom - renderedImage.Height - Pad);
                this.Size = renderedImage.Size;
                this.Refresh();
                timer = new Timer();
                timer.Interval = 30;
                timer.Tag = new TransitContext() { Offset = renderedImage.Width, PrerenderImage = renderedImage };
                timer.Tick += new EventHandler(t_Tick);
                timer.Start();
            }));
        }

        public void EndNotify()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
                this.Invoke((Action)(() =>
                {
                    this.Opacity = 0.0F;
                    this.Size = new Size(1, 1);
                    this.Refresh(); // これ必要。強制的に全面透過色でPaintさせておかないと再表示時に一瞬前のが表示されてしまう
                    this.Opacity = 1.0F;
                }));
            }
        }

        #region Event handlers
        private void t_Tick(object sender, EventArgs e)
        {
            var context = (TransitContext)((Timer)sender).Tag;

            switch (context.state)
            {
                case  State.BeforeFadein:
                    context.beforeFadeInRemain--;
                    if (context.beforeFadeInRemain == 0)
                    {
                        this.Opacity = StartOpacity;
                        context.state = State.FadeIn;
                    }
                    break;
                case State.FadeIn:
                    if (context.Offset == 0)
                    {
                        context.state = State.Show;
                    }
                    else
                    {
                        context.Offset -= Math.Max(1, context.Offset / 5);
                        if (context.Offset < 0) context.Offset = 0;
                        this.Refresh();
                    }
                    break;
                case State.Show:
                    if (context.beforeFadeOutRemain == 0)
                    {
                        context.state = State.Fadeout;
                    }
                    else
                    {
                        context.beforeFadeOutRemain--;
                    }
                    break;
                case State.Fadeout:
                    this.Opacity -= 0.01;
                    if (this.Opacity <= 0)
                    {
                        EndNotify();
                    }
                    break;
            }
        }

        private void NotifyPopupForm_Click(object sender, EventArgs e)
        {
            EndNotify();
        }

        private void NotifyPopupForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (timer != null && timer.Tag != null)
            {
                var context = (TransitContext)timer.Tag;
                if (context.state == State.Show || context.state == State.Fadeout)
                {
                    context.beforeFadeOutRemain = BeforeFadeOutWaitTicks;
                    context.state = State.Show;
                    this.Opacity = StartOpacity;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (timer != null && timer.Tag != null)
            {
                var context = (TransitContext)timer.Tag;
                if (context.PrerenderImage != null)
                {
                    int intOffset = (int)((TransitContext)timer.Tag).Offset;

                    g.FillRectangle(new SolidBrush(this.TransparencyKey), 0, 0, intOffset - 1, this.Height);
                    g.DrawImage(context.PrerenderImage, intOffset, 0);

                    if (this.Width == intOffset) return;

                    // スライドインしてくるときのエッジを修正
                    var y = FindContentEdgeVert(context.PrerenderImage, this.Width - intOffset - 1);
                    g.FillRectangle(new SolidBrush(NotifyBackgroundColor), this.Width - 3, y + 1, 2, this.Height - y - 2);
                    g.DrawLine(new Pen(BorderColor), this.Width - 1, y, this.Width - 1, this.Height);
                    return;
                }
            }
            g.Clear(this.TransparencyKey);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // nothing
            // PaintBackgroundでは何もせず，OnPaint単体で必ず正常に描画できるためDoubleBufferedをfalseにできる。
        }
        #endregion

        #region Rendering
        /// <summary>
        /// テキストの領域を描画
        /// </summary>
        /// <param name="title1">1行目</param>
        /// <param name="title2">2行目</param>
        /// <param name="font">フォント</param>
        /// <returns>Bitmapオブジェクト</returns>
        private Bitmap DrawOversampleTextArea(string title1, string title2, Font font)
        {
            Font font1 = new Font(font.FontFamily, Title1Size * OverSample);
            Font font2 = new Font(font.FontFamily, Title2Size * OverSample);

            var textw1 = TextRenderer.MeasureText(title1, font1, new Size(), TFFlags | TextFormatFlags.NoClipping).Width;
            var textw2 = TextRenderer.MeasureText(title2, font2, new Size(), TFFlags | TextFormatFlags.NoClipping).Width;

            var height = font1.Height + font2.Height + Pad * OverSample;
            var width = Math.Min(height * MaxWidth, Math.Max(textw1, textw2));

            Bitmap bitmap = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(NotifyBackgroundColor);
                var rect1 = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var rect2 = new Rectangle(0, font1.Height + Pad * OverSample, bitmap.Width, bitmap.Height);
                TextRenderer.DrawText(g, title1, font1, rect1, SystemColors.WindowText, TFFlags | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, title2, font2, rect2, SystemColors.WindowText, TFFlags | TextFormatFlags.EndEllipsis);
            }
            return bitmap;
        }

        /// <summary>
        /// 通知領域全体をビットマップとして描画
        /// </summary>
        /// <param name="title">1行目</param>
        /// <param name="title2">2行目</param>
        /// <param name="coverArt">カバーアート画像</param>
        /// <param name="font">フォント</param>
        /// <returns>通知領域を描画したビットマップ</returns>
        private Bitmap DrawPrerenderImage(string title, string title2, Image coverArt, Font font)
        {
            Bitmap renderedText = DrawOversampleTextArea(title, title2, font);
            Size renderedTextSize = new System.Drawing.Size(renderedText.Width / OverSample, renderedText.Height / OverSample);

            var notifyHeight = Math.Max((Pad + renderedTextSize.Height + Pad), (int)((Pad + renderedTextSize.Height + Pad) * CoverArtSizeRatio));
            var notifyWidth = (Pad + renderedTextSize.Width + Pad);

            var hasCoverArt = coverArt != null;

            // 縮小カバーアートを生成
            Image resizedCoverArt = null;
            if (hasCoverArt)
            {
                resizedCoverArt = Gageas.Lutea.Util.ImageUtil.GetResizedImageWithoutPadding(coverArt, notifyHeight, notifyHeight);
                UnTransparencyColor((Bitmap)resizedCoverArt, this.TransparencyKey);
                notifyWidth += resizedCoverArt.Width - Pad;
            }

            // Bitmapを生成
            var notifyContentImage = new Bitmap(notifyWidth, notifyHeight);
            using (Graphics g = Graphics.FromImage(notifyContentImage))
            {
                // 透過色で全面クリア
                g.Clear(this.TransparencyKey);

                // テキスト枠座標計算
                var wt = (Pad + renderedTextSize.Width + Pad) - 1;
                var ht = (Pad + renderedTextSize.Height + Pad) - 1;
                var xt = 0;
                var yt = notifyContentImage.Height - ht - 1;

                // テキスト枠背景を描画
                g.FillRectangle(new SolidBrush(NotifyBackgroundColor), xt, yt, wt, ht);

                // テキストを描画
                g.DrawImage(renderedText, xt + Pad, yt + Pad, renderedTextSize.Width, renderedTextSize.Height);

                // テキスト枠境界線を描画
                g.DrawRectangle(new Pen(BorderColor), xt, yt, wt, ht);

                // カバーアートを描画
                if (hasCoverArt)
                {
                    // カバーアート矩形座標計算
                    var xc = (Pad + renderedTextSize.Width); // + Pad
                    var yc = notifyContentImage.Height - resizedCoverArt.Height;
                    var wc = resizedCoverArt.Width - 1;
                    var hc = resizedCoverArt.Height - 1;

                    // カバーアート描画
                    g.DrawImage(resizedCoverArt, xc, yc);

                    // カバーアート枠境界線を描画
                    // カバーアートの高さがテキストの高さより低い場合は，枠の高さはテキストの高さに合わせる
                    if (hc < ht)
                    {
                        hc = ht;
                        yc = yt;
                    }
                    g.DrawRectangle(new Pen(BorderColor), xc, yc, wc, hc);
                    g.DrawRectangle(new Pen(NotifyBackgroundColor), xc + 1, yc + 1, wc - 2, hc - 2);
                    g.DrawRectangle(new Pen(NotifyBackgroundColor), xc + 2, yc + 2, wc - 4, hc - 4);
                    g.DrawLine(new Pen(NotifyBackgroundColor), xc, yt + 1, xc, notifyHeight - 1 - 1);
                }
            }

            return notifyContentImage;
        }
        #endregion

        #region Bitmap utility
        private int FindContentEdgeVert(Bitmap bitmap, int xOffset)
        {
            // TransparencyKey色とコンテンツの境界となるy座標を探す
            int y = bitmap.Height / 2;
            int delta = (y + 1) / 2;
            int transparencyKeyArgb = this.TransparencyKey.ToArgb();
            while (true)
            {
                if (y < 1 || y > bitmap.Height - 1)
                {
                    y = 0;
                    break;
                }
                if (bitmap.GetPixel(xOffset, y - 1).ToArgb() != transparencyKeyArgb)
                {
                    y -= delta;
                    delta /= 2;
                    if (delta == 0) delta = 1;
                    continue;
                }
                if (bitmap.GetPixel(xOffset, y).ToArgb() == transparencyKeyArgb)
                {
                    y += delta;
                    delta /= 2;
                    if (delta == 0) delta = 1;
                    continue;
                }
                y++;
                break;
            }
            return y;
        }

        /// <summary>
        /// 意図しない画素が透明化されるのを防ぐため
        /// TransparencyKeyと同じ色の画素値をちょっと変える(最下位ビット反転)
        /// </summary>
        /// <param name="bitmap"></param>
        private void UnTransparencyColor(Bitmap bitmap, Color transparencyColor)
        {
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int transparentKeyColorARGB = transparencyColor.ToArgb();
            int count = bitmap.Width * bitmap.Height;
            for (int i = 0; i < count; i++)
            {
                Int32 value = Marshal.ReadInt32(data.Scan0, i * 4);
                if (value == transparentKeyColorARGB)
                {
                    Marshal.WriteInt32(data.Scan0, i * 4, value ^ 1); // flip last bit
                }
            }
            bitmap.UnlockBits(data);
        }
        #endregion
    }
}
