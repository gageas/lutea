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
        private const int SIZE = 200;
        private const int WM_COMMAND = 0x0111;
        private const int THBN_CLICKED = 0x1800;

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

        private DefaultUIForm mainForm;
        private Image cover = new Bitmap(SIZE, SIZE);
        public PseudoMainForm(DefaultUIForm mainForm)
        {
            this.mainForm = mainForm;
            this.Text = mainForm.Text;
            this.mainForm.TextChanged += (_, __) => this.Text = mainForm.Text;
            this.Icon = mainForm.Icon;
            Controller.onTrackChange += new Controller.VOIDINT(Controller_onTrackChange);
            Controller.onElapsedTimeChange += new Controller.VOIDINT(Controller_onElapsedTimeChange);
            InitializeComponent();
        }

        void Controller_onElapsedTimeChange(int second)
        {
            if (TaskbarExt != null)
            {
                TaskbarExt.Taskbar.SetProgressState(this.mainForm.Handle, TaskbarExtension.TbpFlag.Normal);
                TaskbarExt.Taskbar.SetProgressValue(this.mainForm.Handle, (ulong)second, (ulong)Controller.Current.Length);
            }
        }

        void Controller_onTrackChange(int sec)
        {
            var img = CoverArtView.GetCoverArtOrAlternativeImage();
            cover = Gageas.Lutea.Util.ImageUtil.GetResizedImageWithoutPadding(img, SIZE, SIZE);
            using (var g = Graphics.FromImage(cover))
            {
                g.DrawRectangle(Pens.Silver, 0, 0, cover.Width - 1, cover.Height - 1);
                g.DrawRectangle(Pens.Gray, 1, 1, cover.Width - 2, cover.Height - 2);
            }
            this.Size = cover.Size;
            this.Invalidate();
            if (TaskbarExt != null)
            {
                TaskbarExt.Taskbar.SetProgressState(this.mainForm.Handle, TaskbarExtension.TbpFlag.NoProgress);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.DrawImage(cover, 0, 0);
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
