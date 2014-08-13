using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    class SelectablePanel : Panel
    {
        public event EventHandler SelectedIndexChanged;
        public List<Control> TabPages = new List<Control>();

        public SelectablePanel()
        {
        }

        private int selectedIndex = -1;
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value < 0) value = 0;
                if (value >= TabPages.Count) return;
                if (value == selectedIndex) return;
                Controls.Clear();
                TabPages.ElementAt(value).Dock = DockStyle.Fill;
                Controls.Add(TabPages.ElementAt(value));
                selectedIndex = value;
                if (SelectedIndexChanged != null) SelectedIndexChanged.Invoke(this, new EventArgs());
            }
        }

    }
}
