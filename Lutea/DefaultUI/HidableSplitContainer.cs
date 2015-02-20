using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace Gageas.Lutea.DefaultUI
{
    class HidableSplitContainer : SplitContainer
    {
        public int BackupDistance { get; set; }

        public event Action Closed;

        public event Action Opened;

        public HidableSplitContainer()
        {
            SplitterWidth = 10;
            MouseClick += MouseClickHandler;
            Paint += PaintHandler;
            SplitterMoved += (_, __) => { if(Created)Panel2.Controls[0].Select(); };
            ResizeRedraw = true;
        }

        public void Close()
        {
            if (SplitterDistance == 0) return;
            BackupDistance = SplitterDistance;
            SplitterDistance = 0;
            if (Closed != null) Closed.Invoke();
        }

        public void Open()
        {
            if (BackupDistance == 0) BackupDistance = 100;
            if (SplitterDistance == BackupDistance) return;
            SplitterDistance = BackupDistance;
            if (Opened != null) Opened.Invoke();
        }

        private void MouseClickHandler(object sender, MouseEventArgs e)
        {
            // 移動できるsplitterの場合は左クリックは移動の挙動を優先
            if (e.Button == System.Windows.Forms.MouseButtons.Left && !IsSplitterFixed) return;

            if (SplitterDistance == 0)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        private void PaintHandler(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawLine(SystemPens.ControlDark, 10, SplitterDistance + 5, Width / 2 - 20, SplitterDistance + 5);
            e.Graphics.DrawLine(SystemPens.ControlDark, Width - 10, SplitterDistance + 5, Width / 2 + 20, SplitterDistance + 5);
            e.Graphics.FillRectangle(SystemBrushes.ControlDark, Width / 2 - 10 - e.ClipRectangle.X, SplitterDistance + 4, 2, 2);
            e.Graphics.FillRectangle(SystemBrushes.ControlDark, Width / 2, SplitterDistance + 4, 2, 2);
            e.Graphics.FillRectangle(SystemBrushes.ControlDark, Width / 2 + 10, SplitterDistance + 4, 2, 2);
        }
    }
}
