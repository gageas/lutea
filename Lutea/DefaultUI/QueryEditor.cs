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
    public partial class QueryEditor : Form
    {
        private PlaylistEntryFile q;
        private string directory;
        private DynamicPlaylistTreeView parent;
        public QueryEditor(PlaylistEntryFile original, DynamicPlaylistTreeView parent)
        {
            if (original == null) throw new ArgumentNullException();
            this.q = original;
            this.directory = original.directory;
            this.parent = parent;
            InitializeComponent();
            this.textBox1.Text = original.Name;
            this.textBox2.Text = original.sql.Replace("\n", @"\n");
        }
        public QueryEditor(string directory_path, DynamicPlaylistTreeView parent)
        {
            this.directory = directory_path;
            this.parent = parent;
            InitializeComponent();
            this.textBox2.Text = Gageas.Lutea.Core.Controller.LatestPlaylistQueryExpanded.Replace("\n", @"\n");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int sortBy = 0;
            int sortOrder = 0;
            if (q != null)
            {
                q.Delete();
                sortBy = q.sortBy;
                sortOrder = q.sortOrder;
            }
            var name = textBox1.Text;
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                name.Replace(c,'_');
            }
            var new_q = new PlaylistEntryFile(this.directory, name, textBox2.Text, sortBy, sortOrder);
            new_q.Save();
            this.Close();
            parent.reloadDynamicPlaylist();
        }
    }
}
