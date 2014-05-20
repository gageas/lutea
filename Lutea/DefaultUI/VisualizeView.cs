using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    class VisualizeView : UserControl, System.ComponentModel.ISupportInitialize
    {
        private Boolean FFTLogarithmic = false;
        private Controller.FFTNum FFTNum;
        private Color Color1;
        private Color Color2;
        private int SpectrumMode;
        private Thread spectrumAnalyzerThread = null;
        private Bitmap Image;

        public VisualizeView()
        {
            this.Paint += new PaintEventHandler(VisualizeView_Paint);
            this.DoubleBuffered = true;
        }

        void VisualizeView_Paint(object sender, PaintEventArgs e)
        {
            if (this.Image == null) return;
            var g = e.Graphics;
            g.DrawImage(this.Image, 0, 0);
        }

        public void Setup(Boolean FFTLogarithmic, Controller.FFTNum FFTNum, Color Color1, Color Color2, DefaultUIPreference.SpectrumModes SpectrumMode)
        {
            this.FFTLogarithmic = FFTLogarithmic;
            this.FFTNum = FFTNum;
            this.Color1 = Color1;
            this.Color2 = Color2;
            this.SpectrumMode = (int)SpectrumMode;
        }

        public void Start()
        {
            if(spectrumAnalyzerThread == null){
                spectrumAnalyzerThread = new Thread(() => { try { SpectrumAnalyzerProc(); } catch (ThreadInterruptedException) { } });
                spectrumAnalyzerThread.IsBackground = true;
                spectrumAnalyzerThread.Start();
            }
        }

        public void Abort()
        {
            if (spectrumAnalyzerThread != null)
            {
                spectrumAnalyzerThread.Interrupt();
                spectrumAnalyzerThread.Join();
                spectrumAnalyzerThread = null;
            }
        }

        public void Clear()
        {
            var img = new Bitmap(Math.Max(1, this.Width), Math.Max(1, this.Height));
            using (var g = Graphics.FromImage(img))
            {
                g.Clear(this.Parent.BackColor);
            }
            this.Image = img;
            this.Refresh();
        }

        private void SpectrumAnalyzerProc()
        {
            float[] fftdata = null;
            float[] fftdata_prev = null;
            float[] barPosition = null;
            float[] barWidth = null;
            Point[] points = null;
            bool isLogarithmic = FFTLogarithmic; //barPosition,barWidthがLog用で初期化されているかどうか
            Controller.FFTNum fftNum = FFTNum;
            int w = 0;
            int h = 0;
            Bitmap b = null;
            Bitmap[] interThreadBuffer = new Bitmap[2]; // GC祭りにならないようにできるだけBitmapオブジェクトを使いまわす
            SolidBrush opacityBackgroundBlush = new SolidBrush(Color.FromArgb(128, this.Parent.BackColor));
            while (true)
            {
                if (Controller.IsPlaying)
                {
                    w = this.Width;
                    h = this.Height;

                    // 描画の条件が変わる等した場合
                    if (b == null || this.Image == null || w != b.Width || h != b.Height)
                    {
                        if (w * h > 0)
                        {
                            b = new Bitmap(this.Width, this.Height);
                            using (var g = Graphics.FromImage(b))
                            {
                                g.Clear(BackColor);
                            }
                            this.Image = null;
                            if (interThreadBuffer[0] != null) interThreadBuffer[0].Dispose();
                            if (interThreadBuffer[1] != null) interThreadBuffer[1].Dispose();
                            interThreadBuffer[0] = (Bitmap)b.Clone();
                            interThreadBuffer[1] = (Bitmap)b.Clone();
                            this.Image = interThreadBuffer[0];
                            barPosition = null;
                            isLogarithmic = FFTLogarithmic;
                            fftNum = FFTNum;
                            fftdata = new float[(int)fftNum / 2];
                            fftdata_prev = new float[(int)fftNum / 2];
                            points = new Point[fftdata.Length];
                        }
                        else
                        {
                            this.Image = null;
                            b = null;
                        }
                    }
                    if (this.Image != null && spectrumAnalyzerThread != null)
                    {
                        this.Invoke((MethodInvoker)(() =>
                        {
                            var target = this.Image == interThreadBuffer[0] ? interThreadBuffer[1] : interThreadBuffer[0];
                            using (var g = Graphics.FromImage(target))
                            {
                                g.DrawImage(b, 0, 0);
                            }
                            this.Image = target;
                            Refresh();
                        }));
                    }
                }

                Thread.Sleep(20);
                if (SpectrumMode < 0 || SpectrumMode > 4 || !Controller.IsPlaying)
                {
                    Thread.Sleep(200);
                    continue;
                }
                if ((w * h) > 0)
                {
                    Controller.FFTData(fftdata, fftNum);
                    for (int i = 0; i < fftdata.Length; i++)
                    {
                        fftdata[i] = (float)Math.Max(fftdata[i], fftdata_prev[i] * 0.8);
                    }
                    Array.Copy(fftdata, fftdata_prev, fftdata.Length);
                    int n = fftdata.Length;
                    float ww = (float)w / n;
                    using (var g = Graphics.FromImage(b))
                    {
                        g.FillRectangle(opacityBackgroundBlush, 0, 0, w, h);
                        var brush = new SolidBrush(Color.White);

                        double max = Math.Log10(n);
                        if (barPosition == null)
                        {
                            barPosition = new float[fftdata.Length];
                            barWidth = new float[fftdata.Length];
                            if (FFTLogarithmic)
                            {
                                for (int i = 1; i < n; i++)
                                {
                                    barPosition[i] = (float)(Math.Log10(i) / max * w);
                                    barWidth[i] = (float)((Math.Log10(i + 1) - Math.Log10(i)) / max * w);
                                }
                            }
                            else
                            {
                                for (int i = 1; i < n; i++)
                                {
                                    barPosition[i] = (float)i * ww;
                                    barWidth[i] = ww;
                                }
                            }
                        }

                        // ちょっとかっこ悪いけどこのループ内で分岐書きたくないので
                        if (SpectrumMode == 0)
                        {
                            var rect = new RectangleF();
                            rect.Width = ww;
                            for (int j = 0; j < n; j++)
                            {
                                float d = (float)(fftdata[j] * h * j / 8);
                                int c = (int)(Math.Pow(0.03, fftdata[j] * j / 30.0) * 255);
                                rect.X = barPosition[j];
                                rect.Width = barWidth[j];
                                rect.Y = h - d;
                                rect.Height = d;
                                brush.Color = Color.FromArgb((int)c, Color1);
                                g.FillRectangle(brush, rect);
                                brush.Color = Color.FromArgb(255 - (int)c, Color2);
                                g.FillRectangle(brush, rect);
                            }
                        }
                        else
                        {
                            for (int j = 0; j < n; j++)
                            {
                                points[j].X = (int)barPosition[j];
                                points[j].Y = (int)(h - fftdata[j] * h * j / 8);
                            }
                            points[points.Length - 1].Y = h;
                            switch (SpectrumMode)
                            {
                                case 0:
                                    break;
                                case 1:
                                    g.DrawLines(new Pen(Color2), points);
                                    break;
                                case 2:
                                    g.DrawCurve(new Pen(Color2), points);
                                    break;
                                case 3:
                                    g.FillPolygon(new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), Color2, Color1, 90, false), points);
                                    break;
                                case 4:
                                    g.FillClosedCurve(new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), Color2, Color1, 90, false), points);
                                    break;
                                default:
                                    Thread.Sleep(100);
                                    break;
                            }
                        }
                    }
                }
            }
        }

        public void BeginInit()
        {
        }

        public void EndInit()
        {
        }
    }
}
