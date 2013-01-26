using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Gageas.Lutea.DefaultUI
{
    class DoubleBufferedListView : ListView
    {
        private const Int32 HDF_SORTDOWN = 0x0200;
        private const Int32 HDF_SORTUP = 0x0400;
        private const UInt32 HDI_FORMAT = 0x0004;
        private const UInt32 HDM_GETITEM = 0x120b;
        private const UInt32 HDM_SETITEM = 0x120c;
        private const UInt32 LVM_FIRST = 0x1000;
        private const UInt32 LVM_GETHEADER = 0x101f;
        private const UInt32 LVM_SETITEMSTATE = (LVM_FIRST + 43);
        private const UInt32 LVIF_STATE = 0x0008;
        private const UInt32 LVIS_SELECTED = 0x0002;
        private const int WM_SETFONT = 0x0030;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_ERASEBKGND = 0x0014;

        struct HDITEM
        {
            public UInt32 mask;
            public Int32 cxy;
            public String pszText;
            public IntPtr hbm;
            public Int32 cchTextMax;
            public Int32 fmt;
            public IntPtr lParam;
            public Int32 iImage;
            public Int32 iOrder;
            public UInt32 type;
            public IntPtr pvFilter;
            public UInt32 state;
        }


        struct LVITEM
        {
            public UInt32 mask;
            public Int32 iItem;
            public Int32 iSubItem;
            public UInt32 state;
            public UInt32 stateMask;
            public string pszText;
            public Int32 cchTextMax;
            public Int32 iImage;
            public IntPtr lParam;
            /* 以下略 */
        }

        public DoubleBufferedListView()
        {
            this.DoubleBuffered = true;
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_KEYDOWN:
                    // VirtualModeかつViewがSmallIcon or LargeIconのときShift+SPで落ちるのでSPキー入力を握りつぶす
                    if (this.VirtualMode == true && (View == System.Windows.Forms.View.SmallIcon || View == System.Windows.Forms.View.LargeIcon) && m.WParam == (IntPtr)0x20)
                    {
                        return;
                    }
                    break;
                case WM_ERASEBKGND:
                    m.Result = (IntPtr)1;
                    return;
            }
            base.WndProc(ref m);
        }

        public void SetHeaderFont(System.Drawing.Font font)
        {
            IntPtr hwndHdr = SendMessage(this.Handle, (0x1000 + 31), IntPtr.Zero, IntPtr.Zero);
            SendMessage(hwndHdr, WM_SETFONT, font.ToHfont(), (IntPtr)1);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 SetWindowTheme(IntPtr hWnd, String pszSubAppName, String pszSubIdList);

        public void SetExplorerStyle()
        {
            SetWindowTheme(this.Handle, "Explorer", null);
        }


        public void SetSortArrow(int column, SortOrder sortOrder)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)(() => {
                    this.SetSortArrow(column, sortOrder);
                }));
                return;
            }
            var pHeader = SendMessage(this.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);

            var pColumn = new IntPtr(column);
            var headerItem = new HDITEM { mask = HDI_FORMAT };

            SendMessage(pHeader, HDM_GETITEM, pColumn, ref headerItem);

            switch (sortOrder)
            {
                case SortOrder.Ascending:
                    headerItem.fmt &= ~HDF_SORTDOWN;
                    headerItem.fmt |= HDF_SORTUP;
                    break;
                case SortOrder.Descending:
                    headerItem.fmt &= ~HDF_SORTUP;
                    headerItem.fmt |= HDF_SORTDOWN;
                    break;
                case SortOrder.None:
                    headerItem.fmt &= ~(HDF_SORTDOWN | HDF_SORTUP);
                    break;
            }

            SendMessage(pHeader, HDM_SETITEM, pColumn, ref headerItem);
        }

        /// <summary>
        /// 全てのアイテムを選択状態にする
        /// ループで1つずつ設定すると遅いのでこれを使う
        /// </summary>
        public void SelectAllItems()
        {
            LVITEM lvitem = new LVITEM();
            lvitem.mask = LVIF_STATE;
            lvitem.state = LVIS_SELECTED;
            lvitem.stateMask = LVIS_SELECTED;
            SendMessage(this.Handle, LVM_SETITEMSTATE, (IntPtr)(-1), ref lvitem);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 uMsg, IntPtr wParam, ref LVITEM lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 uMsg, IntPtr wParam, ref HDITEM lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 uMsg, IntPtr wParam, IntPtr lParam);
    }
}
