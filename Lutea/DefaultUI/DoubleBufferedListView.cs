using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    class DoubleBufferedListView : ListView
    {
        public DoubleBufferedListView()
        {
            this.DoubleBuffered = true;
        }
    }
}
