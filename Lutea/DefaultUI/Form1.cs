using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.IO;

using Gageas.Lutea.Tags;
using Gageas.Lutea.Util;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.DefaultUI
{
    [GuidAttribute("406AB8D9-F6CF-4234-8B32-4D0064DA0200")]
    [LuteaComponentInfo("DefaultUI", "Gageas", 0.1, "標準GUI Component")]
    public partial class DefaultUIForm : Form, Lutea.Core.LuteaUIComponentInterface
    {
        #region General-purpose delegates
        private delegate void VOIDINT(int x);
        private delegate void VOIDBOOL(bool b);
        private delegate void VOIDSTRING(String str);
        private delegate void VOIDVOID();
        #endregion

        /// <summary>
        /// 発行中のクエリのステータス
        /// </summary>
        enum QueryStatus { Normal, Waiting, Error };

        /// <summary>
        /// クエリ状態ごとのtextboxの背景色
        /// </summary>
        Color[] statusColor = { System.Drawing.SystemColors.Window, Color.FromArgb(230, 230, 130), Color.FromArgb(250, 130, 130) };

        /// <summary>
        /// ログを表示するFormを保持
        /// </summary>
        LogViewerForm logview;

        /// <summary>
        /// カバーアートを表示するFormを保持
        /// </summary>
        CoverViewerForm coverViewForm;

        /// <summary>
        /// カバーアートをバックグラウンドで読み込むスレッドを保持
        /// </summary>
        Thread coverArtImageLoaderThread;

        /// <summary>
        /// Windows7の拡張タスクバーを制御
        /// </summary>
        TaskbarExtension TaskbarExt;

        /// <summary>
        /// win7タスクバーに表示するボタンの画像リスト
        /// </summary>
        ImageList taskbarImageList;

        /// <summary>
        /// 各Columnのでデフォルトの幅を定義
        /// </summary>
        Dictionary<DBCol, int> defaultColumnDisplayWidth = new Dictionary<DBCol, int>(){
            {DBCol.tagTracknumber,40},
            {DBCol.tagTitle,120},
            {DBCol.tagArtist,120},
            {DBCol.tagAlbum,80},
            {DBCol.tagComment,120},
            {DBCol.rating,84},
        };

        /// <summary>
        /// playlistviewに表示するcolumnを定義
        /// </summary>
        DBCol[] displayColumns = { DBCol.tagTracknumber, DBCol.tagTitle, DBCol.tagArtist, DBCol.tagAlbum, DBCol.tagComment, DBCol.tagDate, DBCol.tagGenre,
                                     DBCol.statDuration, DBCol.statBitrate, DBCol.rating, DBCol.lastplayed, DBCol.playcount }; // DBCol.infoCodec, DBCol.infoCodec_sub, DBCol.modify, DBCol.statChannels, DBCol.statSamplingrate
        //        int[] ColumnOrder = null;
        //        int[] ColumnWidth = null;
        Dictionary<DBCol, int> ColumnOrder = new Dictionary<DBCol, int>();
        Dictionary<DBCol, int> ColumnWidth = new Dictionary<DBCol, int>();

        /// <summary>
        /// filter viewに表示するcolumnを定義
        /// </summary>
        DBCol[] filterColumns = { DBCol.tagArtist, DBCol.tagAlbum, DBCol.tagDate, DBCol.tagGenre, DBCol.infoCodec_sub, DBCol.rating };

        /// <summary>
        /// Ratingの☆を描画
        /// </summary>
        RatingRenderer ratingRenderer;

        /// <summary>
        /// settingから読み出した値を保持、あるいはデフォルト値
        /// </summary>
        private Size config_FormSize;
        private Point config_FormLocation;
        private string LibraryLatestDir = "";
        private int settingCoverArtSize = 120;

        private int SpectrumMode = 0;
        private Preference.FFTNum FFTNum = Preference.FFTNum.FFT1024;
        private bool FFTLogarithmic = false;
        private Color SpectrumColor1 = SystemColors.Control;
        private Color SpectrumColor2 = Color.Orange;
        private bool ColoredAlbum = true;

        private bool UseMediaKey = false;
        private Keys hotkey_PlayPause = Keys.None;
        private Keys hotkey_Stop = Keys.None;
        private Keys hotkey_NextTrack = Keys.None;
        private Keys hotkey_PrevTrack = Keys.None;


        public DefaultUIForm()
        {
#if DEBUG
            logview = new LogViewerForm();
            logview.Show();
#endif
            InitializeComponent();
            Thread.CurrentThread.Priority = ThreadPriority.Normal;
            trackInfoText.Text = "";
            textBox1.ForeColor = System.Drawing.SystemColors.WindowText;
            toolStripStatusLabel1.Text = "";
        }

        private void setupPlaylistView()
        {
            listView1.BeginUpdate();
            listView1.Enabled = false;

            // backup order/width
            if (listView1.Columns.Count > 0)
            {
                ColumnOrder.Clear();
                ColumnWidth.Clear();
                for (int i = 0; i < listView1.Columns.Count; i++)
                {
                    ColumnOrder[(DBCol)listView1.Columns[i].Tag] = listView1.Columns[i].DisplayIndex;
                    ColumnWidth[(DBCol)listView1.Columns[i].Tag] = Math.Max(10, listView1.Columns[i].Width);
                }
            }

            listView1.Clear();
            foreach (DBCol col in displayColumns)
            {
                var colheader = new ColumnHeader();
                colheader.Text = Controller.GetColumnLocalString(col);
                colheader.Tag = col;
                if (ColumnWidth.ContainsKey(col))
                {
                    colheader.Width = ColumnWidth[col];
                }
                else
                {
                    if (defaultColumnDisplayWidth.ContainsKey(col))
                    {
                        colheader.Width = defaultColumnDisplayWidth[col];
                    }
                }
                listView1.Columns.Add(colheader);
                if (col == DBCol.statBitrate)
                {
                    colheader.TextAlign = HorizontalAlignment.Right;
                }
            }

            foreach (ColumnHeader colheader in listView1.Columns)
            {
                var col = (DBCol)(colheader.Tag);
                if (ColumnOrder.ContainsKey(col))
                {
                    try
                    {
                        colheader.DisplayIndex = ColumnOrder[col];
                    }
                    catch
                    {
                        colheader.DisplayIndex = listView1.Columns.Count - 1;
                    }
                }
                else
                {
                    colheader.DisplayIndex = listView1.Columns.Count - 1;
                }
            }

            playlistUpdated(null);

            listView1.EndUpdate();
            listView1.Enabled = true;
        }

        #region Application core event handler
        private void trackChange(int index)
        {
            var album = Controller.Current.MetaData(DBCol.tagAlbum);
            var artist = Controller.Current.MetaData(DBCol.tagArtist);
            var genre = Controller.Current.MetaData(DBCol.tagGenre);
            groupBox1.ContextMenuStrip = null;
            ContextMenuStrip cms = null;
            this.Invoke((MethodInvoker)(() =>
            {
                toolStripButton3.Checked = false;
                xTrackBar1.Max = Controller.Current.Length;
                selectRow(index);
                emphasizeRow(index);
                coverArtImageLoaderThread.Interrupt();
                if (index < 0)
                {
                    trackInfoText.Text = "Stop";
                    setFormTitle(null);
                    toolStripStatusLabel2.Text = "Ready ";
                    if (spectrumAnalyzerThread != null)
                    {
                        spectrumAnalyzerThread.Abort();
                        spectrumAnalyzerThread = null;
                    }
                    if (TaskbarExt != null)
                    {
                        TaskbarExt.Taskbar.SetProgressState(this.Handle, TaskbarExtension.TbpFlag.NoProgress);
                    }
                    var hIcon = hIconForWindowIcon_Large;
                    SendMessage(this.Handle, WM_SETICON, (IntPtr)1, this.Icon.Handle);
                    hIconForWindowIcon_Large = IntPtr.Zero;
                    if (hIcon != IntPtr.Zero)
                    {
                        DestroyIcon(hIcon);
                    }
                }

                toolStripStatusLabel2.Text = "Playing " + Controller.Current.StreamFilename;
                groupBox1.Text = (album + Util.Util.FormatIfExists(" #{0}", Controller.Current.MetaData(DBCol.tagTracknumber))).Replace("&", "&&");
                trackInfoText.Text = Util.Util.FormatIfExists("{0}{1}",
                    Controller.Current.MetaData(DBCol.tagTitle),
                    Util.Util.FormatIfExists(" - {0}",
                       Controller.Current.MetaData(DBCol.tagArtist))
                    );
                setFormTitle(Controller.Current.MetaData(DBCol.tagTitle) + Util.Util.FormatIfExists(" / {0}", Controller.Current.MetaData(DBCol.tagArtist)));
                cms = new ContextMenuStrip();
                listView2.Items.Clear();
            }));
            if (index < 0) return;

            if (spectrumAnalyzerThread == null)
            {
                spectrumAnalyzerThread = new Thread(SpectrumAnalyzerProc);
                spectrumAnalyzerThread.IsBackground = true;
                spectrumAnalyzerThread.Start();
            }

            var item_splitter = new char[] { '；', ';', '，', ',', '／', '/', '＆', '&', '・', '･', '、', '､', '（', '(', '）', ')', '\n', '\t' };
            var subArtists = artist.Split(item_splitter, StringSplitOptions.RemoveEmptyEntries).ToList();
            var subGenre = genre.Split(item_splitter, StringSplitOptions.RemoveEmptyEntries).ToList().FindAll(e => e.Length > 1);
            var q = String.Join(" OR ",(from __ in from _ in subArtists select _.LCMapUpper().Trim() select String.Format(__.Length>1?@" LCMapUpper(tagArtist) LIKE '%{0}%' ":@" LCMapUpper(tagArtist) = '{0}' ",__.EscapeSingleQuotSQL())).ToArray());
            object[][] related_albums = null;
            object[][] multi_disc_albums = null;
            using (var db = Controller.GetDBConnection())
            {
                // 関連アルバムを引っ張ってくる
                if (subArtists.Count > 0)
                {
                    using (var stmt = db.Prepare("SELECT tagAlbum,COUNT(*) FROM list WHERE tagAlbum IN (SELECT tagAlbum FROM list WHERE " + q + " ) GROUP BY tagAlbum ORDER BY COUNT(*) DESC;"))
                    {
                        related_albums = stmt.EvaluateAll();
                    }
                }
                // ディスクnに分かれてる感じのアルバムを引っ張ってくる
                using (var stmt = db.Prepare("SELECT tagAlbum,COUNT(*) FROM list WHERE LCMapUpper(tagAlbum) LIKE '" + new Regex(@"\d").Replace(album.LCMapUpper(), "_").EscapeSingleQuotSQL() + "' GROUP BY LCMapUpper(tagAlbum);"))
                {
                    multi_disc_albums = stmt.EvaluateAll();
                }
            }

            this.Invoke((MethodInvoker)(() =>
            {
                var cms_album = new ToolStripMenuItem("Album: " + album.Replace("&", "&&"), null, (e, o) => { Controller.createPlaylist("SELECT * FROM list WHERE tagAlbum = '" + album.EscapeSingleQuotSQL() + "';"); });
                var cms_artist = new ToolStripMenuItem("Artist: " + artist.Replace("&", "&&"), null, (e, o) => { Controller.createPlaylist("SELECT * FROM list WHERE tagArtist = '" + artist.EscapeSingleQuotSQL() + "';"); });
                var cms_genre = new ToolStripMenuItem("Genre: " + genre.Replace("&", "&&"), null, (e, o) => { Controller.createPlaylist("SELECT * FROM list WHERE tagGenre = '" + genre.EscapeSingleQuotSQL() + "';"); });
                cms.Items.Add(cms_album);
                cms.Items.Add(cms_artist);
                cms.Items.Add(cms_genre);
                cms.Items.Add(new ToolStripSeparator());

                // 関連アルバムを登録
                if (related_albums != null)
                {
                    foreach (var _ in related_albums)
                    {
                        var album_title = _[0].ToString();
                        if (string.IsNullOrEmpty(album_title)) continue;
                        var query = "SELECT * FROM list WHERE tagAlbum = '" + album_title.EscapeSingleQuotSQL() + "';";
                        cms.Items.Add("Album: [" + _[1].ToString() + "]" + album_title.Replace("&", "&&"), null, (e, o) => { Controller.createPlaylist(query); });
                        var item = new ListViewItem(new string[] { "", _[0].ToString() });
                        item.Tag = query;
                        listView2.Items.Add(item);
                    }
                }

                if (multi_disc_albums.Length > 1)
                {
                    foreach (var _ in multi_disc_albums)
                    {
                        var album_title = _[0].ToString();
                        if (string.IsNullOrEmpty(album_title)) continue;
                        cms_album.DropDownItems.Add("Album: [" + _[1].ToString() + "]" + album_title.Replace("&", "&&"), null, (e, o) => { Controller.createPlaylist("SELECT * FROM list WHERE tagAlbum = '" + album_title + "';"); });
                    }
                }

                // 各サブアーティストごとのクエリを作る
                if (subArtists.Count > 1)
                {
                    foreach (var _ in subArtists)
                    {
                        var artist_title = _;
                        cms_artist.DropDownItems.Add(artist_title.Trim(), null, (e, o) => { Controller.createPlaylist("SELECT * FROM list WHERE LCMapUpper(tagArtist) like '%" + artist_title.LCMapUpper().Trim().EscapeSingleQuotSQL() + "%';"); });
                    }
                }
                groupBox1.ContextMenuStrip = cms;

                // 各サブジャンルごとのクエリを作る
                if (subGenre.Count > 1)
                {
                    foreach (var _ in subGenre)
                    {
                        var genre_title = _;
                        cms_genre.DropDownItems.Add(genre_title.Trim(), null, (e, o) => { Controller.createPlaylist("SELECT * FROM list WHERE LCMapUpper(tagGenre) like '%" + genre_title.LCMapUpper().Trim().EscapeSingleQuotSQL() + "%';"); });
                    }
                }
                groupBox1.ContextMenuStrip = cms;
            }));
        }

        private void playbackErrorOccured()
        {
            setFormTitle("Playback Error");
        }

        void AppCore_onPlaybackOrderChange()
        {
            toolStripComboBox2.GetControl.SelectedIndex = (int)Controller.playbackOrder;
        }

        private void playlistUpdated(string sql)
        {
            try
            {
                this.Invoke(new Controller.PlaylistUpdatedEvent(refreshPlaylistView), new object[] { sql });
            }
            catch (Exception) { }
        }

        private void RefreshAll()
        {
            this.Invoke((MethodInvoker)(() =>
            {
                dummyFilterTab.TabPages.Clear();
                InitFilterView();
            }));
        }

        private void elapsedTimeChange(int second)
        {
            try
            {
                this.BeginInvoke((MethodInvoker)(() =>
                {
                    ulong len = (ulong)Controller.Current.Length;
                    xTrackBar1.Value = second;
                    toolStripStatusLabel1.Text = (Util.Util.getMinSec(second) + "/" + Util.Util.getMinSec(len));
                    if (TaskbarExt != null)
                    {
                        TaskbarExt.Taskbar.SetProgressState(this.Handle, TaskbarExtension.TbpFlag.Normal);
                        TaskbarExt.Taskbar.SetProgressValue(this.Handle, (ulong)second, (ulong)len);
                    }
                }));
            }
            catch (ObjectDisposedException) { }
        }

        public void changeVolume()
        {
            this.Invoke((MethodInvoker)(() => toolStripXTrackbar1.GetControl.Value = (int)(Controller.Volume * 100)));
        }

        public void playbackOrderChange()
        {
            this.Invoke((MethodInvoker)(() => toolStripComboBox2.GetControl.SelectedIndex = (int)(Controller.playbackOrder)));
        }
        #endregion

        #region Form event handler
        private void Form1_Load(object sender, EventArgs e)
        {
            Logger.Log("Form1 started");
            setFormTitle(null);
            Controller.PlaylistUpdated += new Controller.PlaylistUpdatedEvent(playlistUpdated);
            Controller.onElapsedTimeChange += new Controller.VOIDINT(elapsedTimeChange);
            Controller.onTrackChange += new Controller.VOIDINT(trackChange);
            Controller.onPlaybackErrorOccured += new Controller.VOIDVOID(playbackErrorOccured);
            Controller.onVolumeChange += new Controller.VOIDVOID(changeVolume);
            Controller.onPlaybackOrderChange += new Controller.VOIDVOID(AppCore_onPlaybackOrderChange);
            Controller.onDatabaseUpdated += new Controller.VOIDVOID(RefreshAll);
            //            AppCore.Init();

            treeView1.ImageList = new ImageList();
            //            treeView1.ImageList.ImageSize = new System.Drawing.Size(12, 12);
            treeView1.ImageList.ColorDepth = ColorDepth.Depth32Bit;
            treeView1.ImageList.Images.Add(Shell32.GetShellIcon(3, false));
            treeView1.ImageList.Images.Add(Shell32.GetShellIcon(70, false));
            reloadDynamicPlaylist();
            //            queryTextBox_TextChanged(sender, e);
            toolStripComboBox2.GetControl.Items.AddRange(Enum.GetNames(typeof(Controller.PlaybackOrder)));
            toolStripComboBox2.GetControl.SelectedIndex = 0;
            toolStripComboBox2.GetControl.SelectedIndexChanged += new EventHandler(playbackOrderComboBox_SelectedIndexChanged);

            yomigana = new Yomigana(Controller.UserDirectory + System.IO.Path.DirectorySeparatorChar + "yomiCache", this);
            InitFilterView();
            textBox1.Select();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Controller.Quit();
        }

        private void DefaultUIForm_Activated(object sender, EventArgs e)
        {
            listView1.Select();
        }

        private const int WM_COMMAND = 0x0111;
        private const int WM_GETICON = 0x007f;
        private const int WM_SETICON = 0x0080;
        private const int WM_DWMSENDICONICTHUMBNAIL = 0x0323;
        private const int THBN_CLICKED = 0x1800;
        TaskbarExtension.ThumbButton[] taskbarThumbButtons = new TaskbarExtension.ThumbButton[4];
        protected override void WndProc(ref Message m)
        {
            bool omitBaseProc = false;
            if (m.Msg == WM_GETICON) // && hIconForWindowIcon_Large != IntPtr.Zero
            {
                if ((uint)m.WParam == 1)
                {
                    if (hIconForWindowIcon_Large == IntPtr.Zero)
                    {
                        m.Result = this.Icon.Handle;
                    }
                    else
                    {
                        m.Result = hIconForWindowIcon_Large;
                    }
                    omitBaseProc = true;
                }
            }
            if (TaskbarExt != null)
            {
                switch (m.Msg)
                {
                    case WM_COMMAND:
                        if (((int)m.WParam & 0xffff0000)>>16 == THBN_CLICKED)
                        {
                            switch ((int)m.WParam & 0xffff)
                            {
                                case 0:
                                    Controller.Stop();
                                    break;
                                case 1:
                                    Controller.PrevTrack();
                                    break;
                                case 2:
                                    Controller.TogglePause();
                                    break;
                                case 3:
                                    Controller.NextTrack();
                                    break;
                                default:
                                    Logger.Log((int)m.WParam & 0xff);
                                    break;
                            }
                            omitBaseProc = true;
                        }
                        break;
                    default:
                        if (m.Msg == TaskbarExt.WM_TBC)
                        { // case m_wmTBC�Ƃ���Ɠ{����̂�default�̉��ɂ���܂��E�E�E
                            //				ResetTaskbarProgress();
                            TaskbarExt.ThumbBarAddButtons(taskbarThumbButtons);
                            m.Result = IntPtr.Zero;
                            omitBaseProc = true;
                            break;
                        }
                        break;
                }
            }
            if(!omitBaseProc)base.WndProc(ref m);
        }
        #endregion

        #region Form utility methods
        private void setFormTitle(String title)
        {
            this.Invoke((MethodInvoker)(() => this.Text = (string.IsNullOrEmpty(title) ? "" : title + " - ") + "Lutea✻" + Controller.OutputMode.ToString()));
        }
        #endregion

        #region FilterView utility methods
        private void InitFilterView()
        {
            // clearするとtabControl全体が真っ白になって死ぬ
            int selected = dummyFilterTab.SelectedIndex;
            if (selected < 0) selected = 0;
            while (dummyFilterTab.TabPages.Count > 0)
            {
                dummyFilterTab.TabPages.RemoveAt(0);
            }
            foreach (DBCol col in filterColumns)
            {
                var page = new TabPage(Controller.GetColumnLocalString(col));
                var list = new FilterViewListView();
                //list.SelectEvent += new FilterViewListView.SelectEventHandler(filterViewSelectEvent);
                list.SelectEvent += (c, vals) => { Controller.createPlaylist(list.getQueryString()); };
                list.DoubleClick += (o, arg) => { Controller.createPlaylist(list.getQueryString(), true); };
                list.KeyDown += (o, arg) => { if (arg.KeyCode == Keys.Return)Controller.PlayPlaylistItem(0); };
                list.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
                page.Controls.Add(list);
                page.Padding = new System.Windows.Forms.Padding(0);
                page.Margin = new System.Windows.Forms.Padding(0);
                page.BorderStyle = BorderStyle.None;
                dummyFilterTab.TabPages.Add(page);
                page.Tag = col;
            }
            dummyFilterTab.SelectedIndex = -1;
            dummyFilterTab.SelectedIndex = selected;
        }
        #endregion

        #region playlistView utility methods
        private int emphasizedRowId = -1;
        private void emphasizeRow(int index) // 指定した行を強調表示
        {
            try
            {
                listView1.RedrawItems(emphasizedRowId, emphasizedRowId, true);
            }
            catch { }
            finally
            {
                try
                {
                    emphasizedRowId = index;
                    listView1.RedrawItems(index, index, true);
                }
                catch { }
            }
        }

        private void selectRow(int index) // 指定した行を選択
        {
            if (index < listView1.Items.Count && index >= 0)
            {
                foreach (int i in listView1.SelectedIndices)
                {
                    listView1.Items[i].Selected = false;
                }
                try
                {
                    listView1.Items[index].Selected = true;
                    listView1.EnsureVisible(index);
                }
                catch { }
            }
        }

        private void refreshPlaylistView(string sql) // playlistの内容を更新
        {
            int itemCount = Controller.CurrentPlaylistRows;
            itemCache = new ListViewItem[itemCount];

            int index = Controller.Current.IndexInPlaylist;
            if (sql != null)
            {
                if (sql == textBox1.Text.Replace(@"\n", "\n"))
                {
                    if (itemCount > 0)
                    {
                        textBox1.BackColor = statusColor[(int)QueryStatus.Normal];
                        toolStripStatusLabel2.Text = "Found " + itemCount + " Tracks.";
                    }
                    else
                    {
                        textBox1.BackColor = statusColor[(int)QueryStatus.Error];
                    }
                }
            }

            if (sql != null)
            {
                selectRow(index < 0 ? 0 : index);
            }
            listView1.VirtualListSize = itemCount;
            listView1.Refresh();
            if (sql != null)
            {
                selectRow(index < 0 ? 0 : index);
            }
            emphasizeRow(index);
        }

        private void SetRatingForSelected(int rate)
        {
            string[] filenames = new string[listView1.SelectedIndices.Count];
            for (int i = 0; i < listView1.SelectedIndices.Count; i++)
            {
                filenames[i] = Controller.GetPlaylistRowColumn(listView1.SelectedIndices[i], DBCol.file_name);
            }
            Controller.SetRating(filenames, rate);
        }
        #endregion

        #region queryView utility methods
        internal void reloadDynamicPlaylist()
        {
            char sep = System.IO.Path.DirectorySeparatorChar;
            treeView1.Nodes.Clear();
            TreeNode folder = new TreeNode("クエリ");
            string querydir = Controller.UserDirectory + sep + "query";
            if (!Directory.Exists(querydir))
            {
                Directory.CreateDirectory(querydir);
            }
            if (Directory.GetFileSystemEntries(querydir).Length == 0)
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
            DynamicPlaylist.Load(querydir, folder, null);
            treeView1.Nodes.Add(folder);
            treeView1.ExpandAll();
            previouslyClicked = null;
        }

        private void ExecQueryViewQuery(TreeNode node)
        {
            if (node == null) return;
            if (node.Tag == null) return;
            if (node.Tag is PlaylistEntryFile)
            {
                var ent = (PlaylistEntryFile)node.Tag;
                textBox1.Text = null;
                textBox1.Text = ent.sql.Replace("\n", @"\n");
            }
        }
        #endregion

        // TODO: とりあえずめんどくさいからスレッドでずっと走らせる
        private Thread spectrumAnalyzerThread = null;
        private void SpectrumAnalyzerProc()
        {
            float[] fftdata = null;
            float[] barPosition = null;
            float[] barWidth = null;
            Point[] points = null;
            bool isLogarithmic = FFTLogarithmic; //barPosition,barWidthがLog用で初期化されているかどうか
            Preference.FFTNum fftNum = FFTNum;
            int w = 0;
            int h = 0;
            Bitmap b = null;
            SolidBrush opacityBackgroundBlush = new SolidBrush(Color.White);
            while (true)
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    w = pictureBox2.Width;
                    h = pictureBox2.Height;
                    opacityBackgroundBlush.Color = Color.FromArgb(70, SystemColors.Control);

                    // 描画の条件が変わる等した場合
                    if (b == null || pictureBox2.Image == null || w != b.Width || h != b.Height || isLogarithmic != FFTLogarithmic || fftNum != FFTNum)
                    {
                        if (w * h > 0)
                        {
                            b = new Bitmap(pictureBox2.Width, pictureBox2.Height);
                            using (var g = Graphics.FromImage(b))
                            {
                                g.Clear(SystemColors.Control);
                            }
                            pictureBox2.Image = (Bitmap)b.Clone();
                            barPosition = null;
                            isLogarithmic = FFTLogarithmic;
                            fftNum = FFTNum;
                            fftdata = new float[(int)fftNum / 2];
                            points = new Point[fftdata.Length];
                            for (int i = 0; i < points.Length; i++)
                            {
                                points[i] = new Point();
                            }
                        }
                        else
                        {
                            pictureBox2.Image = null;
                            b = null;
                        }
                    }
                    if (pictureBox2.Image != null)
                    {
                        using (var g = Graphics.FromImage(pictureBox2.Image))
                        {
                            g.DrawImage(b, 0, 0);
                        }
                        pictureBox2.Refresh();
                    }
                }));

                if (SpectrumMode < 0 || SpectrumMode > 4 || !Controller.IsPlaying)
                {
                    b = null;
                    Thread.Sleep(200);
                    continue;
                }
                if ((w * h) > 0)
                {
                    Wrapper.BASS.BASS.IPlayable.FFT bassFFTNum = fftNum == Preference.FFTNum.FFT256
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT256
                        : fftNum == Preference.FFTNum.FFT512
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT512
                        : fftNum == Preference.FFTNum.FFT1024
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT1024
                        : fftNum == Preference.FFTNum.FFT2048
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT2048
                        : fftNum == Preference.FFTNum.FFT4096
                        ? Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT4096
                        : Wrapper.BASS.BASS.IPlayable.FFT.BASS_DATA_FFT8192;
                    Controller.FFTData(fftdata, bassFFTNum);
                    int n = fftdata.Length;
                    float ww = (float)w / n;
                    using (var g = Graphics.FromImage(b))
                    {
                        g.FillRectangle(opacityBackgroundBlush, 0, 0, w, h);
                        var rect = new RectangleF();
                        rect.Width = ww;
                        var brush = new SolidBrush(Color.White);

                        double max = Math.Log10(n);
                        if (barPosition == null)
                        {
                            barPosition = new float[fftdata.Length];
                            barWidth = new float[fftdata.Length];
                            for (int i = 1; i < n; i++)
                            {
                                if (FFTLogarithmic)
                                {
                                    barPosition[i] = (float)(Math.Log10(i) / max * w);
                                    barWidth[i] = (float)((Math.Log10(i + 1) - Math.Log10(i)) / max * w);
                                }
                                else
                                {
                                    barPosition[i] = (float)i * ww;
                                    barWidth[i] = ww;
                                }
                            }
                        }

                        // ちょっとかっこ悪いけどこのループ内で分岐書きたくないので
                        if (SpectrumMode == 0)
                        {
                            for (int j = 0; j < n; j++)
                            {
                                float d = (float)(fftdata[j] * h * j / 8);
                                int c = (int)(Math.Pow(0.03, fftdata[j] * j / 30.0) * 255);
                                rect.X = barPosition[j];
                                rect.Width = barWidth[j];
                                rect.Y = h - d;
                                rect.Height = d;
                                brush.Color = Color.FromArgb((int)c, SpectrumColor1);
                                g.FillRectangle(brush, rect);
                                brush.Color = Color.FromArgb(255 - (int)c, SpectrumColor2);
                                g.FillRectangle(brush, rect);
                            }
                        }
                        else
                        {
                            for (int j = 0; j < n; j++)
                            {
                                points[j].X = (int)barPosition[j];
                                points[j].Y = (int)(h - fftdata[j] * h * j / 8);
                            }
                            points[points.Length - 1].Y = h;
                            switch (SpectrumMode)
                            {
                                case 0:
                                    break;
                                case 1:
                                    g.DrawLines(new Pen(SpectrumColor2), points);
                                    break;
                                case 2:
                                    g.DrawCurve(new Pen(SpectrumColor2), points);
                                    break;
                                case 3:
                                    g.FillPolygon(new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), SpectrumColor2, SpectrumColor1, 90, false), points);
                                    break;
                                case 4:
                                    g.FillClosedCurve(new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, w, h), SpectrumColor2, SpectrumColor1, 90, false), points);
                                    break;
                                default:
                                    Thread.Sleep(100);
                                    break;
                            }
                        }
                    }
                }

                Thread.Sleep(20);
            }
        }

        /* CoverArt関連の関数群
         * 
         * 複数の場所から同時に同一のImageオブジェクトを触ると怒られるようなので、
         * pictureBox.hogeのようなところはmutexで固めまくってる
         * だいぶ汚くなってるけどちょっとでも弄るとすぐ怒られるので不用意に弄れない
         */
        #region CoverArt

        /// <summary>
        /// 現在表示しているCovertArtのリサイズしていないImageオブジェクトを保持。
        /// 縮小版ImageをTagに持つ
        /// </summary>
        Image CurrentCoverArt;

        /// <summary>
        /// pictureBoxを触る時はmutexを取得する。
        /// mutexの取得解放およびpictureBoxへの操作は全てメインスレッドから行う
        /// </summary>
        Mutex m = new Mutex();
        
        /// <summary>
        /// CoverArt画像をバックグラウンドで読み込むスレッドとして動作。
        /// 常に起動したままで、平常時はsleepしている。
        /// 必要になった時にInterruptする。
        /// </summary>

        int CoverArtWidth = 10;
        int CoverArtHeight = 10;
        //
        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr hIconForWindowIcon_Large;
        private void CoverArtLoaderProc()
        {
            int TRANSITION_STEPS = 16;
            int TRANSITION_INTERVAL = 20;
            IEnumerator<int> counter;
            while (true)
            {
                try
                {
                    // Nextを連打したような場合に実際の処理が走らないように少しウェイト
                    Thread.Sleep(300);
                    Image coverArtImage = Controller.Current.CoverArtImage();
                    TaskbarExtension.tagRECT rect = new TaskbarExtension.tagRECT() { left = splitContainer1.SplitterDistance + splitContainer1.SplitterWidth, top = menuStrip1.Height };
                    rect.bottom = rect.top + splitContainer3.Height;
                    rect.right = rect.left + splitContainer3.Width;

                    this.Invoke((MethodInvoker)(() =>
                    {
//                        TaskbarExt.Taskbar.SetThumbnailClip(this.Handle, rect);
                        var oldhIcon_Large = hIconForWindowIcon_Large;
                        hIconForWindowIcon_Large = IntPtr.Zero;
                        if (coverArtImage != null)
                        {
                            int size = Math.Max(coverArtImage.Width, coverArtImage.Height);
                            Bitmap bmp = new Bitmap(size, size);
                            using (var g = Graphics.FromImage(bmp))
                            {
                                g.DrawImage(coverArtImage, (size - coverArtImage.Width) / 2, (size - coverArtImage.Height) / 2, coverArtImage.Width, coverArtImage.Height);
                            }
                            Bitmap bmp2 = new Bitmap(32, 32);
                            int outset = 1;
                            using (var g = Graphics.FromImage(bmp2))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.DrawImage(bmp, -outset, -outset, bmp2.Width + outset * 2, bmp2.Height + outset * 2);
                            }
                            hIconForWindowIcon_Large = (bmp2).GetHicon();
                            // xpだとこちらからSETICONしないといけないっぽいので
                            SendMessage(this.Handle, WM_SETICON, (IntPtr)1, hIconForWindowIcon_Large);
                        }
                        else {
                            SendMessage(this.Handle, WM_SETICON, (IntPtr)1, this.Icon.Handle);
                        }
                        if (oldhIcon_Large != IntPtr.Zero)
                        {
                            DestroyIcon(oldhIcon_Large);
                        }
                    }));
                    if (coverArtImage == null)
                    {
                        try
                        {
                            using (var fs = new System.IO.FileStream("default.jpg", System.IO.FileMode.Open, System.IO.FileAccess.Read))
                            {
                                coverArtImage = System.Drawing.Image.FromStream(fs);
                            }
                        }
                        catch { }
                    }
                    if (coverArtImage == null) coverArtImage = new Bitmap(1, 1);
                    Image resized = null;

                    Image transitionBeforeImage = null;

                    try // Mutex ここから
                    {
                        // 新しい画像をリサイズ
                        coverArtImage.Tag = Util.Util.GetResizedImageWithPadding(coverArtImage, CoverArtWidth, CoverArtHeight);

                        if (true)
                        {
                            // invoke自体は必須ではないのだが、FormsスレッドでのpictureBoxの描画とDrawImageが重なるとだめなので
                            // Formsのスレッドで行う
                            this.Invoke((MethodInvoker)(() =>
                            {
                                transitionBeforeImage = new Bitmap(pictureBox1.Image);
                            }));
                        }

                        CurrentCoverArt = coverArtImage;
                        counter = Util.Util.IntegerCounterIterator(0, TRANSITION_STEPS).GetEnumerator();

                        for (int i = 0; i <= TRANSITION_STEPS; i++)
                        {
                            //                            Image transitionBeforeImage = null;
                            // 実行開始時点でのpictureBoxの画像をCurrentCoverArt.Tagにコピー
                            // 前のtransitionが途中で中断した場合、中断状態の画像からtransitionを継続するため(画像が急に飛ぶのを防ぐ)
                            if (CurrentCoverArt != null)
                            {
                                // invoke自体は必須ではないのだが、FormsスレッドでのpictureBoxの描画とDrawImageが重なるとだめなので
                                // Formsのスレッドで行う
                                Image img = (Image)coverArtImage.Tag;
                                this.Invoke((MethodInvoker)(() =>
                                {
                                    //                                    transitionBeforeImage = new Bitmap(pictureBox1.Image);
                                    if ((resized == null) || (resized.Width != img.Width) || (resized.Height != img.Height))
                                    {

                                        // 1回目またはpictureBoxのリサイズがかかっている時、resizedイメージを生成しなおす
                                        resized = new Bitmap((Image)coverArtImage.Tag);
                                    }
                                }));
                            }
                            Image composed = GetAlphaComposedImage(transitionBeforeImage, resized, (float)i / TRANSITION_STEPS);
                            this.Invoke((MethodInvoker)(() =>
                            {
                                Graphics.FromImage(pictureBox1.Image).DrawImage(composed, 0, 0);
                                pictureBox1.Invalidate();
                            }));
                            Thread.Sleep(TRANSITION_INTERVAL);
                        }
                    }
                    finally
                    {
                    }
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (ThreadInterruptedException)
                {
                }
            }
        }

        /// <summary>
        /// fromを背景にtoをopacityの不透明度で描画したImageオブジェクトを返す
        /// </summary>
        /// <param name="from">背景画像</param>
        /// <param name="to">オーバーレイする画像</param>
        /// <param name="opacity">不透明度</param>
        /// <returns>描画結果のImage(Bitmap)オブジェクト</returns>
        System.Drawing.Imaging.ColorMatrix cm = new System.Drawing.Imaging.ColorMatrix();
        System.Drawing.Imaging.ImageAttributes ia = new System.Drawing.Imaging.ImageAttributes();
        private Image GetAlphaComposedImage(Image from, Image to, float opacity)
        {
            //            Image ret = new Bitmap(from);
            Image ret = new Bitmap(to.Width, to.Height);
            using (var g = Graphics.FromImage(ret))
            {
                g.DrawImage(from, 0, 0);
            }
            AlphaComposedImage(ret, to, opacity);
            return ret;
        }
        private void AlphaComposedImage(Image from, Image to, float opacity)
        {
            //            float f = 1.5F - opacity / 2.0F;
            //            cm.Matrix00 = f;
            //            cm.Matrix11 = f;
            //            cm.Matrix22 = f;
            cm.Matrix33 = opacity;
            ia.SetColorMatrix(cm);
            SolidBrush b = new SolidBrush(Color.FromArgb((int)(Math.Sin(opacity * Math.PI) * 40), Color.White));
            //            Image renderTmp = new Bitmap(to.Width, to.Height);
            using (var gg = Graphics.FromImage(from))
            {
                {
                    gg.DrawImage(to, new Rectangle(0, 0, to.Width, to.Height), 0, 0, to.Width, to.Height, GraphicsUnit.Pixel, ia);
                    gg.FillRectangle(b, 0, 0, to.Width, to.Height);
                    gg.DrawRectangle(Pens.Gray, 0, 0, to.Width - 1, to.Height - 1);
                }
            }
            return;
        }

        #endregion

        #region UI Component events

        #region mainMenu event
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void logToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (logview == null || logview.IsDisposed)
            {
                logview = new LogViewerForm();
                logview.Show();
            }
        }
        #endregion

        #region Playback buttons event
        private void buttonPlay_Click(object sender, EventArgs e)
        {
            Controller.Play();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            Controller.Stop();
        }

        private void prevButton_Click(object sender, EventArgs e)
        {
            Controller.PrevTrack();
        }

        private void buttonPause_Click(object sender, EventArgs e)
        {
            if (Controller.Current.Position > 0)
            {
                Controller.TogglePause();
            }
            else
            {
                Controller.Play();
            }
        }

        private void buttonNext_Click(object sender, EventArgs e)
        {
            Controller.NextTrack();
        }

        private void buttonPrev_Click(object sender, EventArgs e)
        {
            Controller.PrevTrack();
        }
        #endregion

        #region volumeBar event
        private void trackBar1_ValueChanged()
        {
            Controller.Volume = (float)(toolStripXTrackbar1.GetControl.Value / 100.0);
        }
        #endregion

        #region seekBar event
        private void xTrackBar1_OnScroll()
        {
            Controller.Current.Position = (int)xTrackBar1.Value;
        }
        #endregion

        #region QueryView event
        private TreeNode previouslyClicked;

        private void queryView1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;
            TreeNode node = treeView1.GetNodeAt(e.X, e.Y);
            if (node == null) return;
            ExecQueryViewQuery(node);
            previouslyClicked = node;
        }

        private void queryView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node != previouslyClicked)
            {
                ExecQueryViewQuery(e.Node);
            }
            previouslyClicked = e.Node;
        }

        private void reloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            reloadDynamicPlaylist();
        }
        #endregion

        #region queryTextBox event
        private void queryTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            Logger.Debug("textBox1.keyDown " + e.KeyCode);
            if (e.KeyCode == (Keys.Escape | Keys.Return))
            {
                listView1.Select();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Escape)
            {
                listView1.Select();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void queryTextBox_TextChanged(object sender, EventArgs e)
        {
            try
            {
                textBox1.BackColor = statusColor[(int)QueryStatus.Waiting];
                Controller.createPlaylist(textBox1.Text.Replace(@"\n","\n"));
            }
            catch (Exception)
            {
            }
        }
        #endregion

        #region pictureBox event
        private void pictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                try
                {
                    if (coverViewForm != null)
                    {
                        coverViewForm.Close();
                        coverViewForm.Dispose();
                        coverViewForm = null;
                    }
                    if (CurrentCoverArt == null) return;
                    coverViewForm = new CoverViewerForm(CurrentCoverArt);
                    coverViewForm.Show();
                }
                catch { }
            }
        }

        private void pictureBox1_Resize(object sender, EventArgs e)
        {
            Image composed = null;
            try
            {
                m.WaitOne();

                // pictureBoxの新しいサイズを取得
                this.Invoke((MethodInvoker)(() =>
                {
                    CoverArtWidth = pictureBox1.Width;
                    CoverArtHeight = pictureBox1.Height;
                    composed = new Bitmap(CoverArtWidth, CoverArtHeight);
                }));

                if (CurrentCoverArt != null)
                {
                    Image newSize = Util.Util.GetResizedImageWithPadding(CurrentCoverArt, CoverArtWidth, CoverArtHeight);
                    CurrentCoverArt.Tag = newSize;
                    //                    composed = new Bitmap(newSize);
                    AlphaComposedImage(composed, newSize, 1F);
                }
                this.Invoke((MethodInvoker)(() =>
                {
                    pictureBox1.Image = composed;
                    pictureBox1.Invalidate();
                }));
            }
            finally
            {
                m.ReleaseMutex();
                this.Invoke((MethodInvoker)(() =>
                {
                }));
            }
            listView1.Select();
        }
        #endregion

        #region PlaylistView event
        /*
         * 1つのListViewItemオブジェクトを使いまわすのは
         * やっぱり問題があるっぽい(上に遅い)のでやめる
         */
        /// <summary>
        /// playlist viewに表示するitemのキャッシュ
        /// </summary>
        private ListViewItem it = null;
        private ListViewItem[] itemCache;
        private void playlistView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            String[] s = new string[listView1.Columns.Count];
            e.Item = new ListViewItem(s);
            return;
        }

        private void playlistView_KeyDown(object sender, KeyEventArgs e)
        {
            Logger.Debug("Down" + e.KeyCode + e.KeyData + e.KeyValue);
            switch (e.KeyCode)
            {
                case Keys.Return:
                    if (listView1.SelectedIndices.Count > 0)
                    {
                        Controller.PlayPlaylistItem(listView1.SelectedIndices[0]);
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.J:
                    if (e.Modifiers == Keys.Control) // Ctrl + J
                    {
                        if (listView1.SelectedIndices.Count > 0)
                        {
                            Controller.PlayPlaylistItem(listView1.SelectedIndices[0]);
                        }
                    }
                    else // J
                    {
                        if (listView1.SelectedIndices.Count > 0)
                        {
                            selectRow(listView1.SelectedIndices[0] + 1);
                        }
                        else if (Controller.Current.IndexInPlaylist > 0)
                        {
                            selectRow(Controller.Current.IndexInPlaylist + 1);
                        }
                        else
                        {
                            goto case Keys.H;
                        }
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.M:
                    if (e.Modifiers == Keys.Control) // Ctrl + M
                    {
                        if (listView1.SelectedIndices.Count > 0)
                        {
                            Controller.PlayPlaylistItem(listView1.SelectedIndices[0]);
                        }
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.K:
                    if (listView1.SelectedIndices.Count > 0)
                    {
                        selectRow(listView1.SelectedIndices[0] - 1);
                    }
                    else if (Controller.Current.IndexInPlaylist > 0)
                    {
                        selectRow(Controller.Current.IndexInPlaylist - 1);
                    }
                    else
                    {
                        goto case Keys.L;
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.H:
                    selectRow(0);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.L:
                    selectRow(listView1.Items.Count - 1);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Escape:
                    textBox1.Select();
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
                case Keys.OemQuestion: // FIXME: / キーはこれでいいの？
                    textBox1.Select();
                    textBox1.SelectAll();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D1:
                    SetRatingForSelected(10);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D2:
                    SetRatingForSelected(20);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D3:
                    SetRatingForSelected(30);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D4:
                    SetRatingForSelected(40);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D5:
                    SetRatingForSelected(50);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.D0:
                    SetRatingForSelected(0);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        private void playlistView_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                Controller.PlayPlaylistItem(listView1.SelectedIndices[0]);
            }
        }

        #endregion

        #region PlaybackOrderComboBox event
        void playbackOrderComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Controller.playbackOrder = (Controller.PlaybackOrder)toolStripComboBox2.GetControl.SelectedIndex;
        }
        #endregion

        #region filterview event
        public Yomigana yomigana;
        public void refreshFilter(object o)
        {
            refreshFilter(o, null);
        }
        private void createFilterIndex(ListView list, ICollection<ListViewGroup> grps)
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

            list.ContextMenuStrip.Items.Add(new ToolStripSeparator());
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
            list.ContextMenuStrip.Items.AddRange(charTypes);

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
                if(target.OwnerItem != null) 
                    target.OwnerItem.Enabled = true;
                target.DropDownItems.Add(grp.Header, null, (e, obj) =>
                {
                    list.ContextMenuStrip.Hide();
                    list.EnsureVisible(last);
                    list.EnsureVisible(index);

                });
            }
        }
        /// <summary>
        /// FilterViewを更新する。ごちゃごちゃしてるのでなんとかしたい
        /// </summary>
        /// <param name="o"></param>
        public void refreshFilter(object o, string textForSelected = null)
        {
            ListView list = (ListView)(o != null ? o : dummyFilterTab.SelectedTab.Controls[0]);

            list.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            list.ContextMenuStrip.Items.Add("読み修正", null, correctToolStripMenuItem_Click);

            this.Invoke((MethodInvoker)(() =>
            {
                dummyFilterTab.Enabled = false;
                list.Items.Clear();
                toolStripStatusLabel2.Text = "読み仮名を取得しています";
                list.BeginUpdate();
            }));

            ListViewItem selected = null;
            DBCol col = (DBCol)list.Parent.Tag;
            try
            {
                object[][] cache_filter = null;
                // ライブラリからfilterViewに表示する項目を取得
                using (var db = Controller.GetDBConnection())
                using (var stmt = db.Prepare("SELECT " + col.ToString() + " ,COUNT(*) FROM list GROUP BY " + col.ToString() + " ORDER BY COUNT(*) desc;"))
                {
                    cache_filter = stmt.EvaluateAll();
                }

                Dictionary<char, ListViewGroup> groups = new Dictionary<char, ListViewGroup>();
                groups.Add('\0', new ListViewGroup(" " + Controller.GetColumnLocalString(col)));

                int count_sum = 0;
                List<ListViewItem> items = new List<ListViewItem>();
                foreach (var e in cache_filter)
                {
                    string name = e[0].ToString();
                    string count = e[1].ToString();
                    char leading_letter = '\0';
                    string header = "";
                    if (col == DBCol.tagDate)
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
                    var item = new ListViewItem(new string[] { name, count });
                    item.ToolTipText = name + "\n" + count + "項目";
                    item.Group = groups[leading_letter];
                    item.Tag = name;
                    if (name == textForSelected) selected = item;
                    items.Add(item);
                    count_sum += int.Parse(count);
                }
                var item_allFiles = new ListViewItem(new string[] { "すべて", count_sum.ToString() });
                item_allFiles.Group = groups['\0'];
                item_allFiles.Tag = null;
                items.Add(item_allFiles);

                List<ListViewGroup> grpList = new List<ListViewGroup>(groups.Count);
                foreach (var e in groups) grpList.Add(e.Value);
                grpList.Sort((x, y) => x.Header.CompareTo(y.Header));
                this.Invoke((MethodInvoker)(() =>
                {
                    toolStripStatusLabel2.Text = "　 ";
                    list.Groups.AddRange(grpList.ToArray());
                    list.Items.AddRange(items.ToArray());
                    createFilterIndex(list, grpList);
                    list.EndUpdate();
                    if (selected != null)
                    {
                        selected.Selected = true;
                        selected.EnsureVisible();
                    }
                    dummyFilterTab.Enabled = true;
                }));
            }
            catch (Exception e) { Logger.Log(e.ToString()); }
            yomigana.Flush();
        }

        #endregion

        #region Tab event
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int pageIndex = dummyFilterTab.SelectedIndex;
            if (pageIndex < 0) return;
            DBCol col = (DBCol)dummyFilterTab.TabPages[pageIndex].Tag;
            ListView list = (ListView)dummyFilterTab.TabPages[pageIndex].Controls[0];
            if (list.Items.Count == 0)
            {
                Thread th = new Thread(refreshFilter);
                th.IsBackground = true;
                th.Start(list);
                th.Priority = ThreadPriority.Lowest;
            }
            //            this.BeginInvoke((MethodInvoker)(() => refreshFilter()));
        }
        #endregion

        #region splitContainer3 event
        private void splitContainer3_SplitterMoved(object sender, SplitterEventArgs e)
        {
            splitContainer4.SplitterDistance = splitContainer3.SplitterDistance;
            //            splitContainer3.Refresh();
        }
        #endregion

        #region Form ToolStripMenu event
        private void pluginsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var componentManage = new ComponentManager();
            componentManage.ShowDialog();
        }

        private object importThreadLock = new object();
        private ImportForm iform;
        private void importToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (iform != null && !iform.IsDisposed) return;
            lock (importThreadLock)
            {
                FolderBrowserDialog dlg = new FolderBrowserDialog();
                dlg.SelectedPath = LibraryLatestDir;
                DialogResult result = dlg.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    LibraryLatestDir = dlg.SelectedPath;
                    iform = new ImportForm(dlg.SelectedPath);
                    iform.Show();
                    iform.Start();
                }
            }
        }
        #endregion

        #region playlistView ToolStripMenu event
        private void propertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count < 1) return;
            // TODO 
        }

        private void explorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                System.Diagnostics.Process.Start("explorer.exe", "/SELECT, \"" + Controller.GetPlaylistRowColumn(listView1.SelectedIndices[0], DBCol.file_name) + "\"");
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> file_names = new List<string>();
            if (listView1.SelectedIndices.Count > 0)
            {
                foreach (int i in listView1.SelectedIndices)
                {
                    file_names.Add(Controller.GetPlaylistRow(i)[(int)DBCol.file_name].ToString());
                }
            }
            var result = MessageBox.Show(String.Join("\n", file_names.ToArray()), "以下のファイルをライブラリから削除します", MessageBoxButtons.OKCancel);
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Controller.removeItem(file_names);
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            SetRatingForSelected(0);
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            SetRatingForSelected(10);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            SetRatingForSelected(20);
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            SetRatingForSelected(30);
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            SetRatingForSelected(40);
        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            SetRatingForSelected(50);
        }

        private void removeDeadLinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dead_link = Controller.GetDeadLink();
            var result = MessageBox.Show(String.Join("\n", dead_link.ToArray()), "以下のファイルをライブラリから削除します", MessageBoxButtons.OKCancel);
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Controller.removeItem(dead_link);
            }
        }
        #endregion

        #region filterView ToolStripMenu event
        private void correctToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListView lv = (ListView)dummyFilterTab.SelectedTab.Controls[0];
            if (lv.SelectedItems.Count > 0)
            {
                if (lv.SelectedItems[0].Tag == null) return;
                string src = lv.SelectedItems[0].Tag.ToString();
                var lead = yomigana.GetLeadingChars(src);
                if (string.IsNullOrEmpty(lead) || lead.Length == 1) return;
                var correctdialog = new YomiCorrect(lv.SelectedItems[0].Tag.ToString(), yomigana);
                correctdialog.ShowDialog();
            }
        }
        #endregion
        #endregion

        #region pluginInterface methods
        class Preference
        {
            public enum FFTNum
            {
                FFT256 = 256,
                FFT512 = 512,
                FFT1024 = 1024,
                FFT2048 = 2048,
                FFT4096 = 4096,
                FFT8192 = 8192,
            }
            bool _FFTLogarithmic;

            private int spectrumMode;
            [Description("スペクトラムアナライザ描画モード\n0～4")]
            [DefaultValue(0)]
            [Category("Spectrum Analyzer")]
            public int SpectrumMode
            {
                get
                {
                    return spectrumMode;
                }
                set
                {
                    spectrumMode = value;
                }
            }

            [Description("スペクトラムアナライザで横軸を対数にする")]
            [DefaultValue(false)]
            [Category("Spectrum Analyzer")]
            public bool FFTLogarithmic
            {
                get
                {
                    return _FFTLogarithmic;
                }
                set
                {
                    _FFTLogarithmic = value;
                }
            }

            private FFTNum _FFTNum;
            [Description("FFTの細かさ")]
            [DefaultValue(FFTNum.FFT1024)]
            [Category("Spectrum Analyzer")]
            public FFTNum FFTNumber
            {
                get
                {
                    return _FFTNum;
                }
                set
                {
                    _FFTNum = value;
                }
            }

            private Color color1;
            [Description("Color1")]
            [Category("Spectrum Analyzer")]
            public Color SpectrumColor1
            {
                get
                {
                    return color1;
                }
                set
                {
                    color1 = value;
                }
            }

            private Color color2;
            [Description("Color2")]
            [Category("Spectrum Analyzer")]
            public Color SpectrumColor2
            {
                get
                {
                    return color2;
                }
                set
                {
                    color2 = value;
                }
            }

            private Font font_playlistView;
            [Description("プレイリストのフォント")]
            [Category("Playlist View")]
            public Font Font_playlistView
            {
                get
                {
                    return font_playlistView;
                }
                set
                {
                    font_playlistView = value;
                }
            }


            private bool coloredAlbum;
            [Description("アルバムごとに色分けする\n適当")]
            [DefaultValue(true)]
            [Category("Playlist View")]
            public bool ColoredAlbum
            {
                get
                {
                    return coloredAlbum;
                }
                set
                {
                    coloredAlbum = value;
                }
            }
            
            private bool useMediaKey;
            [Description("マルチメディアキーを使用する")]
            [DefaultValue(false)]
            [Category("Hotkey")]
            public bool UseMediaKey
            {
                get
                {
                    return useMediaKey;
                }
                set
                {
                    useMediaKey = value;
                }
            }

            private Keys hotkey_NextTrack;
            [Description("次の曲")]
            [DefaultValue(Keys.None)]
            [Category("Hotkey")]
            public Keys Hotkey_NextTrack
            {
                get
                {
                    return hotkey_NextTrack;
                }
                set
                {
                    hotkey_NextTrack = value;
                }
            }
            
            private Keys hotkey_PrevTrack;
            [Description("前の曲")]
            [DefaultValue(Keys.None)]
            [Category("Hotkey")]
            public Keys Hotkey_PrevTrack
            {
                get
                {
                    return hotkey_PrevTrack;
                }
                set
                {
                    hotkey_PrevTrack = value;
                }
            }

            private Keys hotkey_PlayPause;
            [Description("再生/一時停止")]
            [DefaultValue(Keys.None)]
            [Category("Hotkey")]
            public Keys Hotkey_PlayPause
            {
                get
                {
                    return hotkey_PlayPause;
                }
                set
                {
                    hotkey_PlayPause = value;
                }
            }

            private Keys hotkey_Stop;
            [Description("停止")]
            [DefaultValue(Keys.None)]
            [Category("Hotkey")]
            public Keys Hotkey_Stop
            {
                get
                {
                    return hotkey_Stop;
                }
                set
                {
                    hotkey_Stop = value;
                }
            }
            private DefaultUIForm form;
            public Preference(DefaultUIForm form)
            {
                this.form = form;
                SpectrumMode = form.SpectrumMode;
                _FFTLogarithmic = form.FFTLogarithmic;
                _FFTNum = form.FFTNum;
                color1 = form.SpectrumColor1;
                color2 = form.SpectrumColor2;
                font_playlistView = new Font(form.listView1.Font, 0);
                coloredAlbum = form.ColoredAlbum;
                useMediaKey = form.UseMediaKey;
                hotkey_PlayPause = form.hotkey_PlayPause;
                hotkey_Stop = form.hotkey_Stop;
                hotkey_NextTrack = form.hotkey_NextTrack;
                hotkey_PrevTrack = form.hotkey_PrevTrack;
            }
        }
        private void parseSetting(Dictionary<string, object> setting)
        {
            Util.Util.TryAll(new MethodInvoker[]{
                ()=>{
                    ColumnOrder = (Dictionary<DBCol, int>)setting["PlaylistViewColumnOrder"];
                    ColumnWidth = (Dictionary<DBCol, int>)setting["PlaylistViewColumnWidth"];
                },
//                ()=>textBox1.Text = setting["LastExecutedSQL"].ToString(),
                ()=>config_FormLocation = (Point)setting["WindowLocation"],
                ()=>config_FormSize = (Size)setting["WindowSize"],
                ()=>{
                    this.WindowState = (FormWindowState)setting["WindowState"];
                },
                ()=>this.splitContainer1.SplitterDistance = (int)setting["splitContainer1.SplitterDistance"],
                ()=>this.splitContainer2.SplitterDistance = (int)setting["splitContainer2.SplitterDistance"],
                ()=>{
                    settingCoverArtSize = (int)setting["splitContainer3.SplitterDistance"];
                },
                ()=>LibraryLatestDir = (string)setting["LibraryLatestDir"],
                ()=>SpectrumMode = (int)setting["SpectrumMode"],
                ()=>FFTLogarithmic = (bool)setting["FFTLogarithmic"],
                ()=>FFTNum = (Preference.FFTNum)setting["FFTNum"],
                ()=>SpectrumColor1 = (Color)setting["SpectrumColor1"],
                ()=>SpectrumColor2 = (Color)setting["SpectrumColor2"],
                ()=>displayColumns = (DBCol[])setting["DisplayColumns"],
                ()=>listView1.Font = (System.Drawing.Font)setting["Font_PlaylistView"],
                ()=>ColoredAlbum = (bool)setting["ColoredAlbum"],
                ()=>UseMediaKey = (bool)setting["UseMediaKey"],
                ()=>hotkey_PlayPause = (Keys)setting["Hotkey_PlayPause"],
                ()=>hotkey_Stop = (Keys)setting["Hotkey_Stop"],
                ()=>hotkey_NextTrack = (Keys)setting["Hotkey_NextTrack"],
                ()=>hotkey_PrevTrack = (Keys)setting["Hotkey_PrevTrack"],
            }, null);
        }

        private List<HotKey> hotkeys = new List<HotKey>();
        public void setupHotKeys()
        {
            this.Invoke((MethodInvoker)(() =>
            {
                hotkeys.ForEach((e) => e.Dispose());
                hotkeys.Clear();
                hotkeys.Add(new HotKey(hotkey_PlayPause, (o, e) => { if (Controller.Current.Position > 0)Controller.TogglePause(); else Controller.Play(); }));
                hotkeys.Add(new HotKey(hotkey_Stop, (o, e) => Controller.Stop()));
                hotkeys.Add(new HotKey(hotkey_NextTrack, (o, e) => Controller.NextTrack()));
                hotkeys.Add(new HotKey(hotkey_PrevTrack, (o, e) => Controller.PrevTrack()));
                if (UseMediaKey)
                {
                    hotkeys.Add(new HotKey(0, Keys.MediaPreviousTrack, (o, e) => Controller.PrevTrack()));
                    hotkeys.Add(new HotKey(0, Keys.MediaNextTrack, (o, e) => Controller.NextTrack()));
                    hotkeys.Add(new HotKey(0, Keys.MediaPlayPause, (o, e) => { if (Controller.Current.Position > 0)Controller.TogglePause(); else Controller.Play(); }));
                    hotkeys.Add(new HotKey(0, Keys.MediaStop, (o, e) => Controller.Stop()));
                }
            }));
        }

        public void Init(object _setting)
        {
            if (_setting != null)
            {
                parseSetting((Dictionary<string, object>)_setting);
            }

            ratingRenderer = new RatingRenderer(@"components\rating_on.gif", @"components\rating_off.gif");

            setupPlaylistView();
            if (this.WindowState == FormWindowState.Normal)
            {
                if (!config_FormLocation.IsEmpty) {
                    var locationBackup = this.Location;
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = config_FormLocation;
                    if (System.Windows.Forms.Screen.GetWorkingArea(this).IntersectsWith(this.Bounds) == false)
                    {
                        this.Location = locationBackup;
                    }
                }
                if (!config_FormSize.IsEmpty) this.ClientSize = config_FormSize;
            }
            this.Show();
            pictureBox1.Width = pictureBox1.Height = splitContainer4.SplitterDistance = splitContainer3.SplitterDistance = settingCoverArtSize;
            splitContainer3_SplitterMoved(null, null);
            pictureBox1.Image = new Bitmap(pictureBox1.Width, pictureBox1.Height);

            // プレイリストビューの右クリックにColumn選択を生成
            var column_select = new ToolStripMenuItem("表示する項目");
            foreach (DBCol col in Enum.GetValues(typeof(DBCol)))
            {
                ToolStripMenuItem item = new ToolStripMenuItem(Controller.GetColumnLocalString(col), null, (e, o) =>
                {
                    List<DBCol> displayColumns_list = new List<DBCol>();
                    foreach (ToolStripMenuItem _ in column_select.DropDownItems)
                    {
                        if (_.Checked)
                        {
                            displayColumns_list.Add((DBCol)_.Tag);
                        }
                    }
                    displayColumns = displayColumns_list.ToArray();
                    //                    displayColumns = new DBCol[];
                    setupPlaylistView();
                });
                item.CheckOnClick = true;
                item.Tag = col;
                if (displayColumns.Contains((DBCol)col))
                {
                    item.Checked = true;
                }
                column_select.DropDownItems.Add(item);
            }
            listView1.ContextMenuStrip.Items.Add(column_select);

            // カバーアート関連。これはこの順番で
            // 走らせっぱなしにし、必要な時にinterruptする
            coverArtImageLoaderThread = new Thread(CoverArtLoaderProc);
            pictureBox1_Resize(null, null); // PictureBoxのサイズを憶えるためにここで実行する
            coverArtImageLoaderThread.IsBackground = true;
            coverArtImageLoaderThread.Start();

            try
            {
                TaskbarExt = new TaskbarExtension(this.Handle);
                taskbarImageList = new ImageList();
                taskbarImageList.ImageSize = new System.Drawing.Size(16, 16);
                taskbarImageList.ColorDepth = ColorDepth.Depth32Bit;
                var images = new Bitmap[] { Properties.Resources.stop, Properties.Resources.prev, Properties.Resources.pause, Properties.Resources.next};
                foreach (var img in images)
                {
                    img.MakeTransparent(Color.Magenta);
                }
                taskbarImageList.Images.AddRange(images);
                TaskbarExt.ThumbBarSetImageList(taskbarImageList);
                taskbarThumbButtons[0] = new TaskbarExtension.ThumbButton() { iID = 0, szTip = "Stop", iBitmap = 0, dwMask = TaskbarExtension.ThumbButtonMask.Flags | TaskbarExtension.ThumbButtonMask.ToolTip | TaskbarExtension.ThumbButtonMask.Bitmap, dwFlags = TaskbarExtension.ThumbButtonFlags.Enabled };
                taskbarThumbButtons[1] = new TaskbarExtension.ThumbButton() { iID = 1, szTip = "Prev", iBitmap = 1, dwMask = TaskbarExtension.ThumbButtonMask.Flags | TaskbarExtension.ThumbButtonMask.ToolTip | TaskbarExtension.ThumbButtonMask.Bitmap, dwFlags = TaskbarExtension.ThumbButtonFlags.Enabled };
                taskbarThumbButtons[2] = new TaskbarExtension.ThumbButton() { iID = 2, szTip = "Play/Pause", iBitmap = 2, dwMask = TaskbarExtension.ThumbButtonMask.Flags | TaskbarExtension.ThumbButtonMask.ToolTip | TaskbarExtension.ThumbButtonMask.Bitmap, dwFlags = TaskbarExtension.ThumbButtonFlags.Enabled };
                taskbarThumbButtons[3] = new TaskbarExtension.ThumbButton() { iID = 3, szTip = "Next", iBitmap = 3, dwMask = TaskbarExtension.ThumbButtonMask.Flags | TaskbarExtension.ThumbButtonMask.ToolTip | TaskbarExtension.ThumbButtonMask.Bitmap, dwFlags = TaskbarExtension.ThumbButtonFlags.Enabled };
            }
            catch { }

            setupHotKeys();
        }

        public object GetSetting()
        {
            var setting = new Dictionary<string, object>();
            setting["splitContainer1.SplitterDistance"] = splitContainer1.SplitterDistance;
            setting["splitContainer2.SplitterDistance"] = splitContainer2.SplitterDistance;
            setting["splitContainer3.SplitterDistance"] = splitContainer3.SplitterDistance;
            Dictionary<DBCol, int> PlaylistViewColumnOrder = new Dictionary<DBCol, int>();
            Dictionary<DBCol, int> PlaylistViewColumnWidth = new Dictionary<DBCol, int>();
            for (int i = 0; i < listView1.Columns.Count; i++)
            {
                PlaylistViewColumnOrder[(DBCol)listView1.Columns[i].Tag] = listView1.Columns[i].DisplayIndex;
                PlaylistViewColumnWidth[(DBCol)listView1.Columns[i].Tag] = Math.Max(10, listView1.Columns[i].Width);
            }
            setting["PlaylistViewColumnOrder"] = PlaylistViewColumnOrder;
            setting["PlaylistViewColumnWidth"] = PlaylistViewColumnWidth;
            setting["WindowState"] = this.WindowState;
            if (this.WindowState == FormWindowState.Normal)
            {
                setting["WindowLocation"] = this.Location;
                setting["WindowSize"] = this.ClientSize;
            }
            else
            {
                setting["WindowLocation"] = this.config_FormLocation;
                setting["WindowSize"] = this.config_FormSize;
            }
            setting["LastExecutedSQL"] = textBox1.Text;
            setting["LibraryLatestDir"] = LibraryLatestDir;

            setting["SpectrumMode"] = SpectrumMode;
            setting["FFTLogarithmic"] = FFTLogarithmic;
            setting["FFTNum"] = (int)FFTNum;
            setting["SpectrumColor1"] = SpectrumColor1;
            setting["SpectrumColor2"] = SpectrumColor2;
            setting["DisplayColumns"] = displayColumns;
            setting["Font_PlaylistView"] = listView1.Font;
            setting["ColoredAlbum"] = ColoredAlbum;
            setting["UseMediaKey"] = UseMediaKey;
            setting["Hotkey_PlayPause"] = hotkey_PlayPause;
            setting["Hotkey_Stop"] = hotkey_Stop;
            setting["Hotkey_NextTrack"] = hotkey_NextTrack;
            setting["Hotkey_PrevTrack"] = hotkey_PrevTrack;
            return setting;
        }

        public object GetPreferenceObject()
        {
            return new Preference(this);
        }

        public void SetPreferenceObject(object _pref)
        {
            var pref = (Preference)_pref;
            this.FFTLogarithmic = pref.FFTLogarithmic;
            this.FFTNum = pref.FFTNumber;
            this.SpectrumColor1 = pref.SpectrumColor1;
            this.SpectrumColor2 = pref.SpectrumColor2;
            this.SpectrumMode = pref.SpectrumMode;
            this.listView1.Font = pref.Font_playlistView;
            this.ColoredAlbum = pref.ColoredAlbum;
            this.UseMediaKey = pref.UseMediaKey;
            this.hotkey_PlayPause = pref.Hotkey_PlayPause;
            this.hotkey_Stop = pref.Hotkey_Stop;
            this.hotkey_NextTrack = pref.Hotkey_NextTrack;
            this.hotkey_PrevTrack = pref.Hotkey_PrevTrack;
            setupHotKeys();
            setupPlaylistView();
        }

        public void LibraryInitializeRequired()
        {
            throw new NotImplementedException();
        }

        public void Quit()
        {
            this.Invoke((MethodInvoker)(() =>
            {
                if (logview != null)
                {
                    logview.Close();
                }
                try
                {
                    if (spectrumAnalyzerThread != null) spectrumAnalyzerThread.Abort();
                }
                catch { }

                try
                {
                    if (coverArtImageLoaderThread != null) coverArtImageLoaderThread.Abort();
                }
                catch { }

                yomigana.Dispose();
            }));
            coverArtImageLoaderThread.Abort();

            this.Close();
        }
        #endregion

        private void listView2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView2.SelectedItems.Count > 0)
            {
                Controller.createPlaylist(listView2.SelectedItems[0].Tag.ToString());
            }
        }

        private void listView2_DoubleClick(object sender, EventArgs e)
        {
            if (listView2.SelectedItems.Count > 0 && listView2.SelectedItems[0].Tag != null)
            {
                Controller.createPlaylist(listView2.SelectedItems[0].Tag.ToString());
            }
        }

        private void listView1_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void listView1_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            var row = Controller.GetPlaylistRow(e.ItemIndex);
            if (row == null) return;

            var bounds = e.Bounds;
            var isSelected = (e.State & ListViewItemStates.Selected) != 0;

            var row_above = Controller.GetPlaylistRow(e.ItemIndex - 1);
            var isFirstTrack = row_above == null || row[(int)DBCol.tagAlbum].ToString() != row_above[(int)DBCol.tagAlbum].ToString();
            using (var g = e.Graphics)
            {
                // 背景色描画
                // SystemBrushはsolidBrushのはずだけど
                SolidBrush brush = (SolidBrush)(isSelected ? SystemBrushes.Highlight : e.ItemIndex % 2 == 0 ? SystemBrushes.Window : SystemBrushes.ControlLight);
                if (ColoredAlbum & !isSelected)
                {
                    int c = (row[(int)DBCol.tagAlbum].GetHashCode() % 0x1000000) | 0x00c0c0c0;
                    int red = (c & 0xff0000) >> 16;
                    int green = (c & 0x00ff00) >> 8;
                    int blue = c & 0xff;
                    if (e.ItemIndex % 2 == 0)
                    {
                        red = 255 - (int)((255 - red) * 0.7);
                        green = 255 - (int)((255 - green) * 0.7);
                        blue = 255 - (int)((255 - blue) * 0.7);
                    }
                    brush = new SolidBrush(Color.FromArgb(red, green, blue));
                }
                g.FillRectangle(brush, bounds);

                // columnを表示順にソート
                var cols = new ColumnHeader[listView1.Columns.Count];
                foreach (ColumnHeader head in listView1.Columns)
                {
                    cols[head.DisplayIndex] = head;
                }

                // 各column描画準備
                int pc = 0;
                IntPtr hDC = g.GetHdc();
                IntPtr hFont = (emphasizedRowId == e.ItemIndex ? new Font(listView1.Font, FontStyle.Bold) : listView1.Font).ToHfont();
                IntPtr hOldFont = GDI.SelectObject(hDC, hFont);

                Size size_dots;
                GDI.GetTextExtentPoint32(hDC, "...", "...".Length, out size_dots);
                int y = bounds.Y + (bounds.Height - size_dots.Height) / 2;

                GDI.SelectObject(hDC, GDI.GetStockObject(GDI.WHITE_PEN));
                GDI.SetTextColor(hDC, (uint)(isSelected ? SystemColors.HighlightText.ToArgb() : SystemColors.ControlText.ToArgb()) & 0xffffff);

                // 各column描画
                foreach (ColumnHeader head in cols)
                {
                    GDI.MoveToEx(hDC, bounds.X + pc - 1, bounds.Y, IntPtr.Zero);
                    GDI.LineTo(hDC, bounds.X + pc - 1, bounds.Y + bounds.Height);
                    DBCol col = (DBCol)head.Tag;

                    if (col == DBCol.rating)
                    {
                        int stars = 0;
                        int.TryParse(row[(int)col].ToString(), out stars);
                        stars /= 10;
                        g.ReleaseHdc(hDC);
                        ratingRenderer.Draw(stars, g, bounds.X + pc + 2, bounds.Y, head.Width - 2, bounds.Height);
                        hDC = g.GetHdc();
                        pc += head.Width;
                        continue;
                    }

                    var w = head.Width - 2;
                    var str = row[(int)col].ToString();
                    switch (col)
                    {
                        case DBCol.lastplayed:
                        case DBCol.modify:
                            str = str == "0" ? "-" : Controller.timestamp2DateTime(long.Parse(str)).ToString();
                            break;
                        case DBCol.statDuration:
                            str = Util.Util.getMinSec(int.Parse(str));
                            break;
                        case DBCol.statBitrate:
                            str = (int.Parse(str)) / 1000 + "kbps";
                            break;
                        case DBCol.file_size:
                            int sz = int.Parse(str);
                            str = sz > 1024 * 1024 ? String.Format("{0:0.00}MB", sz / 1024.0 / 1024) : String.Format("{0}KB", sz / 1024);
                            break;
                    }
                    Size size;
                    GDI.GetTextExtentPoint32(hDC, str, str.Length, out size);
                    if (size.Width < w)
                    {
                        var padding = col == DBCol.tagTracknumber || col == DBCol.playcount || col == DBCol.lastplayed || col == DBCol.statBitrate || col == DBCol.statDuration ? (w - size.Width) - 1 : 1;
                        GDI.TextOut(hDC, bounds.X + pc + padding, y, str, str.Length);
                    }
                    else
                    {
                        if (w > size_dots.Width)
                        {
                            int cnt = str.Length + 1;
                            do
                            {
                                GDI.GetTextExtentPoint32(hDC, str, --cnt, out size);
                                if (size.Width > w * 2) cnt = (int)(cnt * 0.75);
                            } while (size.Width + size_dots.Width > w && cnt > 0);
                            GDI.TextOut(hDC, bounds.X + pc + 1, y, str.Substring(0, cnt) + "...", cnt + 3);
                        }
                    }
                    pc += head.Width;
                }

                GDI.DeleteObject(GDI.SelectObject(hDC, hOldFont));
                g.ReleaseHdc(hDC);

                // アルバム先頭マーク描画
                if (isFirstTrack)
                {
                    g.DrawLine(SystemPens.ControlDark, bounds.X, bounds.Y, bounds.X + bounds.Width, bounds.Y);
                }

                // 強調枠描画
                if (emphasizedRowId == e.ItemIndex)
                {
                    g.DrawRectangle(Pens.Navy, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                }
            }
        }

        private void listView1_MouseDown(object sender, MouseEventArgs e)
        {

            //                Logger.Log(item.SubItems.IndexOf(sub).ToString());
            //           }
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            int starwidth = 16;
            var item = listView1.GetItemAt(e.X, e.Y);
            if (item == null) return;
            var sub = item.GetSubItemAt(e.X, e.Y);
            if (sub == null) return;
            if (displayColumns[item.SubItems.IndexOf(sub)] == DBCol.rating)
            {
                if (item.GetSubItemAt(e.X - starwidth * 4, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, DBCol.file_name), 50);
                }
                else if (item.GetSubItemAt(e.X - starwidth * 3, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, DBCol.file_name), 40);
                }
                else if (item.GetSubItemAt(e.X - starwidth * 2, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, DBCol.file_name), 30);
                }
                else if (item.GetSubItemAt(e.X - starwidth * 1, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, DBCol.file_name), 20);
                }
                else if (item.GetSubItemAt(e.X - starwidth / 2, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, DBCol.file_name), 10);
                }
                else
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, DBCol.file_name), 0);
                }
            }
        }

        private void listView1_MouseMove(object sender, MouseEventArgs e)
        {
            var item = listView1.GetItemAt(e.X, e.Y);
            if (item == null) return;
            var sub = item.GetSubItemAt(e.X, e.Y);
            if (sub == null) return;
            if (displayColumns[item.SubItems.IndexOf(sub)] == DBCol.rating)
            {
                return;
            }
            listView1.Cursor = Cursors.Arrow;
        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                if (treeView1.SelectedNode.Tag is PlaylistEntryFile)
                {
                    new QueryEditor((PlaylistEntryFile)treeView1.SelectedNode.Tag, this).ShowDialog();
                }
            }
