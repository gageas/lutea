using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using Gageas.Lutea.Core;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.DefaultUI
{
    public class DynamicPlaylistTreeView : TreeView
    {
        private enum ImageIndexes { FOLDER, SINGLE_FILE, QUERY, MULTIPLE_FILE, ALBUM_DISC };
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
        private TreeNode RelatedItemsRoot = null;

        private TreeNode CreateTreeNode(string text, ImageIndexes imageIndex, IEnumerable<TreeNode> children = null, object tag = null, bool expand = false)
        {
            TreeNode tn;
            if (children == null)
            {
                tn = new TreeNode(text, (int)imageIndex, (int)imageIndex);
            }
            else
            {
                tn = new TreeNode(text, (int)imageIndex, (int)imageIndex, children.ToArray());
            }
            tn.Tag = tag;
            if (expand) tn.Expand();
            return tn;
        }

        /// <summary>
        /// データベースへの接続をプールするクラス
        /// </summary>
        private class DBConPool
        {
            private Gageas.Wrapper.SQLite3.SQLite3DB pooledConnection;
            public Gageas.Wrapper.SQLite3.SQLite3DB Get()
            {
                if (pooledConnection != null) return pooledConnection;
                pooledConnection = Controller.GetDBConnection();
                return pooledConnection;
            }
            public void Release()
            {
                pooledConnection.Dispose();
                pooledConnection = null;
            }
        }
        private DBConPool ConnectionPool = new DBConPool();

        public DynamicPlaylistTreeView()
        {
            ImageList = new ImageList();
            ImageList.ColorDepth = ColorDepth.Depth32Bit;
            ImageList.Images.Add(Shell32.GetShellIcon(  3, false)); //FOLDER
            ImageList.Images.Add(Shell32.GetShellIcon(116, false)); //SINGLE_FILE
            ImageList.Images.Add(Shell32.GetShellIcon( 55, false)); //QUERY
            ImageList.Images.Add(Shell32.GetShellIcon(128, false)); //MULTIPLE_FILE
            ImageList.Images.Add(Shell32.GetShellIcon( 40, false)); //ALBUM_DISC

            ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.treeView1_ItemDrag);
            AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.queryView1_AfterSelect);
            NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeView1_NodeMouseClick);
            NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.treeView1_NodeMouseDoubleClick);
            DragDrop += new System.Windows.Forms.DragEventHandler(this.treeView1_DragDrop);
            DragOver += new System.Windows.Forms.DragEventHandler(this.treeView1_DragOver);
            Gageas.Lutea.Core.Controller.onTrackChange += id =>
            {
                this.Invoke((Action)(() =>
                {
                    ResetRelatedTree();
                }));
            };
            Controller.onDatabaseUpdated += () =>
            {
                this.Invoke((Action)(() =>
                {
                    ResetRelatedTree();
                }));
            };
            InitializeComponent();
        }
        
        private IEnumerable<Tuple<string, string>> FetchFromDBAsTuple2(string sql)
        {
            using (var stmt = ConnectionPool.Get().Prepare(sql))
            {
                return stmt.EvaluateAll().Select(_ => new Tuple<string, string>((string)_[0], (string)_[1]));
            }
        }

        private IEnumerable<Tuple<string, string, string>> FetchFromDBAsTuple3(string sql)
        {
            using (var stmt = ConnectionPool.Get().Prepare(sql))
            {
                return stmt.EvaluateAll().Select(_ => new Tuple<string, string, string>((string)_[0], (string)_[1], (string)_[2]));
            }
        }

        private IEnumerable<Tuple<string, string, string, string>> FetchFromDBAsTuple4(string sql)
        {
            using (var stmt = ConnectionPool.Get().Prepare(sql))
            {
                return stmt.EvaluateAll().Select(_ => new Tuple<string, string, string, string>((string)_[0], (string)_[1], (string)_[2], (string)_[3]));
            }
        }

        private IEnumerable<KeyValuePair<string, string>> FetchRelatedAlbums(string artist)
        {
            var item_splitter = new char[] { '；', ';', '，', ',', '／', '/', '＆', '&', '・', '･', '、', '､', '（', '(', '）', ')', '\n', '\t' };
            var subArtists = artist.Split(item_splitter, StringSplitOptions.RemoveEmptyEntries);
            var q = String.Join(" OR ", (from __ in from _ in subArtists select _.LCMapUpper().Trim() select String.Format(__.Length > 1 ? @" LCMapUpper(tagArtist) LIKE '%{0}%' " : @" LCMapUpper(tagArtist) = '{0}' ", __.EscapeSingleQuotSQL())).ToArray());
            if (subArtists.Length > 0)
            {
                using (var stmt = ConnectionPool.Get().Prepare("SELECT tagAlbum,COUNT(*) FROM list WHERE tagAlbum IN (SELECT tagAlbum FROM list WHERE " + q + " ) GROUP BY tagAlbum ORDER BY COUNT(*) DESC;"))
                {
                    return stmt.EvaluateAll().Select(_ => new KeyValuePair<string, string>(_[0].ToString(), _[1].ToString())).Where(_ => !string.IsNullOrEmpty(_.Key));
                }
            }
            return new KeyValuePair<string, string>[0];
        }

        private TreeNode CreateRelatedAlbumTree()
        {
            return CreateTreeNode(
                text: "関連アルバム", 
                imageIndex: ImageIndexes.FOLDER, 
                expand: true,
                children: FetchRelatedAlbums(Controller.Current.MetaData("tagArtist")).Select((_) => CreateTreeNode(
                    text: _.Key, 
                    imageIndex: ImageIndexes.ALBUM_DISC, 
                    tag: new VirtualPlaylistEntry("SELECT * FROM list WHERE tagAlbum = '" + _.Key.EscapeSingleQuotSQL() + "';", -1, -0)
                ))
            );
        }

        private TreeNode CreateCurrentAlbumTree(string album)
        {
            return CreateTreeNode(
                text: album, 
                imageIndex: ImageIndexes.ALBUM_DISC,
                tag: new VirtualPlaylistEntry("SELECT * FROM list WHERE tagAlbum = '" + album.EscapeSingleQuotSQL() + "';", -1, -0),
                children: FetchFromDBAsTuple3("SELECT tagTitle, 0+tagTrackNumber, file_name FROM list WHERE tagAlbum = '" + album.EscapeSingleQuotSQL() + "' ORDER BY 0+tagTrackNumber ASC;").Select((_) => CreateTreeNode(
                    text: _.Item2 + ". " + _.Item1, 
                    imageIndex: ImageIndexes.SINGLE_FILE, 
                    tag: new VirtualPlaylistEntry("SELECT * FROM list WHERE file_name = '" + _.Item3.EscapeSingleQuotSQL() + "';", -1, -0)
                ))
            );
        }

        /// <summary>
        /// データベースカラムに対する関連トラックのリストを作る
        /// </summary>
        /// <param name="col"></param>
        /// <returns></returns>
        private TreeNode CreateTagBasedTree(Library.Column col)
        {
            // 再生中のトラックのタグの値をMultipleValuesで取得する
            var tagValues = Controller.FetchColumnValueMultipleValue(col.Name, "file_name='" + Controller.Current.Filename.EscapeSingleQuotSQL() + "'").Where(_ => !string.IsNullOrEmpty(_.Key));
            if (tagValues.Count() == 0) return null;

            var level1Node = CreateTreeNode(
                text: col.LocalText, 
                imageIndex: ImageIndexes.FOLDER,
                expand: true,
                children: tagValues.Select(tagValue =>
                {
                    var tracks = FetchFromDBAsTuple4("SELECT file_name, tagTitle, tagArtist, tagAlbum FROM list WHERE any(" + col.Name + ", '" + tagValue.Key.EscapeSingleQuotSQL() + "');");
                    return CreateTreeNode(
                        text: tagValue.Key + " (" + tracks.Count() + ")", 
                        imageIndex: ImageIndexes.MULTIPLE_FILE, 
                        tag: new VirtualPlaylistEntry("SELECT * FROM list WHERE any(" + col.Name + ", '" + tagValue.Key.EscapeSingleQuotSQL() + "');", -1, -0),
                        children: tracks.GroupBy(_ => _.Item4).Select(group => CreateTreeNode(
                            text: group.Key + " (" + group.Count() + ")", 
                            imageIndex: ImageIndexes.ALBUM_DISC,
                            children: group.Select(leafItem => CreateTreeNode(
                                text: leafItem.Item2 + " - " + leafItem.Item3, 
                                imageIndex: ImageIndexes.SINGLE_FILE, 
                                tag: "SELECT * FROM list WHERE file_name = '" + leafItem.Item1.EscapeSingleQuotSQL() + "');"
                            ))
                        ))
                    );
                })
            );
            ConnectionPool.Release();
            return level1Node;
        }

        private void ResetRelatedTree()
        {
            if (!Controller.IsPlaying)
            {
                if (RelatedItemsRoot != null) Nodes.Remove(RelatedItemsRoot);
                return;
            }
            var album = Controller.Current.MetaData("tagAlbum");
            var newRelatedItemsRoot = new TreeNode("再生中");
            newRelatedItemsRoot.Nodes.Add(CreateCurrentAlbumTree(album));
            newRelatedItemsRoot.Nodes.Add(CreateRelatedAlbumTree());

            var cols = Controller.Columns.Where(_ => _.IsTextSearchTarget).Where(_ => !(_.Name == "tagAlbum" || _.Name == "tagComment" || _.Name == "tagTitle"));
            foreach (var col in cols)
            {
                var node = CreateTagBasedTree(col);
                if (node != null)
                {
                    newRelatedItemsRoot.Nodes.Add(node);
                }
            }
            newRelatedItemsRoot.Expand();
            BeginUpdate();
            if (RelatedItemsRoot != null) Nodes.Remove(RelatedItemsRoot);
            RelatedItemsRoot = newRelatedItemsRoot;
            Nodes.Add(RelatedItemsRoot);
            EndUpdate();
        }

        internal void reloadDynamicPlaylist()
        {
            string querydir = Gageas.Lutea.Core.Controller.UserDirectory + System.IO.Path.DirectorySeparatorChar + "query";
            TreeNode rootNode = CreateTreeNode("クエリ", ImageIndexes.FOLDER, null, new PlaylistEntryDirectory(querydir));
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
            Nodes.Clear();
            DynamicPlaylist<TreeNode>.Load(querydir, rootNode,
            (parent, dir) =>
            {
                TreeNode dirtree = CreateTreeNode(System.IO.Path.GetFileNameWithoutExtension(dir), ImageIndexes.FOLDER, null, new PlaylistEntryDirectory(dir));
                parent.Nodes.Add(dirtree);
                return dirtree;
            },
            (parent, path, name, sql, sortBy, sortOrder) => {
                string playlistname = System.IO.Path.GetFileNameWithoutExtension(name);
                parent.Nodes.Add(CreateTreeNode(playlistname, ImageIndexes.QUERY, null, new PlaylistEntryFile(path, playlistname, sql, sortBy, sortOrder)));
            });
            Nodes.Add(rootNode);
            ExpandAll();
            ResetRelatedTree();
            previouslyClicked = null;
        }

        private void ExecQueryViewQuery(TreeNode node, bool playOnCreate = false)
        {
            if (node == null) return;
            if (node.Tag == null) return;
            if (node.Tag is PlaylistEntryFile)
            {
                var ent = (PlaylistEntryFile)node.Tag;
                Gageas.Lutea.Core.Controller.CreatePlaylist(null);
                Gageas.Lutea.Core.Controller.CreatePlaylist(ent.sql, playOnCreate);
            }
            if (node.Tag is VirtualPlaylistEntry)
            {
                var ent = (VirtualPlaylistEntry)node.Tag;
                Gageas.Lutea.Core.Controller.CreatePlaylist(null);
                Gageas.Lutea.Core.Controller.CreatePlaylist(ent.sql, playOnCreate);
            }
        }

        private void queryView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Action == TreeViewAction.Expand) return;
            if (e.Action == TreeViewAction.Collapse) return;
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

                        if (node.Tag is VirtualPlaylistEntry)
                        {
                            item.Enabled = false;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < ContextMenuStrip.Items.Count; i++)
                    {
                        // 全てのメニューアイテムを一旦enableに
                        var item = ContextMenuStrip.Items[i];
                        item.Enabled = false;
                    }
                }
                // クエリの実行を抑制する
                previouslyClicked = e.Node;
            // クリックされたノードをSelectedNodeに設定。
            SelectedNode = e.Node;
            }
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (e.Item == null) return;
            if (!(e.Item is TreeNode)) return;
            if (e.Item == Nodes[0]) return;
            if (((TreeNode)e.Item).Tag == null) return;
            if (((TreeNode)e.Item).Tag is VirtualPlaylistEntry) return;
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
            ExecQueryViewQuery(e.Node, true);
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
