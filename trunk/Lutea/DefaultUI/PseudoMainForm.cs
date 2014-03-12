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
        private int HEIGHT { get { return WIDTH / 3; } }
        private int PADDING { get { return (int)(HEIGHT * 0.05); } }
        private int BORDER { get { return (int)(HEIGHT * 0.025); } }

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
        /// カバーアート画像のキャッシュ
        /// </summary>
        private Image cover;

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
                var img = Controller.Current.CoverArtImage();
                if (img == null)
                {
                    cover = null;
                }
                else
                {
                    cover = Util.ImageUtil.GetResizedImageWithoutPadding(img, HEIGHT - PADDING - PADDING, HEIGHT - PADDING - PADDING);
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

        private void RenderContents(Graphics g, int width, int height, int size, int padding)
        {
            var fmt = new StringFormat(StringFormatFlags.FitBlackBox | StringFormatFlags.NoClip);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
            var rsideRect = new Rectangle(0, (int)(size * 0.13) + padding, width - padding, height - padding - padding);

            var appRect = new Rectangle(rsideRect.X, padding, rsideRect.Width, (int)(rsideRect.Height * 0.09));
            var appName = "Lutea audio player";
            var appFont = new Font(this.Font.Name, appRect.Height, FontStyle.Regular, GraphicsUnit.Pixel, 0x00, false);
            g.DrawString(appName, appFont, Brushes.Black, appRect, fmt);

            var albumRect = new Rectangle(rsideRect.X, rsideRect.Y + padding, rsideRect.Width, (int)(rsideRect.Height * 0.14));
            g.DrawString(Controller.Current.MetaData("tagAlbum"), new Font(this.Font.Name, albumRect.Height, FontStyle.Regular, GraphicsUnit.Pixel, 0x00, false), Brushes.Black, albumRect, fmt);

            var titleRect = new Rectangle(rsideRect.X, albumRect.Bottom + padding, rsideRect.Width, (int)(rsideRect.Height * 0.17));
            g.DrawString(Controller.Current.MetaData("tagTitle"), new Font(this.Font.Name, titleRect.Height, FontStyle.Bold, GraphicsUnit.Pixel, 0x00, false), Brushes.Black, titleRect, fmt);

            var artistRect = new Rectangle(rsideRect.X, titleRect.Bottom + padding, rsideRect.Width, (int)(rsideRect.Height * 0.14));
            g.DrawString(Controller.Current.MetaData("tagArtist"), new Font(this.Font.Name, artistRect.Height, FontStyle.Regular, GraphicsUnit.Pixel, 0x00, false), Brushes.Black, artistRect, fmt);
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
                // OS側でサムネイルを縮小させると汚いので自前でオーバーサンプリング描画する
                var left = PADDING + (cover == null ? 0 : cover.Width + BORDER);
                using (var bg = new Bitmap((Width - left) * OVERSAMPLE, Height * OVERSAMPLE))
                using (var g = Graphics.FromImage(bg))
                {
                    RenderContents(g, bg.Width, bg.Height, HEIGHT * OVERSAMPLE, PADDING * OVERSAMPLE);
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(bg, left, 0, Width - left, Height);
                }
                var barHeihgt = (int)(HEIGHT * 0.08);
                var progressRect = new Rectangle(left, Height - barHeihgt - PADDING, Width - left - PADDING - 1, barHeihgt - 1);
                e.Graphics.DrawRectangle(Pens.Silver, progressRect);
                e.Graphics.FillRectangle(Brushes.Silver, new Rectangle(progressRect.X, progressRect.Y, (int)((progressRect.Width) * (Controller.Current.Position / Controller.Current.Length)), progressRect.Height));
                if (cover != null)
                {
                    e.Graphics.DrawImage(cover, PADDING, PADDING + (Height - PADDING - PADDING - cover.Height) / 2);
                }
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            mainForm.ActivateUI();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            mainForm.Close();
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
