using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    public partial class FindDeadLinkDialog : Form
    {
        Thread th;
        public FindDeadLinkDialog(Form root)
        {
            InitializeComponent();
            var self = this;
            this.th = new Thread(() =>
            {
                try
                {
                    var dead_link = Controller.GetDeadLink((_) => self.Invoke((Action)(() => this.progressBar1.Maximum = _)), (_) => self.Invoke((Action)(() => this.progressBar1.Value = _)));
                    root.Invoke((Action)(() => { (new DeleteFilesDialog(dead_link.ToArray())).ShowDialog(root); }));
                    self.Invoke((Action)(() => { self.Close(); }));
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            });
            th.Priority = ThreadPriority.BelowNormal;
            th.Start();
        }

        private void FindDeadLink_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (th != null)
            {
                th.Interrupt();
            }
        }
    }
}
