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
        public const Int32 HDF_SORTDOWN = 0x0200;
        public const Int32 HDF_SORTUP = 0x0400;
        public const UInt32 HDI_FORMAT = 0x0004;
        public const UInt32 HDM_GETITEM = 0x120b;
        public const UInt32 HDM_SETITEM = 0x120c;
        public const UInt32 LVM_GETHEADER = 0x101f;

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

        public DoubleBufferedListView()
        {
            this.DoubleBuffered = true;
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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 uMsg, IntPtr wParam, ref HDITEM lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 uMsg, IntPtr wParam, IntPtr lParam);
    }
}
