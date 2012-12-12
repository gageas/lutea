using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    class SpectrumRenderer
    {
        private PictureBox DestPictureBox = null;
        private Boolean FFTLogarithmic = false;
        private DefaultUIPreference.FFTNum FFTNum;
        private Color Color1;
        private Color Color2;
        private int SpectrumMode;
        private Thread spectrumAnalyzerThread = null;

        public SpectrumRenderer(PictureBox destPictureBox, Boolean FFTLogarithmic, DefaultUIPreference.FFTNum FFTNum, Color Color1, Color Color2, int SpectrumMode)
        {
            this.DestPictureBox = destPictureBox;
            this.FFTLogarithmic = FFTLogarithmic;
            this.FFTNum = FFTNum;
            this.Color1 = Color1;
            this.Color2 = Color2;
            this.SpectrumMode = SpectrumMode;
        }

        public void Start()
        {
            if(this.spectrumAnalyzerThread == null){
                this.spectrumAnalyzerThread = new Thread(SpectrumAnalyzerProc);
                this.spectrumAnalyzerThread.IsBackground = true;
            }
            this.spectrumAnalyzerThread.Start();
        }

        public void Abort()
        {
            if (this.spectrumAnalyzerThread != null)
            {
                this.spectrumAnalyzerThread.Abort();
            }
        }

        public void Clear()
        {
            if (DestPictureBox != null)
            {
                var img = new Bitmap(Math.Max(1, DestPictureBox.Width), Math.Max(1, DestPictureBox.Height));
                using (var g = Graphics.FromImage(img))
                {
                    g.Clear(DestPictureBox.Parent.BackColor);
                }
                DestPictureBox.Image = img;
                DestPictureBox.Refresh();
            }
        }

        private void SpectrumAnalyzerProc()
        {
            float[] fftdata = null;
            float[] barPosition = null;
            float[] barWidth = null;
            Point[] points = null;
            bool isLogarithmic = FFTLogarithmic; //barPosition,barWidthがLog用で初期化されているかどうか
            DefaultUIPreference.FFTNum fftNum = FFTNum;
            int w = 0;
            int h = 0;
            Bitmap b = null;
            SolidBrush opacityBackgroundBlush = new SolidBrush(Color.FromArgb(70, DestPictureBox.Parent.BackColor));
            while (true)
            {
                DestPictureBox.Invoke((MethodInvoker)(() =>
                {
                    w = DestPictureBox.Width;
                    h = DestPictureBox.Height;

                    // 描画の条件が変わる等した場合
                    if (b == null || DestPictureBox.Image == null || w != b.Width || h != b.Height)
                    {
                        if (w * h > 0)
                        {
                            b = new Bitmap(DestPictureBox.Width, DestPictureBox.Height);
                            using (var g = Graphics.FromImage(b))
                            {
                                g.Clear(DestPictureBox.Parent.BackColor);
                            }
                            DestPictureBox.Image = (Bitmap)b.Clone();
                            barPosition = null;
                            isLogarithmic = FFTLogarithmic;
                            fftNum = FFTNum;
                            fftdata = new float[(int)fftNum / 2];
                            points = new Point[fftdata.Length];
                            for (int i = 0; i < points.Length; i++)
                            {
                                points[i] = new Point();
                            }
                        }
                        else
                        {
                            DestPictureBox.Image = null;
                            b = null;
                        }
                    }
                    if (DestPictureBox.Image != null && spectrumAnalyzerThread != null)
                    {
                        using (var g = Graphics.FromImage(DestPictureBox.Image))
                        {
                            g.DrawImage(b, 0, 0);
                        }
                        DestPictureBox.Refresh();
                    }
                }));

                Thread.Sleep(20);
                if (SpectrumMode < 0 || SpectrumMode > 4 || !Controller.IsPlaying)
                {
                    Thread.Sleep(200);
                    continue;
                }
                if ((w * h) > 0)
                {
                    Wrapper.BASS.BASS.IPlayable.FFT bassFFTNum = fftNum == DefaultUIPreference.FFTNum.FFT256
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT256
                        : fftNum == DefaultUIPreference.FFTNum.FFT512
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT512
                        : fftNum == DefaultUIPreference.FFTNum.FFT1024
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT1024
                        : fftNum == DefaultUIPreference.FFTNum.FFT2048
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT2048
                        : fftNum == DefaultUIPreference.FFTNum.FFT4096
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT4096
                        : Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT8192;
                    Controller.FFTData(fftdata, bassFFTNum);
                    int n = fftdata.Length;
                    float ww = (float)w / n;
                    using (var g = Graphics.FromImage(b))
                    {
                        g.FillRectangle(opacityBackgroundBlush, 0, 0, w, h);
                        var rect = new RectangleF();
                        rect.Width = ww;
                        var brush = new SolidBrush(Color.White);

                        double max = Math.Log10(n);
                        if (barPosition == null)
                        {
                            barPosition = new float[fftdata.Length];
                            barWidth = new float[fftdata.Length];
                            for (int i = 1; i < n; i++)
                            {
                                if (FFTLogarithmic)
                                {
                                    barPosition[i] = (float)(Math.Log10(i) / max * w);
                                    barWidth[i] = (float)((Math.Log10(i + 1) - Math.Log10(i)) / max * w);
                                }
                                else
                                {
                                    barPosition[i] = (float)i * ww;
                                    barWidth[i] = ww;
                                }
                            }
                        }

                        // ちょっとかっこ悪いけどこのループ内で分岐書きたくないので
                        if (SpectrumMode == 0)
                        {
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
    }
}
