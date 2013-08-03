using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    public partial class XTrackBar : UserControl
    {
        private TrackBarThumbState thumbState = TrackBarThumbState.Normal;

        private int thumbwidth;

        private int thumby = 0;
 
        private int padx = 7;
        private int pady = 0;

        private double _max = 100;
        private double _min = 0;
        private double _value = 10;

        #region Getter&Setter
        public double Max
        {
            get
            {
                return _max;
            }
            set
            {
                _max = value; // FIXME: check value range
                this.Invalidate();
            }
        }

        public double Min
        {
            get
            {
                return _min;
            }
            set
            {
                _min = value;
                this.Invalidate();
            }
        }

        public double Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (value > Max) value = Max;
                if (value < Min) value = Min;
                if ((value >= Min) && (value <= Max))
                {
                    _value = value;
                    this.Invalidate();
                }
            }
        }
        public int ThumbWidth
        {
            get
            {
                return thumbwidth;
            }
            set
            {
                thumbwidth = value;
                padx = 2 + value / 2;
            }
        }
        public string ThumbText { get; set; }
        #endregion
        
        static readonly StringFormat sf = new StringFormat();

        static XTrackBar()
        {
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            sf.FormatFlags = StringFormatFlags.NoWrap;
        }

        public XTrackBar()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
        }

        /* padding幅を引いたwidth */
        private int innerWidth
        {
            get
            {
                return this.Width - padx * 2;
            }
        }

        /* thumbのX位置 */
        private int thumbX
        {
            get
            {
                if (Max == 0) return padx;
                return (int)(padx + (innerWidth * Value / Max) - (thumbwidth / 2));
            }
        }

        private int XtoValue(int X)
        {
            int xpos = X - padx;
            return (int)(Max * xpos / innerWidth);
        }
        private void XTrackBar_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle track = new Rectangle(padx, (int)(this.Height * 0.4), innerWidth - 1, (int)(this.Height * 0.2));
            Rectangle thumb = new Rectangle(thumbX, 3 + pady, thumbwidth, this.Height - 6);
            if (TrackBarRenderer.IsSupported)
            {
                TrackBarRenderer.DrawHorizontalTrack(g, track);
                if (Enabled)
                {
                    TrackBarRenderer.DrawHorizontalThumb(g, thumb, thumbState);
                }
            }
            else
            {
                g.DrawLine(SystemPens.ButtonShadow, track.Left, track.Top, track.Right, track.Top);
                g.DrawLine(SystemPens.ButtonShadow, track.Left, track.Top, track.Left, track.Bottom);
                g.DrawLine(SystemPens.ButtonHighlight, track.Right, track.Bottom, track.Right, track.Top);
                g.DrawLine(SystemPens.ButtonHighlight, track.Right, track.Bottom, track.Left, track.Bottom);

                if (Enabled)
                {
                    g.FillRectangle(thumbState == TrackBarThumbState.Normal ? SystemBrushes.ButtonFace : SystemBrushes.ButtonHighlight, thumb);
                    g.DrawLine(SystemPens.ButtonHighlight, thumb.Left, thumb.Top, thumb.Right, thumb.Top);
                    g.DrawLine(SystemPens.ButtonHighlight, thumb.Left, thumb.Top, thumb.Left, thumb.Bottom);
                    g.DrawLine(SystemPens.ButtonShadow, thumb.Right, thumb.Bottom, thumb.Right, thumb.Top);
                    g.DrawLine(SystemPens.ButtonShadow, thumb.Right, thumb.Bottom, thumb.Left, thumb.Bottom);
                }
            }
            if (Enabled && ThumbText != null)
            {
                TextRenderer.DrawText(g, ThumbText, this.Font, thumb, this.ForeColor, TextFormatFlags.NoClipping | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
//                g.DrawString(ThumbText, this.Font, SystemBrushes.ControlText, thumb, sf);
            }
        }

        private void XTrackBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (Capture)
            {
                Value = XtoValue(e.X);
                if (OnScroll != null)
                {
                    OnScroll.Invoke();
                }
                thumbState = TrackBarThumbState.Pressed;
            }
            else
            {
                if ((e.X > thumbX - this.Height/2) && (e.X < thumbX + this.Height) && (e.Y > thumby - this.Height/2) && (e.Y < thumby + this.Height))
                {
                    thumbState = TrackBarThumbState.Hot;
                }
                else
                {
                    thumbState = TrackBarThumbState.Normal;
                }
            }
            this.Invalidate();
        }

        private void XTrackBar_MouseUp(object sender, MouseEventArgs e)
        {
            Capture = false;
            thumbState = TrackBarThumbState.Normal;
            this.Invalidate();
        }

        private void XTrackBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left)
            {
                Capture = false;
                return;
            }
            if ((e.X > thumbX) && (e.X < (thumbX + thumbwidth))
                && (e.Y > thumby) && (e.Y < (thumby + this.Height)))
            {
                Capture = true;
            }
            else
            {
                XTrackBar_MouseMove(sender, e);
            }
        }

        public new event Controller.VOIDVOID OnScroll;

        private void XTrackBar_Resize(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        private void XTrackBar_MouseLeave(object sender, EventArgs e)
        {
            if(!Capture){
                thumbState = TrackBarThumbState.Normal;
                this.Invalidate();
            }
        }
    }
}
