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

//        private int mousex;
//        private int mousey;

        private int thumby = 0;

        private int thumbwidth = 10;
        private int thumbheight = 20;

        private int padx = 7;
        private int pady = 0;

//        private bool captured = false;

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
                if ((value >= Min) && (value <= Max))
                {
                    _value = value;
                    this.Invalidate();
                }
            }
        }
        #endregion

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
            Rectangle track = new Rectangle(padx, 10 + pady, innerWidth - 1, 5 );
            Rectangle thumb = new Rectangle(thumbX, 3 + pady, thumbwidth, 20);
            if (TrackBarRenderer.IsSupported)
            {
                TrackBarRenderer.DrawHorizontalTrack(g, track);
                TrackBarRenderer.DrawHorizontalThumb(g, thumb, thumbState);
            }
            else
            {
                g.DrawRectangle(Pens.Black, track);
                g.DrawRectangle(Pens.Black, thumb);
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
                if ((e.X > thumbX - thumbwidth/2) && (e.X < thumbX + thumbwidth) && (e.Y > thumby - thumbheight/2) && (e.Y < thumby + thumbheight))
                {
                    thumbState = TrackBarThumbState.Hot;
                }
                else
                {
                    thumbState = TrackBarThumbState.Normal;
                }
            }
//            Logger.Log(""+this.Value);
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
                && (e.Y > thumby) && (e.Y < (thumby + thumbheight)))
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
    }
}
