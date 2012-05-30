using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Gageas.Lutea.Core;
using Gageas.Lutea.Library;

namespace Gageas.Lutea.DefaultUI
{
    public partial class DBCustomize : Form
    {
        private Wrap wrap;

        class Wrap
        {
            private Column[] extraColumns;

            public Column[] ExtraColumns
            {
                get { return extraColumns; }
                set { this.extraColumns = value; }
            }
        }

        public DBCustomize()
        {
            InitializeComponent();
            wrap = new Wrap();
            wrap.ExtraColumns = Gageas.Lutea.Core.Controller.ExtraColumns;
            this.propertyGrid1.SelectedObject = wrap;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show("再起動します\nよろしいですか？", "Library.DBのカスタマイズ");
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Controller.Reload(wrap.ExtraColumns);
            }
        }
    }
}
