using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gageas.Lutea.Util;
//using System.Threading;

namespace Gageas.Lutea.DefaultUI
{
    public partial class CoverViewerForm : Form
    {
        private Image artworkImage;
        private System.Timers.Timer fadingTimer;
        private IEnumerator<int> fadeIn;
        public CoverViewerForm(Image img)
        {
            InitializeComponent();
            this.artworkImage = img;
        }

        private void CoverViewer_Load(object sender, EventArgs e)
        {
            this.Invalidate();
            this.Opacity = 0;
            size(0);
            int STEPS = 16;
            fadeIn = IntegerCounterIterator(0, STEPS).GetEnumerator();
            fadingTimer = new System.Timers.Timer();
            fadingTimer.Elapsed += new System.Timers.ElapsedEventHandler((o, arg) => {
                this.Invoke((MethodInvoker)(() =>
                {
                    this.Opacity = (double) fadeIn.Current / STEPS;
                }));
                if (!fadeIn.MoveNext())
                {
                    fadingTimer.Stop();
                }
            });
            fadingTimer.Interval = 5;
            fadingTimer.Start();
        }

        double zoom = 0;
        private void CoverViewer_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
//            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            if (artworkImage == null) return;

            this.Text = String.Format("CoverView {0:00.0}%", zoom*100);

            var w = this.ClientSize.Width + 2;
            var h = this.ClientSize.Height + 2;

            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High;
            g.DrawImage(artworkImage, new Rectangle(-1, -1, w, h), 0, 0, artworkImage.Width, artworkImage.Height, GraphicsUnit.Pixel, null);
            
            return;
        }

        private void CoverViewer_Resize(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        private void CoverViewer_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x020a) // WM_MOUSEWHEEL
            {
                if ((int)m.WParam < 0)
                {
                    size(-1);
                }
                else
                {
                    size(1);
                }
                int i = (int)m.WParam;
                Logger.Debug(i.ToString());
            }
            base.WndProc(ref m);
        }

        private void size(int d)
        {
            var w = this.ClientSize.Width + 2;
            var h = this.ClientSize.Height + 2;
            double xZoomMax = (double)w / artworkImage.Width;
            double yZoomMax = (double)h / artworkImage.Height;

            double c_zoom = Math.Min(xZoomMax, yZoomMax);

            double new_zoom = c_zoom * Math.Pow(1.1, d);
            int new_w = (int)(artworkImage.Width * new_zoom);
            int new_h = (int)(artworkImage.Height * new_zoom);
            Size new_size = this.SizeFromClientSize(new Size(new_w, new_h));

            if (new_size.Width < this.MinimumSize.Width) return;
            if (new_size.Height < this.MinimumSize.Height) return;

            this.Size = new_size;
            this.zoom = new_zoom;
        }

        private void CoverViewer_FormClosing(object sender, FormClosingEventArgs e)
        {
            fadingTimer.Stop();
        }

        private static IEnumerable<int> IntegerCounterIterator(int start, int end, int step = 1)
        {
            for (int i = start; i <= end; i += 1) yield return i;
            yield break;
        }
    }
}
