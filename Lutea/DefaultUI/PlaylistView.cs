using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using Gageas.Lutea.Core;
using Gageas.Lutea.Library;
using Gageas.Lutea.DefaultUI;

namespace Gageas.Lutea.DefaultUI
{
    /// <summary>
    /// Playlistを表示するListView
    /// </summary>
    class PlaylistView : DoubleBufferedListView
    {
        #region 定数
        /// <summary>
        /// カバーアートのマージン(Vertical, Horizontal)
        /// </summary>
        private const int CoverArtMargin = 3;

        /// <summary>
        /// 文字のマージン(Vertical)
        /// </summary>
        private const int TextMargin = 2;

        /// <summary>
        /// 省略文字
        /// </summary>
        private const String PostTruncateString = "…";
        #endregion 

        #region フィールド
        /// <summary>
        /// Ratingの☆を描画するクラス
        /// </summary>
        private RatingRenderer ratingRenderer;

        /// <summary>
        /// 仮想ListViewのダミーアイテム
        /// </summary>
        private ListViewItem dummyPlaylistViewItem = new ListViewItem(new string[99]);

        /// <summary>
        /// 親フォーム
        /// </summary>
        private DefaultUIForm form;

        /// <summary>
        /// カバーアートローダ
        /// </summary>
        private BackgroundCoverartsLoader backgroundCoverartLoader;

        /// <summary>
        /// 背景色の有効にするかどうか
        /// </summary>
        private bool useColor;

        /// <summary>
        /// トラックナンバーの書式
        /// </summary>
        private DefaultUIPreference.TrackNumberFormats trackNumberFormat;

        /// <summary>
        /// カバーアートを表示するかどうか
        /// </summary>
        private bool showCoverArt;

        /// <summary>
        /// グループ表示をするかどうか
        /// </summary>
        private bool showGroup;

        /// <summary>
        /// カラム区切りを表示するかどうか
        /// </summary>
        private bool showVerticalGrid;

        /// <summary>
        /// 強調表示(再生中)の行
        /// </summary>
        private int emphasizedRowId = -1;

        /// <summary>
        /// 直前に押されたキーのKeyEventArgs
        /// </summary>
        private KeyEventArgs previousPressedKey = null;

        /// <summary>
        /// データベースのカラムのキャッシュ
        /// </summary>
        private Column[] dbColumnsCache = null;

        /// <summary>
        /// 現在のフォントでの省略文字のサイズのキャッシュ
        /// </summary>
        Size sizeOfTruncateStringCache = new Size();

        /// <summary>
        /// ListViewのColumnHeaderを表示順にソートしたキャッシュ
        /// </summary>
        private List<ColumnHeader> cols = null;

        /// <summary>
        /// tagAlbumのカラムID
        /// </summary>
        private int colIdOfAlbum;

        /// <summary>
        /// file_nameのカラムID
        /// </summary>
        private int colIdOfFilename;

        /// <summary>
        /// Object数
        /// </summary>
        private int numObjects = 0;

        /// <summary>
        /// アルバム連続数のキャッシュ
        /// </summary>
        private int[] tagAlbumContinuousCount;

        /// <summary>
        /// 各Columnのでデフォルトの幅を定義
        /// </summary>
        private Dictionary<string, int> defaultColumnDisplayWidth = new Dictionary<string, int>(){
            {"tagTracknumber",130},
            {"tagTitle",120},
            {"tagArtist",120},
            {"tagAlbum",80},
            {"tagComment",120},
            {"rating",84},
        };

        /// <summary>
        /// 表示するColumnの順番
        /// </summary>
        private Dictionary<string, int> columnOrder = new Dictionary<string, int>();

        /// <summary>
        /// 表示するColumnの幅
        /// </summary>
        private Dictionary<string, int> columnWidth = new Dictionary<string, int>();

        /// <summary>
        /// ViewIDからObjectIDへのマッピング。
        /// nullの場合,ViewIDとObjectIDが直接対応する
        /// </summary>
        private int[] v2oMap;

        /// <summary>
        /// ObjectIDからViewIDへのマッピング。
        /// nullの場合,ObjectIDとViewIDが直接対応する
        /// </summary>
        private int[] o2vMap;
        #endregion


        #region プロパティ
        /// <summary>
        /// プレイリストにアルバムごとの背景色を有効にするか
        /// </summary>
        public bool UseColor
        {
            get
            {
                return this.useColor;
            }
            set
            {
                if (this.useColor == value) return;
                this.useColor = value;
                this.Invalidate();
            }
        }

        /// <summary>
        /// トラック番号の書式
        /// </summary>
        public DefaultUIPreference.TrackNumberFormats TrackNumberFormat
        {
            get
            {
                return this.trackNumberFormat;
            }
            set
            {
                if (this.trackNumberFormat == value) return;
                this.trackNumberFormat = value;
                this.Invalidate();
            }
        }

        /// <summary>
        /// カバーアートを表示するか
        /// </summary>
        public bool ShowCoverArt
        {
            get
            {
                return this.showCoverArt;
            }
            set
            {
                if (this.showCoverArt == value) return;
                this.showCoverArt = value;
                this.Invalidate();
            }
        }

        /// <summary>
        /// グループとして表示
        /// </summary>
        public bool ShowGroup
        {
            get
            {
                return this.showGroup;
            }
            set
            {
                if (this.showGroup == value) return;
                this.showGroup = value;
                this.Invalidate();
            }
        }

        /// <summary>
        /// カラム区切りを表示
        /// </summary>
        public bool ShowVerticalGrid
        {
            get
            {
                return this.showVerticalGrid;
            }
            set
            {
                if (this.showVerticalGrid == value) return;
                this.showVerticalGrid = value;
                this.Invalidate();
            }
        }

