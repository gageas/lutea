using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    public partial class NotifyPopupForm : Form
    {
        private float CoverArtSizeRatio = 1.6F;
        private const int MaxWidth = 7;
        private const int OverSample = 4;
        private const int Pad = 5;
        private const float Title1Size = 18.0F;
        private const float Title2Size = 14.0F;
        private const float StartOpacity = 0.95F;
        private const int BeforeFadeInWaitTicks = 10;
        private const int BeforeFadeOutWaitTicks = 120;
        private const TextFormatFlags TFFlags = TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix;

        private Timer t;

        private class TransitContext
        {
            public int beforeFadeInRemain = BeforeFadeInWaitTicks;
            public int beforeFadeOutRemain = BeforeFadeOutWaitTicks;
            public int h_norm;
            public int w_norm;
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
        }

        public void DoNotify(string title, string title2, Image image, Font font)
        {
            EndNotify();
            title = title.Replace("\n", "; ");
            title2 = title2.Replace("\n", "; ");
            Font font1 = new Font(font.FontFamily, Title1Size * OverSample);
            Font font2 = new Font(font.FontFamily, Title2Size * OverSample);
            var h_OverSample = font1.Height + font2.Height + Pad * 3 * OverSample;
            var h = h_OverSample / OverSample;
            var hCover = (int)(h * CoverArtSizeRatio);
            if (image != null)
            {
                image = Gageas.Lutea.Util.ImageUtil.GetResizedImageWithoutPadding(image, hCover - Pad * 2, hCover - Pad * 2);
            }
            var hasImage = image != null;
            var gw = (hasImage ? (image.Width + Pad) : 0) + Pad;

            var textw1 = TextRenderer.MeasureText(title, font1, new Size(), TFFlags | TextFormatFlags.NoClipping).Width;
            var textw2 =TextRenderer.MeasureText(title2, font2, new Size(), TFFlags | TextFormatFlags.NoClipping).Width;
            var w_OverSample = Math.Min(h_OverSample * MaxWidth, Math.Max(textw1, textw2)) + (gw + Pad) * OverSample;
            var w = w_OverSample / OverSample;

            Bitmap bg = new Bitmap(w_OverSample, h_OverSample);

            this.Invoke((Action)(() =>
            {
                using (Graphics g = Graphics.FromImage(bg))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
                    var rect1 = new Rectangle(0, 0, bg.Width - ((gw + Pad) * OverSample), bg.Height);
                    var rect2 = new Rectangle(0, font1.Height + Pad * OverSample, bg.Width - ((gw + Pad) * OverSample), bg.Height);
                    TextRenderer.DrawText(g, title, font1, rect1, SystemColors.WindowText, TFFlags | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, title2, font2, rect2, SystemColors.WindowText, TFFlags | TextFormatFlags.EndEllipsis);
                }
                this.BackgroundImage = new Bitmap(w, hCover);
                using (Graphics g = Graphics.FromImage(this.BackgroundImage))
                {
                    g.FillRectangle(Brushes.Fuchsia, 0, 0, w, hCover - h);
                    g.DrawRectangle(SystemPens.ActiveBorder, 0, hCover - h, w - 1, h - 1);

                    if (hasImage)
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bicubic;
                        int top = 0;
                        if (image.Height < h)
                        {
                            top = (int)((hCover - image.Height) / CoverArtSizeRatio);
                        }
                        else
                        {
                            top = hCover - image.Height - Pad;
                        }
                        g.DrawImage(image, w-gw+Pad, top);
                        g.DrawRectangle(Pens.Silver, w - gw + Pad, top, image.Width, image.Height);
                    }
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    g.DrawImage(bg, Pad, hCover - h + Pad, w, h);
                }
                var wa = Screen.PrimaryScreen.WorkingArea;
                t = new Timer();
                t.Interval = 30;
                t.Tag = new TransitContext() { h_norm = hCover, w_norm = w };
                t.Tick += new EventHandler(t_Tick);
                t.Start();
                this.Size = new System.Drawing.Size(0, hCover);
                this.TransparencyKey = Color.Fuchsia;
                this.Location = new Point(wa.Right - w - Pad, wa.Bottom - hCover - Pad);
                this.Opacity = 0.0F;
            }));
        }

        void t_Tick(object sender, EventArgs e)
        {
            var self = (Timer)sender;
            var context = (TransitContext)self.Tag;
            var wa = Screen.PrimaryScreen.WorkingArea;

            context.beforeFadeInRemain--;
            if (context.beforeFadeInRemain > 0)
            {
                return;
            }
            else if (context.beforeFadeInRemain == 0)
            {
                this.Opacity = StartOpacity;
            }
            if (this.Width < context.w_norm)
            {
                this.Location = new Point(wa.Right - this.Width - Pad, wa.Bottom - context.h_norm - Pad);
                this.Width += Math.Max(1, (context.w_norm - this.Width) / 5);
                this.Invalidate();
            }
            if (context.beforeFadeOutRemain > 0)
            {
                context.beforeFadeOutRemain--;
                return;
            }
            this.Opacity -= 0.01;
            if (this.Opacity <= 0)
            {
                t.Stop();
            }
        }

        public void EndNotify()
        {
            if (t != null)
            {
                t.Stop();
                this.Invoke((Action)(() =>
                {
                    this.Size = new Size(0, 0);
                    this.Opacity = 0.0F;
                }));
            }
        }

        private void NotifyPopupForm_Click(object sender, EventArgs e)
        {
            EndNotify();
        }

        private void NotifyPopupForm_MouseMove(object sender, MouseEventArgs e)
        {
            ((TransitContext)t.Tag).beforeFadeOutRemain = BeforeFadeOutWaitTicks;
            this.Opacity = StartOpacity;
        }
    }
}
