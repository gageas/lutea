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
        bool Completed = false;
        public ImportForm(string path, bool fastMode)
        {
            InitializeComponent();
            myImporter = new Importer(path, fastMode);
            myImporter.SetMaximum_read += new Controller.VOIDINT((i) => { if (!this.IsDisposed)this.Invoke((MethodInvoker)(() => { progressBar1.Maximum = i; })); });
            myImporter.Step_read += new Controller.VOIDVOID(() => { if (!this.IsDisposed)this.Invoke((MethodInvoker)(() => { progressBar1.PerformStep(); })); });
            myImporter.Message += new Importer.Message_event((str) => { if (!this.IsDisposed)this.Invoke((MethodInvoker)(() => { textBox1.Text = str; })); });
            myImporter.Complete += new Controller.VOIDVOID(() => { Completed = true; if (!this.IsDisposed)this.Invoke((MethodInvoker)(() => { this.Close(); this.Dispose(); })); });
        }

        public void Start()
        {
            myImporter.Start();
        }

        private void ImportForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Completed)
            {
                myImporter.Abort();
            }
            Completed = true;
        }
    }
}
