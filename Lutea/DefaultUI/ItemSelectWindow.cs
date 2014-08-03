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
    public partial class ItemSelectWindow : Form
    {
        public string[] Candidates;
        public string[] InitialSelected;
        public string[] Results { get { return listBox2.Items.Cast<string>().ToArray(); } }
        public ItemSelectWindow()
        {
            InitializeComponent();
        }

        private void ItemSelectWindow_Load(object sender, EventArgs e)
        {
            listBox1.Items.AddRange(Candidates.Except(InitialSelected ?? new string[1]).ToArray());
            if (InitialSelected != null)
            {
                listBox2.Items.AddRange(InitialSelected);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count == 0) return;
            var item = listBox1.SelectedItem;
            listBox1.Items.Remove(item);
            listBox2.Items.Add(item);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (listBox2.SelectedItems.Count == 0) return;
            var item = listBox2.SelectedItem;
            listBox2.Items.Remove(item);
            listBox1.Items.Add(item);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (listBox2.SelectedItems.Count == 0) return;
            var idx = listBox2.SelectedIndex;
            if (idx == 0) return;
            var item = listBox2.SelectedItem;
            listBox2.Items.Remove(item);
            listBox2.Items.Insert(idx - 1, item);
            listBox2.SelectedItem = item;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (listBox2.SelectedItems.Count == 0) return;
            var idx = listBox2.SelectedIndex;
            if (idx == listBox2.Items.Count - 1) return;
            var item = listBox2.SelectedItem;
            listBox2.Items.Remove(item);
            listBox2.Items.Insert(idx + 1, item);
            listBox2.SelectedItem = item;
        }
    }
}
