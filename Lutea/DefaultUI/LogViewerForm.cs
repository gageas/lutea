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
    public partial class LogViewerForm : Form
    {
        Logger.LogEventHandler l;

        public LogViewerForm()
        {
            InitializeComponent();
        }

        private void LogViewerForm_Load(object sender, EventArgs e)
        {
            l = log => { if (log.Level == Logger.Level.Log || log.Level == Logger.Level.Error) this.AppendText(log.ToString()); };
            Logger.LogClient += l;
        }

        public void AppendText(String s)
        {
            if (this.IsDisposed) return;
            if (this.l == null) return;
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)(() => AppendText(s)));
            }
            else
            {
                this.textBox1.AppendText(s + "\r\n");
            }
        }

        private void LogViewerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (l != null)
            {
                Logger.LogClient -= l;
                l = null;
            }
        }

    }
}
