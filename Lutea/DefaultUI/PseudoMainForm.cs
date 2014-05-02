using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    public partial class PseudoMainForm : Form
    {
        private const int OVERSAMPLE = 4;
        private const int WM_COMMAND = 0x0111;
        private const int THBN_CLICKED = 0x1800;

        private int WIDTH = 200;
        private int HEIGHT { get { return WIDTH / 2; } }
        private int PADDING { get { return (int)(HEIGHT * 0.05); } }
        private int BORDER { get { return (int)(HEIGHT * 0.025); } }

        public bool QuitOnClose = true;

        /// <summary>
        /// win7タスクバーに表示するボタンの画像リスト
        /// </summary>
        ImageList taskbarImageList;

        /// <summary>
        /// win7タスクバーに表示するボタンの配列
        /// </summary>
        TaskbarExtension.ThumbButton[] taskbarThumbButtons = new TaskbarExtension.ThumbButton[4];

        /// <summary>
        /// Windows7の拡張タスクバーを制御
        /// </summary>
        private TaskbarExtension TaskbarExt;

        /// <summary>
        /// メインウィンドウの参照
        /// </summary>
        private DefaultUIForm mainForm;

        /// <summary>
        /// 背景画像のキャッシュ
        /// </summary>
        private Image back = new Bitmap(1, 1);

        public PseudoMainForm(DefaultUIForm mainForm)
        {
            using (var bmp = new Bitmap(1,1))
            using (var g = Graphics.FromImage(bmp))
            {
                WIDTH = (int)(WIDTH * g.DpiX / 96);
            }
            this.mainForm = mainForm;
            this.BackColor = Color.Tan;
            this.DoubleBuffered = true;
            this.Text = mainForm.Text;
            this.mainForm.TextChanged += (_, __) => this.Text = mainForm.Text;
            this.Icon = mainForm.Icon;
            Controller.onTrackChange += new Controller.VOIDINT(Controller_onTrackChange);
            Controller.onElapsedTimeChange += new Controller.VOIDINT(Controller_onElapsedTimeChange);
            InitializeComponent();
            this.ClientSize = new Size(WIDTH, HEIGHT);
            this.Opacity = 0;
        }

        void Controller_onElapsedTimeChange(int second)
        {
            if (TaskbarExt != null)
            {
                this.mainForm.Invoke((MethodInvoker)(() =>
                {
                    TaskbarExt.Taskbar.SetProgressState(this.mainForm.Handle, TaskbarExtension.TbpFlag.Normal);
                    TaskbarExt.Taskbar.SetProgressValue(this.mainForm.Handle, (ulong)second, (ulong)Controller.Current.Length);
                }));
            }
            this.Invalidate();
        }

        void Controller_onTrackChange(int sec)
        {
            if (TaskbarExt != null)
            {
                var backCreate = new Bitmap(Width, Height);
                using (var gg = Graphics.FromImage(backCreate))
                {
                    var bgcolor = Color.White;
                    var img = Controller.Current.CoverArtImage();
                    if (img != null)
                    {
                        var coverx = Util.ImageUtil.GetResizedImageWithoutPadding(img, WIDTH, WIDTH * 2);
                        if (coverx != null)
                        {
                            var h = coverx.Height * (Width - PADDING - PADDING) / coverx.Width;
                            gg.DrawImage(coverx, 0, -(h - Height) / 3);
                        }
                        var onepx = Util.ImageUtil.GetResizedImageWithoutPadding(img, 1, 64);
                        bgcolor = ((Bitmap)onepx).GetPixel(0, 0);
                    }
                    else
                    {
                        gg.FillRectangle(Brushes.DarkSlateBlue, ClientRectangle);
                    }
                    gg.FillRectangle(new SolidBrush(Color.FromArgb(64, Color.White)), new Rectangle(0,(int)(Height*0.50),Width, Height));

                    gg.FillRectangle(Brushes.Tan, 0, 0, BORDER, Height);
                    gg.FillRectangle(Brushes.Tan, 0, 0, Width, BORDER);
                    gg.FillRectangle(Brushes.Tan, Width - BORDER, 0, BORDER, Height);
                    gg.FillRectangle(Brushes.Tan, 0, Height - BORDER, Width, BORDER);
                    // OS側でサムネイルを縮小させると汚いので自前でオーバーサンプリング描画する
                    using (var bg = new Bitmap((Width - BORDER) * OVERSAMPLE, Height * OVERSAMPLE))
                    using (var g = Graphics.FromImage(bg))
                    {
                        RenderContents(g, bg.Width, bg.Height, HEIGHT * OVERSAMPLE, PADDING * OVERSAMPLE / 2);
                        gg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        gg.DrawImage(bg, BORDER, 0, Width - BORDER, Height);
                    }
                }
                var _back = back;
                back = backCreate;
                if (_back != null)
                {
                    _back.Dispose();
                }
                this.mainForm.Invoke((MethodInvoker)(() =>
                {
                    this.Invalidate();
                    TaskbarExt.Taskbar.SetProgressState(this.mainForm.Handle, TaskbarExtension.TbpFlag.NoProgress);
                }));
            }
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

        private void RenderBorderedString(string text, Graphics g, int x, int y, int width, int height, Font font, StringFormat fmt)
        {
            Rectangle rect;
            var delta = OVERSAMPLE / 2;
            var brush1 = Brushes.White;
            var brush2 = Brushes.Black;

            rect = new Rectangle(x + delta, y + delta, width, height);
            g.DrawString(text, font, brush1, rect, fmt);
            rect = new Rectangle(x + delta, y - delta, width, height);
            g.DrawString(text, font, brush1, rect, fmt);
            rect = new Rectangle(x - delta, y + delta, width, height);
            g.DrawString(text, font, brush1, rect, fmt);
            rect = new Rectangle(x - delta, y - delta, width, height);
            g.DrawString(text, font, brush1, rect, fmt);

            rect = new Rectangle(x + delta * 2, y, width, height);
            g.DrawString(text, font, brush1, rect, fmt);
            rect = new Rectangle(x - delta * 2, y, width, height);
            g.DrawString(text, font, brush1, rect, fmt);
            rect = new Rectangle(x, y + delta * 2, width, height);
            g.DrawString(text, font, brush1, rect, fmt);
            rect = new Rectangle(x, y - delta * 2, width, height);
            g.DrawString(text, font, brush1, rect, fmt);

            rect = new Rectangle(x, y, width, height);
            g.DrawString(text, font, brush2, rect, fmt);
        }

        private void RenderContents(Graphics g, int width, int height, int size, int padding)
        {
            var fmt = new StringFormat(StringFormatFlags.FitBlackBox | StringFormatFlags.NoClip);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
            var rsideRect = new Rectangle(0, (int)(size * 0.45) + padding, width - padding, height - padding - padding);

            var albumRect = new Rectangle(rsideRect.X + 3, rsideRect.Y + padding + 3, rsideRect.Width, (int)(rsideRect.Height * 0.11));
            RenderBorderedString(Controller.Current.MetaData("tagAlbum"), g, albumRect.X, albumRect.Y, albumRect.Width, albumRect.Height, new Font(this.Font.Name, albumRect.Height, FontStyle.Bold, GraphicsUnit.Pixel, 0x00, false), fmt);

            var titleRect = new Rectangle(rsideRect.X, albumRect.Bottom + padding, rsideRect.Width, (int)(rsideRect.Height * 0.14));
            RenderBorderedString(Controller.Current.MetaData("tagTitle"), g, titleRect.X, titleRect.Y, titleRect.Width, titleRect.Height, new Font(this.Font.Name, titleRect.Height, FontStyle.Bold, GraphicsUnit.Pixel, 0x00, false), fmt);

            var artistRect = new Rectangle(rsideRect.X, titleRect.Bottom + padding, rsideRect.Width, (int)(rsideRect.Height * 0.12));
            RenderBorderedString(Controller.Current.MetaData("tagArtist"), g, artistRect.X, artistRect.Y, artistRect.Width, artistRect.Height, new Font(this.Font.Name, artistRect.Height, FontStyle.Bold, GraphicsUnit.Pixel, 0x00, false), fmt);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.FillRectangle(Brushes.White, BORDER, BORDER, Width - BORDER - BORDER, Height - BORDER - BORDER);
            if (!Controller.IsPlaying)
            {
                e.Graphics.DrawIcon(this.Icon, (this.Width - this.Icon.Width) / 2, (this.Height - this.Icon.Height) / 2);
                return;
            }
            else
            {
                e.Graphics.DrawImage(back, 0, 0);
                var barHeihgt = (int)(HEIGHT * 0.08);
                var progressRect = new Rectangle(PADDING, Height - barHeihgt - PADDING, Width - PADDING - PADDING - 1, barHeihgt - 1);
                var x = (int)((progressRect.Width - BORDER - BORDER) * (Controller.Current.Position / Controller.Current.Length)) + BORDER;
                e.Graphics.FillPolygon(Brushes.White, new Point[] { new Point(x - PADDING, Height), new Point(x + PADDING, Height), new Point(x, Height - PADDING) });
                e.Graphics.DrawPolygon(Pens.Black, new Point[] { new Point(x - PADDING, Height), new Point(x + PADDING, Height), new Point(x, Height - PADDING) });
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            mainForm.ActivateUI();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (QuitOnClose)
            {
                mainForm.Close();
            }
            base.OnClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            bool omitBaseProc = false;
            if (TaskbarExt != null)
            {
                switch (m.Msg)
                {
                    case WM_COMMAND:
                        if (((int)m.WParam & 0xffff0000) >> 16 == THBN_CLICKED)
                        {
                            switch ((int)m.WParam & 0xffff)
                            {
                                case 0:
                                    Controller.Stop();
                                    break;
                                case 1:
                                    Controller.PrevTrack();
                                    break;
                                case 2:
                                    Controller.TogglePause();
                                    break;
                                case 3:
                                    Controller.NextTrack();
                                    break;
                                default:
                                    Logger.Log((int)m.WParam & 0xff);
                                    break;
                            }
                            omitBaseProc = true;
                        }
                        break;
                    default:
                        if (m.Msg == TaskbarExt.WM_TBC)
                        {
                            TaskbarExt.ThumbBarAddButtons(taskbarThumbButtons);
                            m.Result = IntPtr.Zero;
                            omitBaseProc = true;
                            break;
                        }
                        break;
                }
            }
            if (!omitBaseProc) base.WndProc(ref m);
        }

        private void PseudoCoverView_Load(object sender, EventArgs e)
        {
            TaskbarExt = new TaskbarExtension(this);
            TaskbarExt.Taskbar.RegisterTab(this.Handle, mainForm.Handle);
            TaskbarExt.Taskbar.SetTabOrder(this.Handle, mainForm.Handle);
            TaskbarExt.Taskbar.SetTabProperties(this.Handle, TaskbarExtension.StpFlag.STPF_USEAPPPEEKALWAYS);
            TaskbarExt.Taskbar.SetThumbnailTooltip(this.mainForm.Handle, "");
            ResetTaskbarExtButtonImage();
        }

        private void ResetTaskbarExtButtonImage()
        {
            try
            {
                taskbarImageList = new ImageList();
                taskbarImageList.ImageSize = new System.Drawing.Size(16, 16);
                taskbarImageList.ColorDepth = ColorDepth.Depth32Bit;
                var images = new Bitmap[] { Properties.Resources.stop, Properties.Resources.prev, Properties.Resources.pause, Properties.Resources.next };
                foreach (var img in images)
                {
                    img.MakeTransparent(Color.Magenta);
                }
                taskbarImageList.Images.AddRange(images);
                TaskbarExt.ThumbBarSetImageList(taskbarImageList);
                taskbarThumbButtons[0] = new TaskbarExtension.ThumbButton() { iID = 0, szTip = "Stop", iBitmap = 0, dwMask = TaskbarExtension.ThumbButtonMask.Flags | TaskbarExtension.ThumbButtonMask.ToolTip | TaskbarExtension.ThumbButtonMask.Bitmap, dwFlags = TaskbarExtension.ThumbButtonFlags.Enabled };
                taskbarThumbButtons[1] = new TaskbarExtension.ThumbButton() { iID = 1, szTip = "Prev", iBitmap = 1, dwMask = TaskbarExtension.ThumbButtonMask.Flags | TaskbarExtension.ThumbButtonMask.ToolTip | TaskbarExtension.ThumbButtonMask.Bitmap, dwFlags = TaskbarExtension.ThumbButtonFlags.Enabled };
                taskbarThumbButtons[2] = new TaskbarExtension.ThumbButton() { iID = 2, szTip = "Play/Pause", iBitmap = 2, dwMask = TaskbarExtension.ThumbButtonMask.Flags | TaskbarExtension.ThumbButtonMask.ToolTip | TaskbarExtension.ThumbButtonMask.Bitmap, dwFlags = TaskbarExtension.ThumbButtonFlags.Enabled };
                taskbarThumbButtons[3] = new TaskbarExtension.ThumbButton() { iID = 3, szTip = "Next", iBitmap = 3, dwMask = TaskbarExtension.ThumbButtonMask.Flags | TaskbarExtension.ThumbButtonMask.ToolTip | TaskbarExtension.ThumbButtonMask.Bitmap, dwFlags = TaskbarExtension.ThumbButtonFlags.Enabled };
            }
            catch { }
        }

    }
}
