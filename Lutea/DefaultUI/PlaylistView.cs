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
        private const int CoverArtMargin = 2;

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
        /// カバーアートのサイズ
        /// </summary>
        private int coverArtSize;

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
        /// カバーアート表示の大きさ
        /// </summary>
        public int CoverArtSize
        {
            get
            {
                return this.coverArtSize;
            }
            set
            {
                if (this.coverArtSize == value) return;
                this.coverArtSize = value;
                this.Invalidate();
            }
        }
        #endregion

        #region Publicメソッド
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public PlaylistView()
        {
            // レーティングの☆描画準備
            this.ratingRenderer = new RatingRenderer(@"components\rating_on.gif", @"components\rating_off.gif");

            // イベントハンドラの登録
            this.DrawItem += playlistView_DrawItem;
            this.MouseMove += playlistView_MouseMove;
            this.MouseClick += playlistView_MouseClick;
            this.ColumnClick += playlistView_ColumnClick;
            this.DoubleClick += playlistView_DoubleClick;
            this.KeyDown += playlistView_KeyDown;
            this.DrawColumnHeader += playlistView_DrawColumnHeader;
            this.RetrieveVirtualItem += playlistView_RetrieveVirtualItem;
            this.ColumnReordered += PlaylistView_ColumnReordered;
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
        /// <param name="itemCount">項目数</param>
        public void RefreshPlaylist(bool moveToIndex, int index, int itemCount)
        {
            // プレイリストが更新されてアイテムの位置が変わったらカバーアート読み込みキューを消去
            form.backgroundCoverartLoader.ClearQueue();

            if (moveToIndex)
            {
                this.SelectItem(index < 0 ? 0 : index);
            }
            this.VirtualListSize = itemCount;
            this.Refresh();
            if (moveToIndex)
            {
                this.SelectItem(index < 0 ? 0 : index);
            }
            this.EmphasizeRow(index);
        }

        /// <summary>
        /// 指定した行を強調表示(再生中)
        /// </summary>
        /// <param name="index">行</param>
        public void EmphasizeRow(int index)
        {
            try
            {
                this.RedrawItems(emphasizedRowId, emphasizedRowId, true);
            }
            catch { }

            emphasizedRowId = index;
            try
            {
                this.RedrawItems(index, index, true);
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
                List<string> filenames = new List<string>();
                foreach (int i in this.SelectedIndices)
                {
                    filenames.Add(Controller.GetPlaylistRowColumn(i, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)));
                }
                Controller.SetRating(filenames.ToArray(), rate);
            }
        }

        /// <summary>
        /// 選択されている項目を再生
        /// </summary>
        public void playFirstSelectedItem()
        {
            if (this.SelectedIndices.Count > 0)
            {
                Controller.PlayPlaylistItem(this.SelectedIndices[0]);
            }
        }
        #endregion

        #region Privateメソッド
        #region 選択トラック移動
        /// <summary>
        /// 次のアルバムの先頭トラックを選択
        /// </summary>
        private void moveToNextAlbum()
        {
            int idx = -1;
            if (this.SelectedIndices.Count > 0)
            {
                idx = this.SelectedIndices[0];
            }
            else if (Controller.Current.IndexInPlaylist > 0)
            {
                idx = Controller.Current.IndexInPlaylist;
            }

            if (idx == -1) return;

            var album = Controller.GetPlaylistRowColumn(idx, Controller.GetColumnIndexByName("tagAlbum"));
            do
            {
                idx++;
            } while ((Controller.GetPlaylistRowColumn(idx, Controller.GetColumnIndexByName("tagAlbum")) == album) && (idx + 1 != this.Items.Count));
            this.SelectItem(idx);
            this.EnsureVisible(Math.Min(idx + 5, this.Items.Count - 1));
        }

        /// <summary>
        /// 前のアルバムの先頭トラックを選択
        /// </summary>
        private void moveToPrevAlbum()
        {
            int idx = -1;
            if (this.SelectedIndices.Count > 0)
            {
                idx = this.SelectedIndices[0];
            }
            else if (Controller.Current.IndexInPlaylist > 0)
            {
                idx = Controller.Current.IndexInPlaylist;
            }

            if ((idx == -1) || (idx == 0)) return;
            var album = Controller.GetPlaylistRowColumn(idx - 1, Controller.GetColumnIndexByName("tagAlbum"));
            do
            {
                idx--;
            } while ((Controller.GetPlaylistRowColumn(idx - 1, Controller.GetColumnIndexByName("tagAlbum")) == album) && (idx != 0));
            this.SelectItem(idx);
        }

        /// <summary>
        /// 次のトラックを選択
        /// </summary>
        private void moveToNextTrack()
        {
            if (this.SelectedIndices.Count > 0)
            {
                this.SelectItem(this.SelectedIndices[0] + 1);
            }
            else if (Controller.Current.IndexInPlaylist > 0)
            {
                this.SelectItem(Controller.Current.IndexInPlaylist + 1);
            }
            else
            {
                moveToFirstTrack();
            }
        }

        /// <summary>
        /// 前のトラックを選択
        /// </summary>
        private void moveToPrevTrack()
        {
            if (this.SelectedIndices.Count > 0)
            {
                this.SelectItem(this.SelectedIndices[0] - 1);
            }
            else if (Controller.Current.IndexInPlaylist > 0)
            {
                this.SelectItem(Controller.Current.IndexInPlaylist - 1);
            }
            else
            {
                moveToLastTrack();
            }
        }

        /// <summary>
        /// 先頭トラックを選択
        /// </summary>
        private void moveToFirstTrack()
        {
            this.SelectItem(0);
        }

        /// <summary>
        /// 最後のトラックを選択
        /// </summary>
        private void moveToLastTrack()
        {
            this.SelectItem(this.Items.Count - 1);
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
            if (dbColumnsCache[(int)(this.Columns[item.SubItems.IndexOf(sub)].Tag)].Type == Library.LibraryColumnType.Rating)
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
            int starwidth = ratingRenderer.EachWidth;
            var item = this.GetItemAt(e.X, e.Y);
            if (item == null) return;
            var sub = item.GetSubItemAt(e.X, e.Y);
            if (sub == null) return;
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
            Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)), rate);
        }

        /// <summary>
        /// ドラッグ＆ドロップ時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            try
            {
                var count = this.SelectedIndices.Count;
                if (count < 1) return;
                List<string> filenames = new List<string>();
                var colIndexOfFilename = Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name);
                foreach (int i in this.SelectedIndices)
                {
                    filenames.Add(Controller.GetPlaylistRowColumn(i, colIndexOfFilename).Trim());
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
            Controller.SetSortColumn(dbColumnsCache[(int)this.Columns[e.Column].Tag].Name);
        }

        /// <summary>
        /// ダブルクリックした時のハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_DoubleClick(object sender, EventArgs e)
        {
            if (this.SelectedIndices.Count > 0)
            {
                Controller.PlayPlaylistItem(this.SelectedIndices[0]);
            }
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

        /// <summary>
        /// アイテムの描画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playlistView_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            var index = e.ItemIndex;
            var row = Controller.GetPlaylistRow(index);
            if (row == null) return;
            var bounds = e.Bounds;
            var isSelected = (e.State & ListViewItemStates.Selected) != 0;

            int indexInGroup = 0;
            var colIdOfAlbum = Controller.GetColumnIndexByName("tagAlbum");
            var album = row[colIdOfAlbum].ToString();
            var file_name = row[Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)].ToString();
            while (album == Controller.GetPlaylistRowColumn(index - indexInGroup, colIdOfAlbum)) indexInGroup++;
            var isLastOfAlbum = album != Controller.GetPlaylistRowColumn(index + 1, colIdOfAlbum);
            var isFirstOfAlbum = indexInGroup == 1;

            using (var g = e.Graphics)
            {
                IntPtr hDC = g.GetHdc();

                // カバアート読み込みをキューイング
                if (ShowCoverArt && !string.IsNullOrEmpty(album))
                {
                    if (((indexInGroup - 2) * bounds.Height) < CoverArtSize && !form.backgroundCoverartLoader.IsCached(album))
                    {
                        form.backgroundCoverartLoader.Enqueue(album, file_name, index);
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
                IntPtr hFont = (emphasizedRowId == index ? new Font(realFont, FontStyle.Bold) : realFont).ToHfont();
                IntPtr hOldFont = GDI.SelectObject(hDC, hFont);
                if (sizeOfTruncateStringCache.Width == 0)
                {
                    GDI.GetTextExtentPoint32(hDC, PostTruncateString, PostTruncateString.Length, out sizeOfTruncateStringCache);
                }

                GDI.SetTextColor(hDC, (uint)(isSelected ? SystemColors.HighlightText.ToArgb() : SystemColors.ControlText.ToArgb()) & 0xffffff);
                GDI.SetBkMode(hDC, GDI.BkMode.TRANSPARENT);
                int offsetX = bounds.X;

                // 各column描画
                foreach (ColumnHeader head in cols)
                {
                    int colidx = (int)head.Tag;
                    if (colidx >= row.Length) continue;
                    drawColumnSeparator(hDC, offsetX, bounds.Y, bounds.Height);
                    var col = dbColumnsCache[colidx];
                    if (col.Type == Library.LibraryColumnType.Rating)
                    {
                        int rate = 0;
                        int.TryParse(row[colidx].ToString(), out rate);
                        g.ReleaseHdc(hDC);
                        ratingRenderer.Draw(rate / 10, g, offsetX + TextMargin, bounds.Y, head.Width - TextMargin, bounds.Height);
                        hDC = g.GetHdc();
                    }
                    else
                    {
                        if (ShowCoverArt && col.Type == Library.LibraryColumnType.TrackNumber)
                        {
                            drawItemCoverArt(hDC, offsetX, bounds.Y, bounds.Height, CoverArtMargin, album, indexInGroup, isLastOfAlbum);
                        }
                        drawStringTruncate(hDC, offsetX, bounds.Y + (bounds.Height - sizeOfTruncateStringCache.Height) / 2, head.Width, TextMargin, prettyColumnString(col, row[colidx].ToString()), sizeOfTruncateStringCache.Width, isColumnPadRight(col));
                    }
                    offsetX += head.Width;
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
        private void drawColumnSeparator(IntPtr hDC, int offsetX, int offsetY, int height)
        {
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
        private void drawItemBackground(IntPtr hDC, string album, Rectangle bounds, int index,  bool isSelected, bool isFirstTrack)
        {
            GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.DC_BRUSH));
            GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.DC_PEN));
            var isEvenRow = index % 2 == 0;
            if (isSelected)
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
            if (emphasizedRowId == index)
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
        /// <param name="isLast">最終トラックかどうか</param>
        private void drawItemCoverArt(IntPtr hDC, int offsetX, int offsetY, int height, int margin, string album, int index, bool isLast)
        {
            if (form.backgroundCoverartLoader.IsCached(album))
            {
                var img = form.backgroundCoverartLoader.GetCache(album);
                var isFirstTrack = index == 1;
                if (img != null && img.Width > 1)
                {
                    GDI.BitBlt(hDC,
                        offsetX + (CoverArtSize - img.Width) / 2 + margin,
                        offsetY + (isFirstTrack ? margin : 0),
                        img.Width,
                        height - (isFirstTrack ? margin : 0),
                        img.HDC,
                        0,
                        (index - 1) * height - (isFirstTrack ? 0 : margin) + ((isFirstTrack && isLast) ? (int)(img.Height * 0.30 - (height / 2)) : 0),
                        0x00CC0020);
                }
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
        private void drawStringTruncate(IntPtr hDC, int offsetX, int offsetY, int width, int margin, string str, int widthTruncateString, bool padRight)
        {
            width -= margin * 2;
            Size size;
            GDI.GetTextExtentPoint32(hDC, str, str.Length, out size);
            if (size.Width < width)
            {
                var paddingLeft = margin + (padRight ? (width - size.Width) : 0);
                GDI.TextOut(hDC, offsetX + paddingLeft, offsetY, str, str.Length);
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
        #endregion
        #endregion
    }
}
