using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using Gageas.Lutea.Core;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.DefaultUI
{
    public class FilterViewListView : ListView
    {
        public delegate void SelectEventHandler(DBCol col, string[] items);
        public event SelectEventHandler SelectEvent;
        public FilterViewListView()
            : base()
        {
            this.FullRowSelect = true;
            this.Dock = DockStyle.Fill;
            this.HeaderStyle = ColumnHeaderStyle.None;
            this.View = System.Windows.Forms.View.Details;
            this.Columns.Add("Name");
            this.Columns.Add("");
            this.ShowItemToolTips = true;
            this.Columns[1].TextAlign = HorizontalAlignment.Right;
            this.Resize += new System.EventHandler(this.listView2_Resize);
            this.SelectedIndexChanged += new EventHandler(FilterViewListView_SelectedIndexChanged);
            this.GridLines = false;
            this.DoubleBuffered = true;
//            this.ContextMenuStrip = new ContextMenuStrip();
        }

        private void listView2_Resize(object sender, EventArgs e)
        {
            this.Columns[1].Width = 45;
            this.Columns[0].Width = this.ClientSize.Width - this.Columns[1].Width - 2;
        }

        // ref. http://stackoverflow.com/questions/86793/how-to-avoid-thousands-of-needless-listview-selectedindexchanged-events
        Timer changeDelayTimer = null;
        private void FilterViewListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.changeDelayTimer == null)
            {
                this.changeDelayTimer = new Timer();
                this.changeDelayTimer.Tick += ChangeDelayTimerTick;
                this.changeDelayTimer.Interval = 1; //200ms is what Explorer uses
            }
            this.changeDelayTimer.Enabled = false;
            this.changeDelayTimer.Enabled = true;
        }
        private void ChangeDelayTimerTick(object sender, EventArgs e)
        {
            this.changeDelayTimer.Enabled = false;
            this.changeDelayTimer.Dispose();
            this.changeDelayTimer = null;

            int c = this.SelectedItems.Count;
            if (c > 0)
            {
                if (SelectEvent != null)
                {
                    string[] values = new string[c];
                    for (int i = 0; i < c; i++)
                    {
                        values[i] = this.SelectedItems[i].Text;
                    }
                    SelectEvent.Invoke((DBCol)this.Parent.Tag, values);
                }
            }
        }

        public string getQueryString()
        {
            int c = this.SelectedItems.Count;
            string[] values = new string[c];
            for (int i = 0; i < c; i++)
            {
                values[i] = (string)this.SelectedItems[i].Tag;
            }

            if (values.Contains(null)) return "SELECT * FROM list;";

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = values[i].EscapeSingleQuotSQL();
            }
            var items_all = "'" + String.Join("' , '", values) + "'";
            //            this.textBox1.Text = "SELECT * FROM list WHERE " + col.ToString() + " IN (" + items_all + ");";
            return "SELECT * FROM list WHERE " + ((DBCol)this.Parent.Tag).ToString() + " IN (" + items_all + ");";
        }
    }
}