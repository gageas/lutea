using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace Gageas.Lutea.DefaultUI
{
    public class DynamicPlaylistTreeView : TreeView
    {
        private System.ComponentModel.IContainer components;
        private ContextMenuStrip queryTreeViewContextMenuStrip1;
        private ToolStripMenuItem クエリ作成ToolStripMenuItem;
        private ToolStripMenuItem editToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem newDirectoryToolStripMenuItem;
        private ToolStripMenuItem reloadToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem 削除ToolStripMenuItem;
        private ToolStripMenuItem 名前の変更ToolStripMenuItem;
        private TreeNode previouslyClicked;

        public DynamicPlaylistTreeView()
        {
            ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.treeView1_ItemDrag);
            AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.queryView1_AfterSelect);
            NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeView1_NodeMouseClick);
            NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeView1_NodeMouseDoubleClick);
            DragDrop += new System.Windows.Forms.DragEventHandler(this.treeView1_DragDrop);
            DragOver += new System.Windows.Forms.DragEventHandler(this.treeView1_DragOver);
            InitializeComponent();
        }

        internal void reloadDynamicPlaylist()
        {
            TreeNode folder = new TreeNode("クエリ");
            string querydir = Gageas.Lutea.Core.Controller.UserDirectory + System.IO.Path.DirectorySeparatorChar + "query";
            if (!System.IO.Directory.Exists(querydir))
            {
                System.IO.Directory.CreateDirectory(querydir);
            }
            if (System.IO.Directory.GetFileSystemEntries(querydir).Length == 0)
            {
                new PlaylistEntryFile(querydir, "ランダム20曲", "SELECT * FROM list order by random() limit 20;", -1, 0).Save();
                new PlaylistEntryFile(querydir, "一週間以内に聞いた曲", "SELECT * FROM list WHERE current_timestamp64() - lastplayed <= 604800;", 18, 0).Save();
                new PlaylistEntryFile(querydir, "再生回数3回以上", "SELECT * FROM list WHERE playcount >= 3;", 17, 0).Save();
                new PlaylistEntryFile(querydir, "評価3つ星以上", "SELECT * FROM list WHERE rating >= 30;", 15, 0).Save();
                new PlaylistEntryFile(querydir, "3日以内に追加・更新された曲", "SELECT * FROM list WHERE current_timestamp64() - modify <= 259200;", -1, 0).Save();
                new PlaylistEntryFile(querydir, "まだ聞いていない曲", "SELECT * FROM list WHERE lastplayed = 0;", 18, 0).Save();
            }
            folder.Tag = new PlaylistEntryDirectory(querydir);
            folder.ImageIndex = 0;
            Nodes.Clear();
            DynamicPlaylist.Load(querydir, folder, null);
            Nodes.Add(folder);
            ExpandAll();
            previouslyClicked = null;
        }

        private void ExecQueryViewQuery(TreeNode node)
        {
            if (node == null) return;
            if (node.Tag == null) return;
            if (node.Tag is PlaylistEntryFile)
            {
                var ent = (PlaylistEntryFile)node.Tag;
                Gageas.Lutea.Core.Controller.CreatePlaylist(null);
                Gageas.Lutea.Core.Controller.CreatePlaylist(ent.sql);
            }
        }

        private void queryView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node != previouslyClicked)
            {
                ExecQueryViewQuery(e.Node);
            }
            previouslyClicked = e.Node;
        }

        /// <summary>
        /// QueryView上でクリックされたとき
        /// 右クリックメニューの準備等を行う
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            previouslyClicked = null;

            //　右クリックの場合
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                var node = e.Node;
                if (node != null && node.Tag != null)
                {
                    for (int i = 0; i < ContextMenuStrip.Items.Count; i++)
                    {
                        // 全てのメニューアイテムを一旦enableに
                        var item = ContextMenuStrip.Items[i];
                        item.Enabled = true;
                        if (item.Tag != null)
                        {
                            // ディレクトリノードの場合
                            if (node.Tag is PlaylistEntryDirectory)
                            {
                                if (item.Tag.ToString().Contains("-dir"))
                                {
                                    item.Enabled = false;
                                }
                            }

                            // ルートノード("クエリ"フォルダ)の場合
                            if (node.Level == 0)
                            {
                                if (item.Tag.ToString().Contains("-root"))
                                {
                                    item.Enabled = false;
                                }
                            }
                        }
                    }
                }
                // クエリの実行を抑制する
                previouslyClicked = e.Node;
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ExecQueryViewQuery(e.Node);
                // クエリの実行を抑制する
                previouslyClicked = e.Node;
            }

            // クリックされたノードをSelectedNodeに設定。
            SelectedNode = e.Node;
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DragDropEffects dde = DoDragDrop(e.Item, DragDropEffects.All);
        }

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TreeNode)))
            {
                TreeNode target = GetNodeAt(PointToClient(new Point(e.X, e.Y)));
                if (target != null && target.Tag != null && target.Tag is PlaylistEntryDirectory)
                {
                    e.Effect = DragDropEffects.Move;
                    SelectedNode = target;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TreeNode)))
            {
                TreeNode target = GetNodeAt(PointToClient(new Point(e.X, e.Y)));
                if (target != null && target.Tag != null && target.Tag is PlaylistEntryDirectory)
                {
                    PlaylistEntryDirectory ped = (PlaylistEntryDirectory)target.Tag;
                    var tomove = (TreeNode)e.Data.GetData(typeof(TreeNode));
                    PlaylistEntry src_pe = (PlaylistEntry)tomove.Tag;
                    if (target == tomove) return;
                    if (tomove.Parent == target) return;
                    System.IO.Directory.Move(src_pe.Path, ped.Path + System.IO.Path.DirectorySeparatorChar + System.IO.Path.GetFileName(src_pe.Path));
                    reloadDynamicPlaylist();
                }
            }
        }

        private void treeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var tn = e.Node;
            if (tn != null && tn.Tag is PlaylistEntryFile)
            {
                Gageas.Lutea.Core.Controller.CreatePlaylist(((PlaylistEntryFile)tn.Tag).sql, true);
            }
        }

        private void reloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            reloadDynamicPlaylist();
        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedNode != null)
            {
                if (SelectedNode.Tag is PlaylistEntryFile)
                {
                    new QueryEditor((PlaylistEntryFile)SelectedNode.Tag, this).ShowDialog();
                }
            }
        }

        private void newDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Tag == null) return;
            PlaylistEntryDirectory parent = null;
            if (SelectedNode.Tag is PlaylistEntryFile)
            {
                parent = (PlaylistEntryDirectory)SelectedNode.Parent.Tag;
            }
            else if (SelectedNode.Tag is PlaylistEntryDirectory)
            {
                parent = (PlaylistEntryDirectory)SelectedNode.Tag;
            }
            (new QueryDirectoryNew(parent, this)).Show();
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedNode == null) return;
            if (MessageBox.Show("以下の項目を削除します\n " + SelectedNode.Text, "クエリ項目の削除", MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.OK)
            {
                ((PlaylistEntry)SelectedNode.Tag).Delete();
            }
            reloadDynamicPlaylist();
        }

        private void RenameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedNode == null) return;
            new QueryRenameForm((PlaylistEntry)SelectedNode.Tag).ShowDialog();
            reloadDynamicPlaylist();
        }

        private void CreateQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (SelectedNode == null || SelectedNode.Tag == null) return;
            PlaylistEntryDirectory parent = null;
            if (SelectedNode.Tag is PlaylistEntryFile)
            {
                parent = (PlaylistEntryDirectory)SelectedNode.Parent.Tag;
            }
            else if (SelectedNode.Tag is PlaylistEntryDirectory)
            {
                parent = (PlaylistEntryDirectory)SelectedNode.Tag;
            }
            new QueryEditor(parent.Path, this).ShowDialog();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DynamicPlaylistTreeView));
            this.queryTreeViewContextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.クエリ作成ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.newDirectoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.reloadToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.削除ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.名前の変更ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.queryTreeViewContextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // queryTreeViewContextMenuStrip1
            // 
            this.queryTreeViewContextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.クエリ作成ToolStripMenuItem,
            this.editToolStripMenuItem,
            this.toolStripSeparator1,
            this.newDirectoryToolStripMenuItem,
            this.reloadToolStripMenuItem,
            this.toolStripSeparator2,
            this.削除ToolStripMenuItem,
            this.名前の変更ToolStripMenuItem});
            this.queryTreeViewContextMenuStrip1.Name = "queryTreeViewContextMenuStrip1";
            this.queryTreeViewContextMenuStrip1.Size = new System.Drawing.Size(166, 148);
            // 
            // クエリ作成ToolStripMenuItem
            // 
            this.クエリ作成ToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("クエリ作成ToolStripMenuItem.Image")));
            this.クエリ作成ToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.クエリ作成ToolStripMenuItem.Name = "クエリ作成ToolStripMenuItem";
            this.クエリ作成ToolStripMenuItem.Size = new System.Drawing.Size(165, 22);
            this.クエリ作成ToolStripMenuItem.Text = "クエリ作成...";
            this.クエリ作成ToolStripMenuItem.Click += new System.EventHandler(this.CreateQueryToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("editToolStripMenuItem.Image")));
            this.editToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(165, 22);
            this.editToolStripMenuItem.Tag = "-dir";
            this.editToolStripMenuItem.Text = "クエリ編集...";
            this.editToolStripMenuItem.Click += new System.EventHandler(this.editToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(162, 6);
            // 
            // newDirectoryToolStripMenuItem
            // 
            this.newDirectoryToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("newDirectoryToolStripMenuItem.Image")));
            this.newDirectoryToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.newDirectoryToolStripMenuItem.Name = "newDirectoryToolStripMenuItem";
            this.newDirectoryToolStripMenuItem.Size = new System.Drawing.Size(165, 22);
            this.newDirectoryToolStripMenuItem.Text = "新しいフォルダ...";
            this.newDirectoryToolStripMenuItem.Click += new System.EventHandler(this.newDirectoryToolStripMenuItem_Click);
            // 
            // reloadToolStripMenuItem
            // 
            this.reloadToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("reloadToolStripMenuItem.Image")));
            this.reloadToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.reloadToolStripMenuItem.Name = "reloadToolStripMenuItem";
            this.reloadToolStripMenuItem.Size = new System.Drawing.Size(165, 22);
            this.reloadToolStripMenuItem.Text = "最新の情報に更新";
            this.reloadToolStripMenuItem.Click += new System.EventHandler(this.reloadToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(162, 6);
            // 
            // 削除ToolStripMenuItem
            // 
            this.削除ToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("削除ToolStripMenuItem.Image")));
            this.削除ToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.削除ToolStripMenuItem.Name = "削除ToolStripMenuItem";
            this.削除ToolStripMenuItem.Size = new System.Drawing.Size(165, 22);
            this.削除ToolStripMenuItem.Tag = "-root";
            this.削除ToolStripMenuItem.Text = "削除...";
            this.削除ToolStripMenuItem.Click += new System.EventHandler(this.DeleteToolStripMenuItem_Click);
            // 
            // 名前の変更ToolStripMenuItem
            // 
            this.名前の変更ToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("名前の変更ToolStripMenuItem.Image")));
            this.名前の変更ToolStripMenuItem.ImageTransparentColor = System.Drawing.Color.Fuchsia;
            this.名前の変更ToolStripMenuItem.Name = "名前の変更ToolStripMenuItem";
            this.名前の変更ToolStripMenuItem.Size = new System.Drawing.Size(165, 22);
            this.名前の変更ToolStripMenuItem.Tag = "-root";
            this.名前の変更ToolStripMenuItem.Text = "名前の変更...";
            this.名前の変更ToolStripMenuItem.Click += new System.EventHandler(this.RenameToolStripMenuItem_Click);
            // 
            // DynamicPlaylistTreeView
            // 
            this.ContextMenuStrip = this.queryTreeViewContextMenuStrip1;
            this.LineColor = System.Drawing.Color.Black;
            this.queryTreeViewContextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }
    }
}
