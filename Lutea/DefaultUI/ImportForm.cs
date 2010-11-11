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
    public partial class ImportForm : Form
    {
        Importer myImporter;
        public ImportForm(string path)
        {
            InitializeComponent();
            myImporter = new Importer(path);
            myImporter.SetMaximum_import += new Controller.VOIDINT((i) => { this.Invoke((MethodInvoker)(() => { progressBar2.Maximum = i; })); });
            myImporter.SetMaximum_read += new Controller.VOIDINT((i) => { this.Invoke((MethodInvoker)(() => { progressBar1.Maximum = i; })); });
            myImporter.Step_import += new Controller.VOIDVOID(() => { this.Invoke((MethodInvoker)(() => { progressBar2.PerformStep(); })); });
            myImporter.Step_read += new Controller.VOIDVOID(() => { this.Invoke((MethodInvoker)(() => { progressBar1.PerformStep(); })); });
            myImporter.Message += new Importer.Message_event((str) => { this.Invoke((MethodInvoker)(() => { textBox1.Text = str; })); });
            myImporter.Complete += new Controller.VOIDVOID(() => { this.Invoke((MethodInvoker)(() => { this.Close(); this.Dispose(); })); });
        }

        public void Start()
        {
            myImporter.Start();
        }

        private void ImportForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            myImporter.Abort();
        }
    }
}
