using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

/* ref. http://d.hatena.ne.jp/Kazzz/20061106/p1 */

namespace Gageas.Lutea.DefaultUI
{
    public class ExtToolStrip : ToolStrip
    {
        const uint WM_MOUSEACTIVATE = 0x21;
        const uint MA_ACTIVATE = 1;
        const uint MA_ACTIVATEANDEAT = 2;

        private bool enableClickThrough = true;

        public bool EnableClickThrough
        {
            get { return this.enableClickThrough; }
            set { this.enableClickThrough = value; }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (this.enableClickThrough
                && m.Msg == WM_MOUSEACTIVATE && m.Result == (IntPtr)MA_ACTIVATEANDEAT)
            {
                m.Result = (IntPtr)MA_ACTIVATE;
            }
        }
    }

    public class ExtMenuStrip : MenuStrip
    {
        const uint WM_MOUSEACTIVATE = 0x21;
        const uint MA_ACTIVATE = 1;
        const uint MA_ACTIVATEANDEAT = 2;

        private bool enableClickThrough = true;

        public bool EnableClickThrough
        {
            get { return this.enableClickThrough; }
            set { this.enableClickThrough = value; }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (this.enableClickThrough
                && m.Msg == WM_MOUSEACTIVATE && m.Result == (IntPtr)MA_ACTIVATEANDEAT)
            {
                m.Result = (IntPtr)MA_ACTIVATE;
            }
        }
    }
}
