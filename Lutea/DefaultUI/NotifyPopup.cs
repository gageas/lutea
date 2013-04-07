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
        private const int OverSample = 4;
        private const int Pad = 5;
        private const int Title1LenLimit = 25;
        private const int Title2LenLimit = 40;
        private const float Title1Size = 18.0F;
        private const float Title2Size = 14.0F;
        private const float StartOpacity = 0.95F;
        private const int BeforeFadeInWaitTicks = 10;
        private const int BeforeFadeOutWaitTicks = 120;

        private Timer t;
        private int BeforeFadeInWaitTicksRemain;
        private int BeforeFadeOutWaitTicksRemain;

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
            TextFormatFlags tfFlags = TextFormatFlags.NoClipping | TextFormatFlags.SingleLine | TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.NoPrefix;
            if (title.Length > Title1LenLimit)
            {
                title = title.Substring(0, Title1LenLimit) + "...";
            }

            if (title2.Length > Title2LenLimit)
            {
                title2 = title2.Substring(0, Title2LenLimit) + "...";
            }

            Font font1 = new Font(font.FontFamily, Title1Size * OverSample);
            Font font2 = new Font(font.FontFamily, Title2Size * OverSample);
            var h = font1.Height + font2.Height + Pad * 3 * OverSample;
            var h_norm = h / OverSample;
            if (image != null)
            {
                image = Gageas.Lutea.Util.ImageUtil.GetResizedImageWithoutPadding(image, h_norm - Pad * 2, h_norm - Pad * 2);
            }
            var hasImage = image != null;
            var gw = (hasImage ? (image.Width + Pad) : 0) + Pad;

            var w = Math.Max(TextRenderer.MeasureText(title, font1, new Size(), tfFlags).Width, TextRenderer.MeasureText(title2, font2, new Size(), tfFlags).Width) + (gw + Pad) * OverSample;
            var w_norm = w / OverSample;

            Bitmap bg = new Bitmap(w, h);

            this.Invoke((Action)(() =>
            {
                using (Graphics g = Graphics.FromImage(bg))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
                    TextRenderer.DrawText(g, title, font1, new Rectangle(0, 0, bg.Width, bg.Height), SystemColors.WindowText, tfFlags);
                    TextRenderer.DrawText(g, title2, font2, new Rectangle(0, font1.Height + Pad * OverSample, bg.Width, bg.Height), SystemColors.WindowText, tfFlags);

                }
                this.BackgroundImage = new Bitmap(w_norm, h_norm);
                using (Graphics g = Graphics.FromImage(this.BackgroundImage))
                {
                    g.DrawRectangle(SystemPens.ActiveBorder, 0, 0, w_norm - 1, h_norm - 1);

                    if (hasImage)
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bicubic;
                        g.DrawImage(image, Pad, (h_norm - image.Height) / 2);
                    }
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
                    g.DrawImage(bg, gw, Pad, w_norm, h_norm);
                }
                var wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                BeforeFadeOutWaitTicksRemain = BeforeFadeOutWaitTicks;
                BeforeFadeInWaitTicksRemain = BeforeFadeInWaitTicks;
                t = new Timer();
                t.Interval = 30;
                t.Tick += (_, __) =>
                {
                    BeforeFadeInWaitTicksRemain--;
                    if (BeforeFadeInWaitTicksRemain > 0)
                    {
                        return;
                    }
                    else if (BeforeFadeInWaitTicksRemain == 0)
                    {
                        this.Opacity = StartOpacity;
                    }
                    if (this.Width < w_norm)
                    {
                        this.Location = new Point(wa.Right - this.Width - Pad, wa.Bottom - h_norm - Pad);
                        this.Width += Math.Max(1, (w_norm - this.Width) / 5);
                        this.Invalidate();
                    }
                    if (BeforeFadeOutWaitTicksRemain > 0)
                    {
                        BeforeFadeOutWaitTicksRemain--;
                        return;
                    }
                    this.Opacity -= 0.01;
                    if (this.Opacity <= 0)
                    {
                        t.Stop();
                    }
                };
                t.Start();
                this.Size = new System.Drawing.Size(0, h_norm);
                this.Location = new Point(wa.Right - w_norm - Pad, wa.Bottom - h_norm - Pad);
                this.Opacity = 0.0F;
            }));
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
            BeforeFadeOutWaitTicksRemain = BeforeFadeOutWaitTicks;
            this.Opacity = StartOpacity;
        }
    }
}