        /// <summary>
        /// カバーアート表示の大きさ
        /// </summary>
        public int CoverArtSize
        {
            get
            {
                return CoverArtSizeWithPad - CoverArtMargin * 2;
            }
        }

        public int CoverArtSizeWithPad
        {
            get
            {
                return CoverArtLineNum * ItemHeight;
            }
        }

        /// <summary>
        /// ColumnOrder
        /// </summary>
        internal Dictionary<string, int> ColumnOrder
        {
            set { this.columnOrder = new Dictionary<string, int>(value); }
        }

        /// <summary>
        /// ColumnWidth
        /// </summary>
        internal Dictionary<string, int> ColumnWidth
        {
            set { this.columnWidth = new Dictionary<string, int>(value); }
        }

        private int itemHeight = int.MaxValue;
        private int ItemHeight
        {
            set
            {
                if (value != itemHeight)
                {
                    itemHeight = value;
                    backgroundCoverartLoader.Reset(CoverArtSize);
                    RefreshPlaylist(false, emphasizedRowId);
                }
            }
            get
            {
                return itemHeight;
            }
        }

        int coverArtLineNum = 4;
        public int CoverArtLineNum
        {
            get
            {
                return coverArtLineNum;
            }
            set
            {
                if (value < 2) return;
                coverArtLineNum = value;
            }
        }

        public string lastSelectedString
        {
            get;
            private set;
        }

        public int lastSelectedColumnId
        {
            get;
            private set;
        }

        public ContextMenuStrip HeaderContextMenu { get; set; }
        #endregion

        #region Publicメソッド
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public PlaylistView()
        {
            // レーティングの☆描画準備
            this.ratingRenderer = new RatingRenderer(@"components\rating_on.gif", @"components\rating_off.gif");

            backgroundCoverartLoader = new BackgroundCoverartsLoader(CoverArtSize);

            // イベントハンドラの登録
            this.DrawItem += playlistView_DrawItem;
            this.ItemDrag += playlistView_ItemDrag;
            this.MouseMove += playlistView_MouseMove;
            this.MouseClick += playlistView_MouseClick;
            this.ColumnClick += playlistView_ColumnClick;
            this.DoubleClick += playlistView_DoubleClick;
            this.KeyDown += playlistView_KeyDown;
            this.DrawColumnHeader += playlistView_DrawColumnHeader;
            this.ColumnReordered += PlaylistView_ColumnReordered;
            this.RetrieveVirtualItem += playlistView_RetrieveVirtualItem;
            backgroundCoverartLoader.Complete += (indexes =>
            {
                Invoke((Action)(() =>
                {
                    foreach (var index in indexes)
                    {
                        if (index < VirtualListSize)
                        {
                            RedrawItems(index, index, true);
                        }
                    }
                }));
            });
        }
        
        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="form">Form</param>
        /// <param name="columns">データベースのカラム</param>
        public void Setup(DefaultUIForm form, Column[] columns)
        {
            this.form = form;
            this.dbColumnsCache = columns;
            colIdOfAlbum = Controller.GetColumnIndexByName("tagAlbum");
            colIdOfFilename = Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name);
        }

        /// <summary>
        /// 表示するカラムをリセット
        /// </summary>
        /// <param name="displayColumns">表示するカラム</param>
        public void ResetColumns(IEnumerable<string> displayColumns)
        {
            displayColumns = displayColumns.OrderBy((_) => columnOrder.ContainsKey(_) ? columnOrder[_] : columnOrder.Count).ToArray();

            // backup order/width
            if (Columns.Count > 0)
            {
                foreach (ColumnHeader col in Columns)
                {
                    if (col.Tag == null) continue;
                    var colName = dbColumnsCache[(int)col.Tag].Name;
                    columnOrder[colName] = col.DisplayIndex;
                    columnWidth[colName] = Math.Max(10, col.Width);
                }
            }

            Clear();

            if (ShowCoverArt)
            {
                var cover = new ColumnHeader();
                cover.Width = CoverArtSizeWithPad;
                cover.Text = "";
                Columns.Add(cover);
            }

            foreach (string coltext in displayColumns)
            {
                var colheader = new ColumnHeader();
                var col = Controller.GetColumnIndexByName(coltext);
                if (col == -1) continue;
                colheader.Text = dbColumnsCache[col].LocalText;
                colheader.Tag = col;
                if (columnWidth.ContainsKey(coltext))
                {
                    colheader.Width = columnWidth[coltext];
                }
                else
                {
                    if (defaultColumnDisplayWidth.ContainsKey(dbColumnsCache[col].Name))
                    {
                        colheader.Width = defaultColumnDisplayWidth[dbColumnsCache[col].Name];
                    }
                }
                if (dbColumnsCache[col].Name == LibraryDBColumnTextMinimum.statBitrate)
                {
                    colheader.TextAlign = HorizontalAlignment.Right;
                }
                Columns.Add(colheader);
            }

            foreach (ColumnHeader colheader in Columns)
            {
                if (colheader.Tag == null) continue;
                var colName = dbColumnsCache[(int)colheader.Tag].Name;
                if (columnOrder.ContainsKey(colName))
                {
                    try
                    {
                        colheader.DisplayIndex = columnOrder[colName];
                    }
                    catch
                    {
                        colheader.DisplayIndex = Columns.Count - 1;
                    }
                }
                else
                {
                    colheader.DisplayIndex = Columns.Count - 1;
                }
            }
            cols = null;
        }

        /// <summary>
        /// 実際の描画フォントを設定
        /// </summary>
        /// <param name="font">フォント</param>
        public override void SetHeaderFont(Font font)
        {
            base.SetHeaderFont(font);
            sizeOfTruncateStringCache.Width = 0;
            sizeOfTruncateStringCache.Height = 0;
        }

