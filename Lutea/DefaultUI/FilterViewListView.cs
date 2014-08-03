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
        public delegate void SelectEventHandler(int colid, string[] items);
        public event SelectEventHandler SelectEvent;
        public bool MetaTableMode = false;
        private bool SupplessFilterViewSelectChangeEvent = false;
        private Yomigana yomigana;
        public FilterViewListView(Yomigana yomigana)
            : base()
        {
            this.yomigana = yomigana;
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
            this.SelectEvent += (c, vals) =>
            {
                if (SupplessFilterViewSelectChangeEvent)
                {
                    SupplessFilterViewSelectChangeEvent = false;
                    return;
                }
                Controller.CreatePlaylist(GetQueryString());
            };
            this.MouseClick += (oo, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    SupplessFilterViewSelectChangeEvent = true;
                }
            };
        }

        private void listView2_Resize(object sender, EventArgs e)
        {
            this.Columns[1].Width = TextRenderer.MeasureText(" 88888", this.Font).Width;
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
                    SelectEvent.Invoke((int)this.Parent.Tag, values);
                }
            }
        }

        private void correctToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedItems.Count > 0)
            {
                if (SelectedItems[0].Tag == null) return;
                string src = SelectedItems[0].Tag.ToString();
                var lead = yomigana.GetLeadingChars(src);
                if (string.IsNullOrEmpty(lead) || lead.Length == 1) return;
                var correctdialog = new YomiCorrect(SelectedItems[0].Tag.ToString(), yomigana);
                correctdialog.ShowDialog();
            }
        }

        /// <summary>
        /// FilterViewを更新する。ごちゃごちゃしてるのでなんとかしたい
        /// </summary>
        /// <param name="o"></param>
        public void SetupContents(string textForSelected)
        {
            this.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            this.ContextMenuStrip.Items.Add("読み修正", null, correctToolStripMenuItem_Click);

            ListViewItem selected = null;
            var colid = (int)this.Parent.Tag;
            var col = Controller.Columns[colid];
            try
            {
                IEnumerable<KeyValuePair<string, int>> cf = Controller.FetchColumnValueMultipleValue(col.Name, null);

                Dictionary<char, ListViewGroup> groups = new Dictionary<char, ListViewGroup>();
                groups.Add('\0', new ListViewGroup(" " + col.LocalText));

                int count_sum = 0;
                List<ListViewItem> items = new List<ListViewItem>();

                foreach (var e in cf)
                {
                    string name = e.Key;
                    var count = e.Value;
                    char leading_letter = '\0';
                    string header = "";
                    if (col.MappedTagField == "DATE")
                    {
                        int year = 0;
                        int.TryParse(name.Substring(0, Math.Min(4, name.Length)), out year);
                        leading_letter = (char)year;  // .Netのcharは16bitなので、yearの数値表現をそのままつっこめる 問題ないはず
                        header = year.ToString();
                    }
                    else // tagDate以外のとき
                    {
                        leading_letter = yomigana.GetFirst(name);
                        header = leading_letter == '\0' ? " その他" : leading_letter.ToString();
                    }
                    // 新しいグループを追加
                    if (!groups.ContainsKey(leading_letter))
                    {
                        groups.Add(leading_letter, new ListViewGroup(header));
                    }
                    var item = new ListViewItem(new string[] { name, "" + count });
                    item.ToolTipText = name + "\n" + count + "項目";
                    item.Group = groups[leading_letter];
                    item.Tag = name;
                    if (name == textForSelected) selected = item;
                    items.Add(item);
                    count_sum += count;
                }
                var item_allFiles = new ListViewItem(new string[] { "すべて", count_sum.ToString() });
                item_allFiles.Group = groups['\0'];
                item_allFiles.Tag = null;
                items.Add(item_allFiles);

                var grpList = groups.Select((_) => _.Value).OrderBy((_) => _.Header).ToArray();
                this.Invoke((MethodInvoker)(() =>
                {
                    Parent.Enabled = false;
                    BeginUpdate();
                    Items.Clear();
                    Groups.AddRange(grpList);
                    Items.AddRange(items.ToArray());
                    createFilterIndex(grpList);
                    EndUpdate();
                    if (selected != null)
                    {
                        selected.Selected = true;
                        selected.EnsureVisible();
                    }
                    Parent.Enabled = true;
                }));
            }
            catch (Exception e) { Logger.Log(e.ToString()); }
            yomigana.Flush();
        }

        private void createFilterIndex(ICollection<ListViewGroup> grps)
        {
            ToolStripMenuItem toolstrip_index_other = new ToolStripMenuItem("その他");
            ToolStripMenuItem toolstrip_index_num = new ToolStripMenuItem("数字");
            ToolStripMenuItem toolstrip_index_alpha = new ToolStripMenuItem("A-Z");
            ToolStripMenuItem toolstrip_index_kana_a = new ToolStripMenuItem("あ");
            ToolStripMenuItem toolstrip_index_kana_k = new ToolStripMenuItem("か");
            ToolStripMenuItem toolstrip_index_kana_s = new ToolStripMenuItem("さ");
            ToolStripMenuItem toolstrip_index_kana_t = new ToolStripMenuItem("た");
            ToolStripMenuItem toolstrip_index_kana_n = new ToolStripMenuItem("な");
            ToolStripMenuItem toolstrip_index_kana_h = new ToolStripMenuItem("は");
            ToolStripMenuItem toolstrip_index_kana_m = new ToolStripMenuItem("ま");
            ToolStripMenuItem toolstrip_index_kana_y = new ToolStripMenuItem("や");
            ToolStripMenuItem toolstrip_index_kana_r = new ToolStripMenuItem("ら");
            ToolStripMenuItem toolstrip_index_kana_w = new ToolStripMenuItem("わ");

            var kanas = new ToolStripMenuItem[]{
                    toolstrip_index_kana_a,
                    toolstrip_index_kana_k,
                    toolstrip_index_kana_s,
                    toolstrip_index_kana_t,
                    toolstrip_index_kana_n,
                    toolstrip_index_kana_h,
                    toolstrip_index_kana_m,
                    toolstrip_index_kana_y,
                    toolstrip_index_kana_r,
                    toolstrip_index_kana_w,
                };

            this.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            var charTypes = new ToolStripMenuItem[]{
                    toolstrip_index_num,
                    toolstrip_index_alpha,
                    toolstrip_index_kana_a,
                    toolstrip_index_kana_k,
                    toolstrip_index_kana_s,
                    toolstrip_index_kana_t,
                    toolstrip_index_kana_n,
                    toolstrip_index_kana_h,
                    toolstrip_index_kana_m,
                    toolstrip_index_kana_y,
                    toolstrip_index_kana_r,
                    toolstrip_index_kana_w,

                    toolstrip_index_other,
                };
            this.ContextMenuStrip.Items.AddRange(charTypes);

            foreach (var e in kanas.Concat(charTypes))
            {
                var self = e; // ブロック内に参照コピー
                e.Enabled = false;
                e.Click += (x, y) => self.DropDownItems[0].PerformClick();
            }

            foreach (ListViewGroup grp in grps)
            {
                char c = grp.Header[0];
                if (c == ' ') continue;
                ToolStripMenuItem target = toolstrip_index_other;
                if ('A' <= c && 'Z' >= c)
                {
                    target = toolstrip_index_alpha;
                }
                else if ('0' <= c && '9' >= c)
                {
                    target = toolstrip_index_num;
                }
                else if ('あ' <= c && 'お' >= c) target = toolstrip_index_kana_a;
                else if ('か' <= c && 'こ' >= c) target = toolstrip_index_kana_k;
                else if ('さ' <= c && 'そ' >= c) target = toolstrip_index_kana_s;
                else if ('た' <= c && 'と' >= c) target = toolstrip_index_kana_t;
                else if ('な' <= c && 'の' >= c) target = toolstrip_index_kana_n;
                else if ('は' <= c && 'ほ' >= c) target = toolstrip_index_kana_h;
                else if ('ま' <= c && 'も' >= c) target = toolstrip_index_kana_m;
                else if ('や' <= c && 'よ' >= c) target = toolstrip_index_kana_y;
                else if ('ら' <= c && 'ろ' >= c) target = toolstrip_index_kana_r;
                else if ('わ' <= c && 'ん' >= c) target = toolstrip_index_kana_w;
                int index = grp.Items[0].Index;
                var item = grp.Items[0];
                var last = grps.Last().Items[grps.Last().Items.Count - 1].Index; // 最後のグループの最後の項目
                target.Enabled = true;
                if (target.OwnerItem != null)
                    target.OwnerItem.Enabled = true;
                target.DropDownItems.Add(grp.Header, null, (e, obj) =>
                {
                    this.ContextMenuStrip.Hide();
                    this.EnsureVisible(last);
                    this.EnsureVisible(index);
                });
            }
        }

        public string GetQueryString()
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
            var items_all = "'" + String.Join("', '", values) + "'";
            return "SELECT file_name FROM list WHERE any(" + Controller.Columns[(int)this.Parent.Tag].Name + ", " + items_all + ");";
        }
    }
}