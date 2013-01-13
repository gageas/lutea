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
    public partial class DeleteFilesDialog : Form
    {
        private string[] file_names;
        public DeleteFilesDialog(string[] file_names)
        {
            InitializeComponent();
            this.file_names = file_names;
            this.textBox1.Text = string.Join(System.Environment.NewLine, file_names.Select((_)=>{var tr = _.TrimEnd(); return tr + ", tr" + (_.Length - tr.Length);}).ToArray());
            this.textBox1.Select(0, 0);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
            Controller.removeItem(file_names);
        }
    }
}