        /// <summary>
        /// プレイリストの内容が変わったことを通知
        /// </summary>
        /// <param name="moveToIndex">SelectItemをindexに移動</param>
        /// <param name="index">選択されているindex</param>
        public void RefreshPlaylist(bool moveToIndex, int index)
        {
            // プレイリストが更新されてアイテムの位置が変わったらカバーアート読み込みキューを消去
            backgroundCoverartLoader.ClearQueue();

            if (moveToIndex)
            {
                if (index < 0)
                {
                    SelectItemIndirect(0);
                }
                else
                {
                    SelectItemIndirect(index);
                }
            }
            tagAlbumContinuousCount = Controller.GetTagAlbumContinuousCount();
            if (tagAlbumContinuousCount != null)
            {
                numObjects = tagAlbumContinuousCount.Length;
            }
            else
            {
                numObjects = 0;
            }
            genMapTable(tagAlbumContinuousCount);
            this.VirtualListSize = v2oMap != null ? v2oMap.Length : numObjects;
            this.Refresh();
            if (moveToIndex)
            {
                if (index < 0)
                {
                    SelectItemIndirect(0);
                }
                else
                {
                    SelectItemIndirect(index);
                }
            }
            this.EmphasizeRowIndirect(index);
        }

        /// <summary>
        /// 選択されているオブジェクトのIDの配列を返す
        /// </summary>
        /// <returns></returns>
        public int[] GetSelectedObjects()
        {
            List<int> result = new List<int>();
            foreach (int viewid in SelectedIndices)
            {
                result.Add(getObjectIDByViewID(viewid));
            }
            return result.Distinct().ToArray();
        }

        /// <summary>
        /// オブジェクトを選択状態にする
        /// </summary>
        /// <param name="oid"></param>
        public void SelectItemIndirect(int oid)
        {
            var vid = getViewIDByObjectID(oid);
            if (vid < 0) return;
            if (vid >= VirtualListSize) return;
            EnsureVisibleIndirect(oid);
            SelectItem(vid);
        }

        /// <summary>
        /// オブジェクトを画面内に表示する
        /// </summary>
        /// <param name="oid"></param>
        public void EnsureVisibleIndirect(int oid)
        {
            var vid = getViewIDByObjectID(oid);
            if (vid < 0) return;
            if (vid >= VirtualListSize) return;
            EnsureVisible(vid);
            if (getIndexInGroup(vid) == 1)
            {
                if (vid > 0)
                {
                    EnsureVisible(vid - 1);
                }
            }
        }

        /// <summary>
        /// 指定した行を強調表示(再生中)
        /// </summary>
        /// <param name="oid">行</param>
        public void EmphasizeRowIndirect(int oid)
        {
            var prev = emphasizedRowId;
            emphasizedRowId = oid;

            try
            {
                var vid = getViewIDByObjectID(prev);
                if (vid != -1)
                {
                    this.RedrawItems(vid, vid, true);
                }
            }
            catch { }

            try
            {
                var vid = getViewIDByObjectID(emphasizedRowId);
                if (vid != -1)
                {
                    this.RedrawItems(vid, vid, true);
                }
            }
            catch { }
        }

        /// <summary>
        /// 選択されているアイテムのレートを変更
        /// </summary>
        /// <param name="rate">レート</param>
        public void SetRatingForSelectedItems(int rate)
        {
            if (this.SelectedIndices.Count > 0)
            {
                Controller.SetRating(
                        GetSelectedObjects()
                        .Select(_ => Controller.GetPlaylistRowColumn(_, colIdOfFilename)).ToArray(), rate);
            }
        }

        /// <summary>
        /// 選択されている項目を再生
        /// </summary>
        public void playFirstSelectedItem()
        {
            if (this.SelectedIndices.Count > 0)
            {
                Controller.PlayPlaylistItem(getObjectIDByViewID(SelectedIndices[0]));
            }
        }
        #endregion

