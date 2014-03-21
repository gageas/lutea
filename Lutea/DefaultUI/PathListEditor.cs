using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    public partial class FolderListEditor : Form
    {
        public bool Result
        {
            get;
            private set;
        }

        public IEnumerable<string> PathList
        {
            get
            {
                var ret = new string[listBox1.Items.Count];
                listBox1.Items.CopyTo(ret, 0);
                return ret;
            }
            set
            {
                listBox1.Items.Clear();
                if (value == null) return;
                listBox1.Items.AddRange(value.ToArray());
            }
        }

        public FolderListEditor()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                if (listBox1.Items.Contains(dialog.SelectedPath)) return;
                listBox1.Items.Add(dialog.SelectedPath);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count == 0) return;
            listBox1.Items.RemoveAt(listBox1.SelectedIndices[0]);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Result = true;
            Close();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Result = false;
            Close();
        }
    }
}
