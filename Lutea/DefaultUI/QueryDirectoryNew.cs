﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Gageas.Lutea.DefaultUI
{
    public partial class QueryDirectoryNew : Form
    {
        DefaultUIForm form;
        PlaylistEntryDirectory parent;
        public QueryDirectoryNew(PlaylistEntryDirectory parent, DefaultUIForm form)
        {
            this.form = form;
            this.parent = parent;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            System.IO.Directory.CreateDirectory(parent.path + System.IO.Path.DirectorySeparatorChar + textBox1.Text);
            this.Close();
            form.reloadDynamicPlaylist();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}