        #region Privateメソッド
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            // WM_CONTEXTMENU
            if (m.Msg == 0x7b)
            {
                if (m.WParam != this.Handle)
                {
                    // 自身のHWINDOW宛てじゃないWM_CONTEXTMENUが来たらColumnHeader宛てのWM_CONTEXTMENUと判断する
                    // ref. http://stackoverflow.com/questions/17838494/listview-contextmenustrip-for-column-headers
                    if (HeaderContextMenu != null) HeaderContextMenu.Show(Control.MousePosition);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        #region 選択トラック移動
        /// <summary>
        /// 次のアルバムの先頭トラックを選択
        /// </summary>
        private void moveToNextAlbum()
        {
            int oid = getCurrentObjectID();
            if (oid == -1) return;
            do
            {
                oid++;
            } while ((oid < numObjects) && (tagAlbumContinuousCount[oid] != 0));
            if (oid == numObjects)
            {
                oid = numObjects - 1;
            }
            SelectItemIndirect(oid);
            EnsureVisible(Math.Min(getViewIDByObjectID(oid) + CoverArtLineNum, VirtualListSize - 1));
        }

        /// <summary>
        /// 前のアルバムの先頭トラックを選択
        /// </summary>
        private void moveToPrevAlbum()
        {
            int oid = getCurrentObjectID();
            if (oid <= 0) return;
            do
            {
                oid--;
            } while ((oid >= 0) && (tagAlbumContinuousCount[oid] != 0));
            SelectItemIndirect(oid);
            EnsureVisible(Math.Max(getViewIDByObjectID(oid) - 1, 0));
        }

        /// <summary>
        /// 次のトラックを選択
        /// </summary>
        private void moveToNextTrack()
        {
            var oid = getCurrentObjectID() + 1;
            if (oid == numObjects) oid--;
            SelectItemIndirect(oid);
            EnsureVisible(Math.Min(getViewIDByObjectID(oid) + CoverArtLineNum, VirtualListSize - 1));
        }

        /// <summary>
        /// 前のトラックを選択
        /// </summary>
        private void moveToPrevTrack()
        {
            int oid = getCurrentObjectID();
            if (oid < 0) oid = numObjects - 1;
            if (oid == 0) return;
            SelectItemIndirect(oid - 1);
            if (getViewIDByObjectID(oid - 1) > 0)
            {
                EnsureVisible(getViewIDByObjectID(oid - 1) - 1);
            }
        }

        /// <summary>
        /// 先頭トラックを選択
        /// </summary>
        private void moveToFirstTrack()
        {
            this.SelectItemIndirect(0);
            EnsureVisible(0);
        }

        /// <summary>
        /// 最後のトラックを選択
        /// </summary>
        private void moveToLastTrack()
        {
            if (numObjects == 0) return;
            this.SelectItemIndirect(numObjects - 1);
            this.EnsureVisible(VirtualListSize - 1);
        }
        #endregion

        #region イベントハンドラ
        /// <summary>
        /// カラムを並び替えた時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void PlaylistView_ColumnReordered(object sender, ColumnReorderedEventArgs e)
        {
            cols = null;
        }

        /// <summary>
        /// キーが押された時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_KeyDown(object sender, KeyEventArgs e)
        {
            Logger.Debug("Down" + e.KeyCode + e.KeyData + e.KeyValue);
            switch (e.KeyCode)
            {
                case Keys.PageUp:
                    if (ShowGroup)
                    {
                        moveToPrevAlbum();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    break;
                case Keys.PageDown:
                    if (ShowGroup)
                    {
                        moveToNextAlbum();
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                    break;
                case Keys.Up:
                    moveToPrevTrack();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Down:
                    moveToNextTrack();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Home:
                    moveToFirstTrack();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.End:
                    moveToLastTrack();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Return:
                    playFirstSelectedItem();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.J:
                    if (e.Modifiers == Keys.Control) // Ctrl + J
                    {
                        playFirstSelectedItem();
                    }
                    else if (e.Modifiers == Keys.Shift) // Shift + J
                    {
                        moveToNextAlbum();
                    }
                    else // J
                    {
                        moveToNextTrack();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.M:
                    if (e.Modifiers == Keys.Control) // Ctrl + M
                    {
                        playFirstSelectedItem();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.K:
                    if (e.Modifiers == Keys.Shift) // Shift + K
                    {
                        moveToPrevAlbum();
                    }
                    else
                    {
                        moveToPrevTrack();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.H:
                    moveToFirstTrack();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.L:
                    moveToLastTrack();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.G:
                    if (e.Modifiers == Keys.Shift)
                    {
                        moveToLastTrack();
                    }
                    else
                    {
                        if (previousPressedKey != null && previousPressedKey.KeyCode == Keys.G && previousPressedKey.Modifiers == 0)
                        {
                            moveToFirstTrack();
                        }
                    }
                    break;
                case Keys.Escape:
                    form.SelectQueryComboBox(false);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.N:
                    if (e.Modifiers == Keys.Control)
                    {
                        Controller.NextTrack();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.P:
                    if (e.Modifiers == Keys.Control)
                    {
                        Controller.PrevTrack();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.A:
                    if (e.Modifiers == Keys.Control)
                    {
                        this.SelectAllItems();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.OemQuestion: // FIXME: / キーはこれでいいの？
                    form.SelectQueryComboBox(true);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D1:
                    SetRatingForSelectedItems(10);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D2:
                    SetRatingForSelectedItems(20);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D3:
                    SetRatingForSelectedItems(30);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D4:
                    SetRatingForSelectedItems(40);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D5:
                    SetRatingForSelectedItems(50);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D0:
                    SetRatingForSelectedItems(0);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
            previousPressedKey = e;
        }

        /// <summary>
        /// マウスが移動した時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_MouseMove(object sender, MouseEventArgs e)
        {
            var item = this.GetItemAt(e.X, e.Y);
            if (item == null) return;
            var sub = item.GetSubItemAt(e.X, e.Y);
            if (sub == null) return;
            if (this.Columns[item.SubItems.IndexOf(sub)].Tag == null) return;
            if ((!isDummyRow(item.Index)) 
                && (dbColumnsCache[(int)(this.Columns[item.SubItems.IndexOf(sub)].Tag)].Type == Library.LibraryColumnType.Rating))
            {
                if (this.Cursor != Cursors.Hand)
                {
                    this.Cursor = Cursors.Hand;
                }
            }
            else if (this.Cursor != Cursors.Arrow)
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        /// <summary>
        /// クリックした時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_MouseClick(object sender, MouseEventArgs e)
        {
            lastSelectedString = null;
            lastSelectedColumnId = -1;
            var item = this.GetItemAt(e.X, e.Y);
            if (item == null) return;
            var sub = item.GetSubItemAt(e.X, e.Y);
            if (sub == null) return;
            if (sub.Tag == null) return;
            switch (e.Button)
            {
                case System.Windows.Forms.MouseButtons.Right:
                    var oid = getObjectIDByViewID(item.Index);
                    lastSelectedColumnId = (int)(this.Columns[item.SubItems.IndexOf(sub)].Tag);
                    lastSelectedString = Controller.GetPlaylistRowColumn(oid, lastSelectedColumnId);
                    break;
                case System.Windows.Forms.MouseButtons.Left:
                    if (isDummyRow(item.Index)) return;
                    int starwidth = ratingRenderer.EachWidth;
                    if (dbColumnsCache[(int)(this.Columns[item.SubItems.IndexOf(sub)].Tag)].Type != Library.LibraryColumnType.Rating) return;
                    var x = e.X - TextMargin;
                    int rate = 0;
                    if (item.GetSubItemAt(x - starwidth * 4, e.Y) == sub)
                    {
                        rate = 50;
                    }
                    else if (item.GetSubItemAt(x - starwidth * 3, e.Y) == sub)
                    {
                        rate = 40;
                    }
                    else if (item.GetSubItemAt(x - starwidth * 2, e.Y) == sub)
                    {
                        rate = 30;
                    }
                    else if (item.GetSubItemAt(x - starwidth * 1, e.Y) == sub)
                    {
                        rate = 20;
                    }
                    else if (item.GetSubItemAt(x - starwidth / 2, e.Y) == sub)
                    {
                        rate = 10;
                    }
                    Controller.SetRating(Controller.GetPlaylistRowColumn(getObjectIDByViewID(item.Index), colIdOfFilename), rate);
                    break;
            }
        }

        /// <summary>
        /// ドラッグ＆ドロップ時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            try
            {
                var count = this.SelectedIndices.Count;
                if (count < 1) return;
                List<string> filenames = new List<string>();
                foreach (int viewid in SelectedIndices)
                {
                    filenames.Add(Controller.GetPlaylistRowColumn(getObjectIDByViewID(viewid), colIdOfFilename));
                }
                DataObject dataObj = new DataObject(DataFormats.FileDrop, filenames.Distinct().ToArray());
                DoDragDrop(dataObj, DragDropEffects.Copy);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        /// <summary>
        /// カラムをクリックした時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (this.Columns[e.Column].Tag == null) return;
            Controller.SetSortColumn(dbColumnsCache[(int)this.Columns[e.Column].Tag].Name);
        }

        /// <summary>
        /// ダブルクリックした時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_DoubleClick(object sender, EventArgs e)
        {
            playFirstSelectedItem();
        }

        /// <summary>
        /// カラムヘッダの描画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        /// <summary>
        /// 仮想アイテムの取得
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            e.Item = dummyPlaylistViewItem;
        }

        int requestEnsureVisibleOID;
        /// <summary>
        /// アイテムの描画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            if (this.ItemHeight != e.Bounds.Height)
            {
                this.ItemHeight = e.Bounds.Height;
                Columns[0].Width = CoverArtSizeWithPad;
                return;
            }
            var index = e.ItemIndex;
            if (index >= VirtualListSize) return;
            int oid = getObjectIDByViewID(index);
            int indexInGroup = getIndexInGroup(index);
            bool isFirstOfAlbum = indexInGroup == (showGroup ? 0 : 1);
            var row = Controller.GetPlaylistRow(oid);
            if (row == null) return;
            if (requestEnsureVisibleOID != -1)
            {
                EnsureVisible(VirtualListSize - 1);
                SelectItemIndirect(requestEnsureVisibleOID);
                requestEnsureVisibleOID = -1;
            }
            if (Columns[0].Width != CoverArtSizeWithPad)
            {
                int tmp = (int)((Columns[0].Width - CoverArtMargin * 2) / ItemHeight);
                if (CoverArtLineNum != tmp)
                {
                    var si = SelectedIndices;
                    if (si.Count != 0)
                    {
                        requestEnsureVisibleOID = getObjectIDByViewID(si[0]);
                    }
                    CoverArtLineNum = tmp;
                    genMapTable(tagAlbumContinuousCount);
                    VirtualListSize = v2oMap != null ? v2oMap.Length : numObjects;
                    backgroundCoverartLoader.Reset(CoverArtSize);
                }
                Columns[0].Width = CoverArtSizeWithPad;
            }
            var bounds = e.Bounds;
            var isSelected = (e.State & ListViewItemStates.Selected) != 0;

            var album = row[colIdOfAlbum].ToString();
            var file_name = row[colIdOfFilename].ToString();

            using (var g = e.Graphics)
            {
                IntPtr hDC = g.GetHdc();

                // カバアート読み込みをキューイング
                if (ShowCoverArt && !string.IsNullOrEmpty(album))
                {
                    if (((indexInGroup - 2) * bounds.Height) < CoverArtSize)
                    {
                        backgroundCoverartLoader.Enqueue(album, file_name, index);
                    }
                }

                // 背景を描画
                drawItemBackground(hDC, album, bounds, index, isSelected, isFirstOfAlbum);

                // columnを表示順にソート
                if ((cols == null) || (this.Columns.Count != cols.Count))
                {
                    cols = this.Columns.Cast<ColumnHeader>().OrderBy(_ => _.DisplayIndex).ToList();
                }

                // 各column描画準備
                GDI.SetBkMode(hDC, GDI.BkMode.TRANSPARENT);
                int offsetX = bounds.X;
                IntPtr hOldFont;

                // 各column描画
                if (indexInGroup == 0)
                {
                    GDI.SetTextColor(hDC, (uint)(SystemColors.ControlText.ToArgb()) & 0xffffff);
                    IntPtr hFont = (new Font(realFont, FontStyle.Bold)).ToHfont();
                    hOldFont = GDI.SelectObject(hDC, hFont);
                    var albumArtist = row[Controller.GetColumnIndexByName("tagAlbumArtist")].ToString();
                    var artist = row[Controller.GetColumnIndexByName("tagArtist")].ToString();
                    var groupText = album + " / " + (string.IsNullOrEmpty(albumArtist) ? artist : albumArtist);
                    var textWidth = drawStringTruncate(hDC, offsetX + TextMargin * 2, bounds.Y + (bounds.Height - sizeOfTruncateStringCache.Height) / 2, bounds.Width, TextMargin, groupText, sizeOfTruncateStringCache.Width, false);
                    textWidth = offsetX + TextMargin * 6 + textWidth;
                    if (textWidth < bounds.Width)
                    {
                        GDI.MoveToEx(hDC, textWidth, bounds.Y + bounds.Height / 2, IntPtr.Zero);
                        GDI.LineTo(hDC, offsetX + bounds.Width, bounds.Y + bounds.Height / 2);
                    }

                    foreach (ColumnHeader head in cols)
                    {
                        if (showVerticalGrid && offsetX > textWidth)
                        {
                            drawGridLine(hDC, offsetX, bounds.Y + bounds.Height / 2 + 1, bounds.Height - bounds.Height / 2 - 1);
                        }
                        offsetX += head.Width;
                    }
                }
                else
                {
                    GDI.SetTextColor(hDC, (uint)(isSelected ? SystemColors.HighlightText.ToArgb() : SystemColors.ControlText.ToArgb()) & 0xffffff);
                    IntPtr hFont = (emphasizedRowId >= 0 && getViewIDByObjectID(emphasizedRowId) == index ? new Font(realFont, FontStyle.Bold) : realFont).ToHfont();
                    hOldFont = GDI.SelectObject(hDC, hFont);
                    if (sizeOfTruncateStringCache.Width == 0)
                    {
                        GDI.GetTextExtentPoint32(hDC, PostTruncateString, PostTruncateString.Length, out sizeOfTruncateStringCache);
                    }

                    foreach (ColumnHeader head in cols)
                    {
                        if (showVerticalGrid)
                        {
                            drawGridLine(hDC, offsetX, bounds.Y, bounds.Height);
                        } 
                        if (head.Tag == null)
                        {
                            if (ShowCoverArt)
                            {
                                drawItemCoverArt(hDC, offsetX, bounds.Y, bounds.Height, CoverArtMargin, album, indexInGroup, ShowGroup ? 0 : getGroupItemCount(index));
                            }
                            offsetX += head.Width;
                            continue;
                        }
                        int colidx = (int)head.Tag;
                        if (colidx >= row.Length) continue;
                        var col = dbColumnsCache[colidx];
                        if (col.Type == Library.LibraryColumnType.Rating)
                        {
                            if (!isDummyRow(index))
                            {
                                int rate = 0;
                                int.TryParse(row[colidx].ToString(), out rate);
                                g.ReleaseHdc(hDC);
                                ratingRenderer.Draw(rate / 10, g, offsetX + TextMargin, bounds.Y, head.Width - TextMargin, bounds.Height);
                                hDC = g.GetHdc();
                            }
                        }
                        else
                        {
                            if (!isDummyRow(index))
                            {
                                drawStringTruncate(hDC, offsetX, bounds.Y + (bounds.Height - sizeOfTruncateStringCache.Height) / 2, head.Width, TextMargin, prettyColumnString(col, row[colidx].ToString()), sizeOfTruncateStringCache.Width, isColumnPadRight(col));
                            }
                        }
                        offsetX += head.Width;
                    }
                }
                GDI.DeleteObject(GDI.SelectObject(hDC, hOldFont));
                g.ReleaseHdc(hDC);
            }
        }
        #endregion

        #region 描画用関数
        /// <summary>
        /// アルバムの先頭を示す描画
        /// </summary>
        /// <param name="hDC"></param>
        /// <param name="bounds"></param>
        private void drawFirstTrackIndicator(IntPtr hDC, Rectangle bounds)
        {
            int markerSize = bounds.Height / 2;
            GDI.SetDCPenColor(hDC, SystemPens.ControlDark.Color);
            GDI.SetDCBrushColor(hDC, SystemPens.ControlDark.Color);
            GDI.MoveToEx(hDC, bounds.X, bounds.Y, IntPtr.Zero);
            GDI.LineTo(hDC, bounds.X + bounds.Width, bounds.Y);
        }

        /// <summary>
        /// 強調表示を示す描画
        /// </summary>
        /// <param name="hDC"></param>
        /// <param name="bounds"></param>
        private void drawEmphasizedIndicator(IntPtr hDC, Rectangle bounds)
        {
            GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.NULL_BRUSH));
            GDI.SetDCPenColor(hDC, Color.Navy);
            GDI.Rectangle(hDC, bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);
        }

        /// <summary>
        /// カラムの境界を描画する
        /// </summary>
        /// <param name="hDC"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <param name="height"></param>
        private void drawGridLine(IntPtr hDC, int offsetX, int offsetY, int height)
        {
            if (offsetX <= 0) return;
            GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.DC_PEN));
            GDI.SetDCPenColor(hDC, SystemPens.ControlDark.Color);
            GDI.MoveToEx(hDC, offsetX - 1, offsetY, IntPtr.Zero);
            GDI.LineTo(hDC, offsetX - 1, offsetY + height);

            GDI.SetDCPenColor(hDC, SystemPens.Control.Color);
            GDI.MoveToEx(hDC, offsetX, offsetY, IntPtr.Zero);
            GDI.LineTo(hDC, offsetX, offsetY + height);
        }

        /// <summary>
        /// 行の背景を描画する
        /// </summary>
        /// <param name="hDC"></param>
        /// <param name="album"></param>
        /// <param name="bounds"></param>
        /// <param name="index"></param>
        /// <param name="isSelected"></param>
        /// <param name="isFirstTrack"></param>
        private void drawItemBackground(IntPtr hDC, string album, Rectangle bounds, int index, bool isSelected, bool isFirstTrack)
        {
            GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.DC_BRUSH));
            GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.DC_PEN));
            var isEvenRow = index % 2 == 0;
            if (isSelected && !isDummyRow(index))
            {
                var color = ((SolidBrush)SystemBrushes.Highlight).Color;
                GDI.SetDCBrushColor(hDC, color);
                GDI.SetDCPenColor(hDC, color);
            }
            else
            {
                if (UseColor)
                {
                    int c = (album.GetHashCode() & 0xFFFFFF) | 0x00c0c0c0;
                    int red = c >> 16;
                    int green = (c >> 8) & 0xff;
                    int blue = c & 0xff;
                    if (isEvenRow)
                    {
                        red = 63 + (red * 3 / 4);
                        green = 63 + (green * 3 / 4);
                        blue = 63 + (blue * 3 / 4);
                    }
                    GDI.SetDCBrushColor(hDC, red, green, blue);
                    GDI.SetDCPenColor(hDC, red, green, blue);
                }
                else
                {
                    var fillcolor = ((SolidBrush)(isEvenRow
                                ? SystemBrushes.Window
                                : SystemBrushes.ControlLight)).Color;
                    GDI.SetDCBrushColor(hDC, fillcolor);
                    GDI.SetDCPenColor(hDC, fillcolor);
                }
            }
            GDI.Rectangle(hDC, bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y + bounds.Height);

            // アルバム先頭描画
            if (isFirstTrack)
            {
                drawFirstTrackIndicator(hDC, bounds);
            }

            // 強調描画
            if (emphasizedRowId >= 0 && !isDummyRow(index) && emphasizedRowId == getObjectIDByViewID(index))
            {
                drawEmphasizedIndicator(hDC, bounds);
            }
        }

        /// <summary>
        /// 行内にカバーアートを描画
        /// </summary>
        /// <param name="hDC">DC</param>
        /// <param name="offsetX">X位置</param>
        /// <param name="offsetY">Y位置</param>
        /// <param name="height">高さ</param>
        /// <param name="margin">余白</param>
        /// <param name="album">アルバム名</param>
        /// <param name="index">アルバム内の位置</param>
        /// <param name="numOfGroup">最終トラックかどうか</param>
        private void drawItemCoverArt(IntPtr hDC, int offsetX, int offsetY, int height, int margin, string album, int index, int numOfGroup)
        {
            if (backgroundCoverartLoader.IsCached(album))
            {
                var img = backgroundCoverartLoader.GetCache(album);
                var isFirstTrack = index == 1;
                if (img.Width <= 1) return;
                if (img == null) return;

                var virtheight = img.Height + margin + margin;
                int imageOffset;
                int topMargin;
                if (numOfGroup > 0 && virtheight > numOfGroup * height)
                {
                    topMargin = (isFirstTrack ? 1 : 0);
                    imageOffset = (int)((numOfGroup * height / 2.0) - (0.3 * img.Height));
                    if (imageOffset > 0) imageOffset = ((numOfGroup * height) - img.Height) / 2;
                }
                else
                {
                    topMargin = (isFirstTrack ? margin : 0);
                    imageOffset = margin;
                }
                GDI.BitBlt(hDC,
                    offsetX + (CoverArtSize - img.Width) / 2 + margin,
                    offsetY + topMargin,
                    img.Width,
                    height - topMargin,
                    img.HDC,
                    0,
                    (index - 1) * height - imageOffset + topMargin,
                    0x00CC0020);
            }
        }

        /// <summary>
        /// 文字列を指定幅に切り詰めて描画
        /// </summary>
        /// <param name="hDC">DC</param>
        /// <param name="offsetX">X位置</param>
        /// <param name="offsetY">Y位置</param>
        /// <param name="width">幅</param>
        /// <param name="margin">マージン</param>
        /// <param name="str">文字列</param>
        /// <param name="widthTruncateString">"..."の幅</param>
        /// <param name="padRight">右寄せ</param>
        /// <returns>描画幅(px)</returns>
        private int drawStringTruncate(IntPtr hDC, int offsetX, int offsetY, int width, int margin, string str, int widthTruncateString, bool padRight)
        {
            width -= margin * 2;
            Size size;
            GDI.GetTextExtentPoint32(hDC, str, str.Length, out size);
            if (size.Width < width)
            {
                var paddingLeft = margin + (padRight ? (width - size.Width) : 0);
                GDI.TextOut(hDC, offsetX + paddingLeft, offsetY, str, str.Length);
                return size.Width;
            }
            else
            {
                if (width > widthTruncateString)
                {
                    //　二分探索で表示できる最大文字数を調査
                    int rangeMin = 0;
                    int rangeWidth = str.Length;
                    do
                    {
                        var halfRangeWidth = rangeWidth / 2;
                        GDI.GetTextExtentPoint32(hDC, str, rangeMin + halfRangeWidth, out size);
                        if (size.Width + widthTruncateString > width)
                        {
                            rangeWidth = halfRangeWidth;
                        }
                        else
                        {
                            rangeMin += halfRangeWidth;
                        }
                    } while (rangeWidth > 1);
                    GDI.TextOut(hDC, offsetX + margin, offsetY, str.Substring(0, rangeMin) + PostTruncateString, rangeMin + PostTruncateString.Length);
                    return size.Width + widthTruncateString;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// カラムの値を読みやすくフォーマットする
        /// </summary>
        /// <param name="col">カラム</param>
        /// <param name="str">値の文字列</param>
        /// <returns>フォーマットされた文字列</returns>
        private string prettyColumnString(Column col, string str)
        {
            switch (col.Type)
            {
                case Library.LibraryColumnType.Timestamp64:
                    return str == "0" ? "-" : Util.Util.timestamp2DateTime(long.Parse(str)).ToString();
                case Library.LibraryColumnType.Time:
                    return Util.Util.getMinSec(int.Parse(str));
                case Library.LibraryColumnType.Bitrate:
                    return str == "" ? "" : (int.Parse(str)) / 1000 + "kbps";
                case Library.LibraryColumnType.FileSize:
                    int sz = int.Parse(str);
                    return sz > 1024 * 1024 ? String.Format("{0:0.00}MB", sz / 1024.0 / 1024) : String.Format("{0}KB", sz / 1024);
                case LibraryColumnType.TrackNumber:
                    if (TrackNumberFormat == DefaultUIPreference.TrackNumberFormats.N)
                    {
                        int tr = -1;
                        if (Util.Util.tryParseInt(str, ref tr))
                        {
                            return tr.ToString();
                        }
                    }
                    return str;
                default:
                    return str.Replace("\n", "; ");
            }
        }

        /// <summary>
        /// 右寄せで表示するカラムかどうか
        /// </summary>
        /// <param name="col">カラム</param>
        /// <returns>右寄せならTrue</returns>
        private bool isColumnPadRight(Column col)
        {
            switch (col.Type)
            {
                case LibraryColumnType.Timestamp64:
                case LibraryColumnType.Time:
                case LibraryColumnType.Bitrate:
                case LibraryColumnType.TrackNumber:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 表示の行IDとプレイリストのオブジェクトのマッピングテーブルを作る
        /// </summary>
        /// <param name="albumCounts"></param>
        private void genMapTable(int[] albumCounts)
        {
            if (ShowGroup)
            {
                int minNum = CoverArtLineNum - 1;
                if ((albumCounts == null) || (albumCounts.Length == 0))
                {
                    v2oMap = new int[0];
                    o2vMap = new int[0];
                    return;
                }
                var v2imap = new int[albumCounts.Length * (minNum + 2)];
                o2vMap = new int[albumCounts.Length];
                int iv2imap = 0;
                int ii2vmap = 0;
                int c = 0;
                v2imap[iv2imap++] = int.MinValue;
                v2imap[iv2imap++] = 0;
                c++;
                o2vMap[ii2vmap++] = c++;
                for (int i = 1, I = albumCounts.Length; i < I; i++)
                {
                    if (albumCounts[i] == 0)
                    {
                        for (int j = 0, J = minNum - albumCounts[i - 1]; j < J; j++)
                        {
                            v2imap[iv2imap++] = -j - 1;
                            c++;
                        }
                        v2imap[iv2imap++] = int.MinValue;
                        c++;
                    }
                    v2imap[iv2imap++] = i;
                    o2vMap[ii2vmap++] = c++;
                }
                for (int j = 0, J = minNum - albumCounts[albumCounts.Length - 1]; j < J; j++)
                {
                    v2imap[iv2imap++] = -j - 1;
                }
                Array.Resize<int>(ref v2imap, iv2imap);
                v2oMap = v2imap;
            }
            else
            {
                o2vMap = null;
                v2oMap = null;
            }
        }

        /// <summary>
        /// ViewIDからObjectIDを取得
        /// </summary>
        /// <param name="vid"></param>
        /// <returns></returns>
        private int getObjectIDByViewID(int vid)
        {
            if (v2oMap == null) return vid;
            if (vid >= v2oMap.Length) return 0;
            if (v2oMap[vid] == int.MinValue) return v2oMap[vid + 1];
            if (v2oMap[vid] < 0) return v2oMap[vid + v2oMap[vid]];
            return v2oMap[vid];
        }

        /// <summary>
        /// ObjectIDからViewIDを取得
        /// </summary>
        /// <param name="oid"></param>
        /// <returns></returns>
        private int getViewIDByObjectID(int oid)
        {
            if (o2vMap == null) return oid;
            if (oid < 0) return -1;
            if (oid >= o2vMap.Length) return -1;
            return o2vMap[oid];
        }

        /// <summary>
        /// ViewIDがダミー行かどうか
        /// </summary>
        /// <param name="vid"></param>
        /// <returns></returns>
        private bool isDummyRow(int vid)
        {
            if (v2oMap == null) return false;
            if (vid >= v2oMap.Length) return false;
            return v2oMap[vid] < 0;
        }

        /// <summary>
        /// ViewIDがグループヘッダの行かどうか
        /// </summary>
        /// <param name="vid"></param>
        /// <returns></returns>
        private bool isGroupHeaderRow(int vid)
        {
            if (v2oMap == null) return false;
            if (vid >= v2oMap.Length) return false;
            return v2oMap[vid] == int.MinValue;
        }

        /// <summary>
        /// ViewIDからAlbum内の行番号を取得
        /// </summary>
        /// <param name="vid">ViewID</param>
        /// <returns>Album内の行番号</returns>
        private int getIndexInGroup(int vid)
        {
            if (isGroupHeaderRow(vid)) return 0;
            var tmp = v2oMap == null ? vid : v2oMap[vid];
            var cnt = tagAlbumContinuousCount;
            var oid = getObjectIDByViewID(vid);
            if (oid >= cnt.Length) return -1;
            return 1 + cnt[oid] - (tmp < 0 ? tmp : 0);
        }

        /// <summary>
        /// グループのアイテム数を取得
        /// </summary>
        /// <param name="vid"></param>
        /// <returns></returns>
        private int getGroupItemCount(int vid)
        {
            int oid = getObjectIDByViewID(vid);
            do
            {
                oid++;
            } while ((oid < numObjects) && (tagAlbumContinuousCount[oid] != 0));
            return tagAlbumContinuousCount[oid - 1]+1;
        }

        /// <summary>
        /// アクティブなオブジェクトのIDを取得
        /// </summary>
        /// <returns></returns>
        private int getCurrentObjectID()
        {
            if (this.SelectedIndices.Count > 0)
            {
                return getObjectIDByViewID(SelectedIndices[0]);
            }
            else if (Controller.Current.IndexInPlaylist > 0)
            {
                return Controller.Current.IndexInPlaylist;
            }
            else
            {
                return -1;
            }
        }
        #endregion
        #endregion
    }
}