//            Logger.Log(treeView1.SelectedNode.ToString());
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            treeView1.SelectedNode = e.Node;
        }

        private void newDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode == null || treeView1.SelectedNode.Tag == null) return;
            PlaylistEntryDirectory parent = null;
            if (treeView1.SelectedNode.Tag is PlaylistEntryFile)
            {
                parent = (PlaylistEntryDirectory)treeView1.SelectedNode.Parent.Tag;
            }
            else if (treeView1.SelectedNode.Tag is PlaylistEntryDirectory)
            {
                parent = (PlaylistEntryDirectory)treeView1.SelectedNode.Tag;
            }
            (new QueryDirectoryNew(parent, this)).Show();
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode == null) return;
            if (MessageBox.Show("以下の項目を削除します\n " + treeView1.SelectedNode.Text, "クエリ項目の削除", MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.OK)
            {
                ((PlaylistEntry)treeView1.SelectedNode.Tag).Delete();
            }
            reloadDynamicPlaylist();
        }

        private void RenameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode == null) return;
            new QueryRenameForm((PlaylistEntry)treeView1.SelectedNode.Tag, this).ShowDialog();
            reloadDynamicPlaylist();
        }

        private void CreateQueryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode == null || treeView1.SelectedNode.Tag == null) return;
            PlaylistEntryDirectory parent = null;
            if (treeView1.SelectedNode.Tag is PlaylistEntryFile)
            {
                parent = (PlaylistEntryDirectory)treeView1.SelectedNode.Parent.Tag;
            }
            else if (treeView1.SelectedNode.Tag is PlaylistEntryDirectory)
            {
                parent = (PlaylistEntryDirectory)treeView1.SelectedNode.Tag;
            }
            new QueryEditor(parent.path, this).ShowDialog();
        }
    }
}