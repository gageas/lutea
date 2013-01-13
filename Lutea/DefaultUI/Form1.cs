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
using Gageas.Lutea.Library;

namespace Gageas.Lutea.DefaultUI
{
    [GuidAttribute("406AB8D9-F6CF-4234-8B32-4D0064DA0200")]
    [LuteaComponentInfo("DefaultUI", "Gageas", 0.140, "標準GUI Component")]
    public partial class DefaultUIForm : Form, Lutea.Core.LuteaUIComponentInterface
    {
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
        /// 存在しないファイルを検索するFormを保持
        /// </summary>
        FindDeadLinkDialog findDeadLinkDialog;

        /// <summary>
        /// カバーアートをバックグラウンドで読み込むスレッドを保持
        /// </summary>
        Thread coverArtImageLoaderThread;

        /// <summary>
        /// スペアナを描画するクラスのオブジェクト
        /// </summary>
        SpectrumRenderer SpectrumRenderer = null;

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
        Dictionary<string, int> defaultColumnDisplayWidth = new Dictionary<string, int>(){
            {"tagTracknumber",130},
            {"tagTitle",120},
            {"tagArtist",120},
            {"tagAlbum",80},
            {"tagComment",120},
            {"rating",84},
        };

        /// <summary>
        /// playlistviewに表示するcolumnを定義
        /// </summary>
        string[] displayColumns = null; // DBCol.infoCodec, DBCol.infoCodec_sub, DBCol.modify, DBCol.statChannels, DBCol.statSamplingrate
        Dictionary<string, int> ColumnOrder = new Dictionary<string, int>();
        Dictionary<string, int> ColumnWidth = new Dictionary<string, int>();

        /// <summary>
        /// filter viewに表示するcolumnを定義
        /// </summary>
        string[] filterColumns = { "tagArtist", "tagAlbum", "tagDate", "tagGenre", LibraryDBColumnTextMinimum.infoCodec_sub, LibraryDBColumnTextMinimum.rating, };

        /// <summary>
        /// Ratingの☆を描画
        /// </summary>
        RatingRenderer ratingRenderer;

        /// <summary>
        /// ライブラリデータベースのカラム一覧のキャッシュ
        /// </summary>
        private Column[] Columns = null;

        /// <summary>
        /// settingから読み出した値を保持、あるいはデフォルト値
        /// </summary>
        private Size config_FormSize;
        private Point config_FormLocation;
        private string LibraryLatestDir = "";
        private int settingCoverArtSize = 120;

        private int SpectrumMode = 0;
        private DefaultUIPreference.FFTNum FFTNum = DefaultUIPreference.FFTNum.FFT1024;
        private bool FFTLogarithmic = false;
        private Color SpectrumColor1 = SystemColors.Control;
        private Color SpectrumColor2 = Color.Orange;
        private bool ColoredAlbum = true;
        private Boolean ShowCoverArtInPlaylistView = true;
        private int CoverArtSizeInPlaylistView = 80;
        private Font PlaylistViewFont = null;
        private Font TrackInfoViewFont = null;
        private int PlaylistViewLineHeightAdjustment = 0;

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
            Columns = Controller.Columns;
            InitializeComponent();
            Thread.CurrentThread.Priority = ThreadPriority.Normal;
            trackInfoText.Text = "";
            queryComboBox.ForeColor = System.Drawing.SystemColors.WindowText;
            toolStripStatusLabel1.Text = "";
            PlaylistViewFont = this.listView1.Font;
            TrackInfoViewFont = this.trackInfoText.Font;
            toolStripXTrackbar1.GetControl.ThumbWidth = 30;
        }

        private void ResetPlaylistView()
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
                    ColumnOrder[Columns[(int)listView1.Columns[i].Tag].Name] = listView1.Columns[i].DisplayIndex;
                    ColumnWidth[Columns[(int)listView1.Columns[i].Tag].Name] = Math.Max(10, listView1.Columns[i].Width);
                }
            }

            listView1.Clear();

            // set "dummy" font
            listView1.Font = new Font(this.Font.FontFamily, PlaylistViewFont.Height + PlaylistViewLineHeightAdjustment, GraphicsUnit.Pixel);

            // set "real" font
            listView1.SetHeaderFont(PlaylistViewFont);

            displayColumns = displayColumns.Where(_ => Controller.GetColumnIndexByName(_) >= 0).OrderBy((_) => ColumnOrder.ContainsKey(_) ? ColumnOrder[_] : ColumnOrder.Count).ToArray();
            foreach (string coltext in displayColumns)
            {
                var colheader = new ColumnHeader();
                var col = Controller.GetColumnIndexByName(coltext);
                colheader.Text = Columns[col].LocalText;
                colheader.Tag = col;
                if (ColumnWidth.ContainsKey(coltext))
                {
                    colheader.Width = ColumnWidth[coltext];
                }
                else
                {
                    if (defaultColumnDisplayWidth.ContainsKey(Columns[col].Name))
                    {
                        colheader.Width = defaultColumnDisplayWidth[Columns[col].Name];
                    }
                }
                listView1.Columns.Add(colheader);
                if (Columns[col].Name == LibraryDBColumnTextMinimum.statBitrate)
                {
                    colheader.TextAlign = HorizontalAlignment.Right;
                }
            }

            foreach (ColumnHeader colheader in listView1.Columns)
            {
                var col = (int)(colheader.Tag);
                if (ColumnOrder.ContainsKey(Columns[col].Name))
                {
                    try
                    {
                        colheader.DisplayIndex = ColumnOrder[Columns[col].Name];
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

        private void ResetSpectrumRenderer(bool forceReset = false)
        {
            if (InvokeRequired)
            {
                this.Invoke((Action)(() => { ResetSpectrumRenderer(forceReset); }));
                return;
            }
            if (forceReset && SpectrumRenderer != null)
            {
                SpectrumRenderer.Abort();
                SpectrumRenderer = null;
            }

            if (SpectrumRenderer == null)
            {
                pictureBox2.Top = trackInfoText.Top + trackInfoText.Height;
                pictureBox2.Height = groupBox1.Height - pictureBox2.Top - 2;
                if (pictureBox2.Height > 0)
                {
                    SpectrumRenderer = new SpectrumRenderer(this.pictureBox2, FFTLogarithmic, FFTNum, SpectrumColor1, SpectrumColor2, SpectrumMode);
                    SpectrumRenderer.Start();
                }
            }
        }

        private void ResetTrackInfoView()
        {
            trackInfoText.Font = TrackInfoViewFont;
            trackInfoText.Height = trackInfoText.Font.Height;
            groupBox1.Font = new Font(TrackInfoViewFont.FontFamily, (float)Math.Max(this.Font.Size, TrackInfoViewFont.Size*0.6));
            ResetSpectrumRenderer(true);
        }

        private void ResetProgressBar()
        {
            xTrackBar1.Anchor = AnchorStyles.None;
            var items = menuStrip1.Items;
            var widthSum = menuStrip1.Padding.Left + menuStrip1.Margin.Left;
            for (int i = 0; i < items.Count; i++)
            {
                widthSum += items[i].Width;
            }
            xTrackBar1.Left = widthSum;
            xTrackBar1.Width = this.ClientSize.Width - widthSum;
            xTrackBar1.Height = menuStrip1.Height;
            xTrackBar1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            xTrackBar1.Update();
        }

        #region Application core event handler
        private void trackChange(int index)
        {
            var album = Controller.Current.MetaData("tagAlbum");
            var artist = Controller.Current.MetaData("tagArtist");
            var genre = Controller.Current.MetaData("tagGenre");
            var lyrics = Controller.Current.GetLyrics();
            groupBox1.ContextMenuStrip = null;
            ContextMenuStrip cms = null;
            try
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    richTextBox1.Clear();
                    if (lyrics != null)
                    {
                        if (tabPage4.ImageIndex != 2)
                        {
                            tabPage4.ImageIndex = 2;
                        }
                        richTextBox1.Font = this.Font;
                        richTextBox1.AppendText(string.Join("\n", Util.Util.StripLyricsTimetag(lyrics)));
                    }
                    else
                    {
                        if (tabPage4.ImageIndex != -1)
                        {
                            tabPage4.ImageIndex = -1;
                        }
                    }
                    xTrackBar1.Max = Controller.Current.Length;
                    selectRow(index);
                    emphasizeRow(index);
                    coverArtImageLoaderThread.Interrupt();
                    if (index < 0)
                    {
                        trackInfoText.Text = "";
                        groupBox1.Text = "";
                        setFormTitle(null);
                        setStatusText("Ready ");
                        toolStripStatusLabel1.Text = "";
                        if (SpectrumRenderer != null)
                        {
                            SpectrumRenderer.Abort();
                            SpectrumRenderer.Clear();
                            SpectrumRenderer = null;
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
                        xTrackBar1.Value = 0;
                        xTrackBar1.ThumbText = null;
                        xTrackBar1.Enabled = false;
                    }
                    else
                    {
                        setStatusText("Playing " + Controller.Current.StreamFilename);
                        groupBox1.Text = (album + Util.Util.FormatIfExists(" #{0}", Controller.Current.MetaData("tagTracknumber"))).Replace("&", "&&");
                        trackInfoText.Text = Util.Util.FormatIfExists("{0}{1}",
                            Controller.Current.MetaData("tagTitle"),
                            Util.Util.FormatIfExists(" - {0}",
                               Controller.Current.MetaData("tagArtist"))
                            );
                        setFormTitle(Controller.Current.MetaData("tagTitle") + Util.Util.FormatIfExists(" / {0}", Controller.Current.MetaData("tagArtist")));
                        cms = new ContextMenuStrip();

                        xTrackBar1.Enabled = true;
                    }
                    listView2.Items.Clear();
                }));
                if (index < 0) return;

                ResetSpectrumRenderer();
                var item_splitter = new char[] { '；', ';', '，', ',', '／', '/', '＆', '&', '・', '･', '、', '､', '（', '(', '）', ')', '\n', '\t' };
                var subArtists = artist.Split(item_splitter, StringSplitOptions.RemoveEmptyEntries).ToList();
                var subGenre = genre.Split(item_splitter, StringSplitOptions.RemoveEmptyEntries).ToList().FindAll(e => e.Length > 1);
                var q = String.Join(" OR ", (from __ in from _ in subArtists select _.LCMapUpper().Trim() select String.Format(__.Length > 1 ? @" LCMapUpper(tagArtist) LIKE '%{0}%' " : @" LCMapUpper(tagArtist) = '{0}' ", __.EscapeSingleQuotSQL())).ToArray());
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
                    var cms_album = new ToolStripMenuItem("Album: " + album.Replace("&", "&&"), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE tagAlbum = '" + album.EscapeSingleQuotSQL() + "';"); });
                    var cms_artist = new ToolStripMenuItem("Artist: " + artist.Replace("&", "&&"), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE tagArtist = '" + artist.EscapeSingleQuotSQL() + "';"); });
                    var cms_genre = new ToolStripMenuItem("Genre: " + genre.Replace("&", "&&"), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE tagGenre = '" + genre.EscapeSingleQuotSQL() + "';"); });
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
                            cms.Items.Add("Album: [" + _[1].ToString() + "]" + album_title.Replace("&", "&&"), null, (e, o) => { Controller.CreatePlaylist(query); });
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
                            cms_album.DropDownItems.Add("Album: [" + _[1].ToString() + "]" + album_title.Replace("&", "&&"), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE tagAlbum = '" + album_title + "';"); });
                        }
                    }

                    // 各サブアーティストごとのクエリを作る
                    if (subArtists.Count > 1)
                    {
                        foreach (var _ in subArtists)
                        {
                            var artist_title = _;
                            cms_artist.DropDownItems.Add(artist_title.Trim(), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE LCMapUpper(tagArtist) like '%" + artist_title.LCMapUpper().Trim().EscapeSingleQuotSQL() + "%';"); });
                        }
                    }
                    groupBox1.ContextMenuStrip = cms;

                    // 各サブジャンルごとのクエリを作る
                    if (subGenre.Count > 1)
                    {
                        foreach (var _ in subGenre)
                        {
                            var genre_title = _;
                            cms_genre.DropDownItems.Add(genre_title.Trim(), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE LCMapUpper(tagGenre) like '%" + genre_title.LCMapUpper().Trim().EscapeSingleQuotSQL() + "%';"); });
                        }
                    }
                    groupBox1.ContextMenuStrip = cms;
                }));
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
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
                InitAlbumArtList();
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
                    xTrackBar1.ThumbText = Util.Util.getMinSec(second);
                    toolStripStatusLabel1.Text = (Util.Util.getMinSec(second) + "/" + Util.Util.getMinSec(len));
                    if (TaskbarExt != null)
                    {
                        TaskbarExt.Taskbar.SetProgressState(this.Handle, TaskbarExtension.TbpFlag.Normal);
                        TaskbarExt.Taskbar.SetProgressValue(this.Handle, (ulong)second, (ulong)len);
                    }
                }));
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }

        public void changeVolume()
        {
            this.Invoke((MethodInvoker)(() => {
                toolStripXTrackbar1.GetControl.Value = (int)(Controller.Volume * 100);
                toolStripXTrackbar1.GetControl.ThumbText = ((int)(Controller.Volume * 100)).ToString();
            }));
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
            Controller.PlaylistSortOrderChanged += new Controller.PlaylistSortOrderChangeEvent(OnPlaylistSortOrderChange);
            treeView1.ImageList = new ImageList();
            treeView1.ImageList.ColorDepth = ColorDepth.Depth32Bit;
            treeView1.ImageList.Images.Add(Shell32.GetShellIcon(3, false));
            treeView1.ImageList.Images.Add(Shell32.GetShellIcon(116, false)); //70
            reloadDynamicPlaylist();
            tabControl1.ImageList.Images.Add(Shell32.GetShellIcon(116, true));
            tabControl1.ImageList.Images.Add(Shell32.GetShellIcon(40, true));
            tabControl1.ImageList.Images.Add(Shell32.GetShellIcon(70, true));
            tabPage2.ImageIndex = 0;
            tabPage3.ImageIndex = 1;
            toolStripComboBox2.GetControl.Items.AddRange(Enum.GetNames(typeof(Controller.PlaybackOrder)));
            toolStripComboBox2.GetControl.SelectedIndex = 0;
            toolStripComboBox2.GetControl.SelectedIndexChanged += new EventHandler(playbackOrderComboBox_SelectedIndexChanged);

            menuStrip1.SetBackgroundColorSolid(SystemColors.Control);
            ResetProgressBar();
 
            yomigana = new Yomigana(Controller.UserDirectory + System.IO.Path.DirectorySeparatorChar + "yomiCache", this);
            InitFilterView();
            queryComboBox.Select();
        }

        private bool quitFromCore = false;
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!quitFromCore)
            {
                Controller.Quit();
            }
        }

        private void DefaultUIForm_Activated(object sender, EventArgs e)
        {
            if (tabControl1.SelectedIndex == 0)
            {
                listView1.Select();
            }
            else if (tabControl1.SelectedIndex == 1)
            {
                albumArtListView.Select();
            }
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
                        if (((int)m.WParam & 0xffff0000) >> 16 == THBN_CLICKED)
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
            if (!omitBaseProc) base.WndProc(ref m);
        }

        private FormWindowState prevWindowsState = FormWindowState.Minimized;
        private void DefaultUIForm_SizeChanged(object sender, EventArgs e)
        {
            if (prevWindowsState == FormWindowState.Minimized)
            {
                // ウィンドウ位置を復元
                if (!config_FormLocation.IsEmpty)
                {
                    var locationBackup = this.Location;
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = config_FormLocation;
                    if (System.Windows.Forms.Screen.GetWorkingArea(this).IntersectsWith(this.Bounds) == false)
                    {
                        this.Location = locationBackup;
                    }
                }

                // ウィンドウサイズを復元
                if (this.WindowState != FormWindowState.Maximized && !config_FormSize.IsEmpty)
                {
                    this.ClientSize = config_FormSize;
                }
                ResetProgressBar();
            }
            prevWindowsState = this.WindowState;
        }

        private void DefaultUIForm_Move(object sender, EventArgs e)
        {
            if (this.IsHandleCreated && this.WindowState == FormWindowState.Normal)
            {
                this.config_FormLocation = this.Location;
            }
        }

        private void DefaultUIForm_Resize(object sender, EventArgs e)
        {
            if (this.IsHandleCreated && this.WindowState == FormWindowState.Normal)
            {
                this.config_FormSize = this.ClientSize;
            }
        }
        #endregion

        #region Form utility methods
        private void setFormTitle(String title)
        {
            this.Invoke((MethodInvoker)(() => this.Text = (string.IsNullOrEmpty(title) ? "" : title + " - ") + "Lutea✻" + Controller.OutputMode.ToString()));
        }

        private void setStatusText(String text)
        {
            this.toolStripStatusLabel2.Text = text.ToString().Replace("&", "&&");
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
            foreach (int colid in filterColumns.Select(_ => Controller.GetColumnIndexByName(_)))
            {
                if (colid < 0) continue;
                var col = Columns[colid];
                var page = new TabPage(col.LocalText);
                var list = new FilterViewListView();
                list.SelectEvent += (c, vals) =>
                {
                    if (SupplessFilterViewSelectChangeEvent)
                    {
                        SupplessFilterViewSelectChangeEvent = false;
                        return;
                    }
                    Controller.CreatePlaylist(list.getQueryString());
                };
                list.DoubleClick += (o, arg) => { Controller.CreatePlaylist(list.getQueryString(), true); };
                list.KeyDown += (o, arg) => { if (arg.KeyCode == Keys.Return)Controller.PlayPlaylistItem(0); };
                list.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
                page.Controls.Add(list);
                page.Padding = new System.Windows.Forms.Padding(0);
                page.Margin = new System.Windows.Forms.Padding(0);
                page.BorderStyle = BorderStyle.None;
                dummyFilterTab.TabPages.Add(page);
                page.Tag = colid;
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
                    listView1.FocusedItem = listView1.Items[index];
                    listView1.EnsureVisible(index);
                }
                catch { }
            }
        }

        private void refreshPlaylistView(string sql) // playlistの内容を更新
        {
            int itemCount = Controller.PlaylistRowCount;
            int index = Controller.Current.IndexInPlaylist;
            if (sql != null)
            {
                if (sql == queryComboBox.Text.Replace(@"\n", "\n"))
                {
                    if (itemCount > 0)
                    {
                        queryComboBox.BackColor = statusColor[(int)QueryStatus.Normal];
                        setStatusText("Found " + itemCount + " Tracks.");

                    }
                    else
                    {
                        queryComboBox.BackColor = statusColor[(int)QueryStatus.Error];
                    }
                }
                if (!queryComboBox.Items.Contains(sql))
                {
                    while (queryComboBox.Items.Count > 20)
                    {
                        queryComboBox.Items.RemoveAt(0);
                    }
                    queryComboBox.Items.Add(sql);
                }
            }

            // プレイリストが更新されてアイテムの位置が変わったらカバーアート読み込みキューを消去
            lock (playlistViewImageLoadQueue)
            {
                playlistViewImageLoadQueue.Clear();
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
            if (listView1.SelectedIndices.Count > 0)
            {
                List<string> filenames = new List<string>();
                foreach (int i in listView1.SelectedIndices)
                {
                    filenames.Add(Controller.GetPlaylistRowColumn(i, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)));
                }
                Controller.SetRating(filenames.ToArray(), rate);
            }
        }
        #endregion

        #region queryView utility methods
        internal void reloadDynamicPlaylist()
        {
            TreeNode folder = new TreeNode("クエリ");
            string querydir = Controller.UserDirectory + System.IO.Path.DirectorySeparatorChar + "query";
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
            treeView1.Nodes.Clear();
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
                queryComboBox.Text = null;
                queryComboBox.Text = ent.sql.Replace("\n", @"\n");
            }
        }
        #endregion

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
                    rect.top = 0;
                    rect.left = 0;
                    rect.bottom = 100;
                    rect.right = 100;

                    this.Invoke((MethodInvoker)(() =>
                    {
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
                        else
                        {
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
                        coverArtImage.Tag = ImageUtil.GetResizedImageWithPadding(coverArtImage, CoverArtWidth, CoverArtHeight);

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
                            Image composed = ImageUtil.GetAlphaComposedImage(transitionBeforeImage, resized, (float)i / TRANSITION_STEPS);
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

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                if (treeView1.SelectedNode.Tag is PlaylistEntryFile)
                {
                    new QueryEditor((PlaylistEntryFile)treeView1.SelectedNode.Tag, this).ShowDialog();
                }
            }
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
                    for (int i = 0; i < queryTreeViewContextMenuStrip1.Items.Count; i++)
                    {
                        // 全てのメニューアイテムを一旦enableに
                        var item = queryTreeViewContextMenuStrip1.Items[i];
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
            treeView1.SelectedNode = e.Node;
        }

        private void treeView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            DragDropEffects dde = treeView1.DoDragDrop(e.Item, DragDropEffects.All);
        }

        private void treeView1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TreeNode)))
            {
                TreeNode target = treeView1.GetNodeAt(treeView1.PointToClient(new Point(e.X, e.Y)));
                if (target != null && target.Tag != null && target.Tag is PlaylistEntryDirectory)
                {
                    e.Effect = DragDropEffects.Move;
                    treeView1.SelectedNode = target;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void treeView1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TreeNode)))
            {
                TreeNode target = treeView1.GetNodeAt(treeView1.PointToClient(new Point(e.X, e.Y)));
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
                Controller.CreatePlaylist(((PlaylistEntryFile)tn.Tag).sql, true);
            }
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
            new QueryEditor(parent.Path, this).ShowDialog();
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
                queryComboBox.BackColor = statusColor[(int)QueryStatus.Waiting];
                Controller.CreatePlaylist(queryComboBox.Text.Replace(@"\n", "\n"));
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
            if (!this.IsHandleCreated || !this.Created) return;
            Image composed = null;
            try
            {
                m.WaitOne();

                // pictureBoxの新しいサイズを取得
                var lambda = (MethodInvoker)(() =>
                {
                    CoverArtWidth = Math.Max(1, pictureBox1.Width);
                    CoverArtHeight = Math.Max(1, pictureBox1.Height);
                    composed = new Bitmap(CoverArtWidth, CoverArtHeight);
                });
                if (this.InvokeRequired)
                {
                    this.Invoke(lambda);
                }
                else
                {
                    lambda.Invoke();
                }

                // 新しいサイズでカバーアートを描画
                if (CurrentCoverArt != null)
                {
                    Image newSize = ImageUtil.GetResizedImageWithPadding(CurrentCoverArt, CoverArtWidth, CoverArtHeight);
                    CurrentCoverArt.Tag = newSize;
                    ImageUtil.AlphaComposedImage(composed, newSize, 1F);
                }

                // 描画したBitmapオブジェクトをpictureBoxに設定して再描画
                var lambda2 = (MethodInvoker)(() =>
                {
                    pictureBox1.Image = composed;
                    pictureBox1.Invalidate();
                });
                if (this.InvokeRequired)
                {
                    this.Invoke(lambda2);
                }
                else
                {
                    lambda2.Invoke();
                }
            }
            finally
            {
                m.ReleaseMutex();
                try
                {
                    listView1.Select();
                }
                catch { }
            }
        }
        #endregion

        #region PlaylistView event

        ListViewItem dummyPlaylistViewItem = new ListViewItem(new string[99]);
        private void playlistView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            e.Item = dummyPlaylistViewItem;
        }

        private KeyEventArgs previousPressedKey = null;
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
                    else if (e.Modifiers == Keys.Shift) // Shift + J
                    {
                        // 次のアルバムの先頭トラックを選択
                        string prev_album = null;
                        string album = null;
                        int idx = -1;
                        if (listView1.SelectedIndices.Count > 0)
                        {
                            idx = listView1.SelectedIndices[0];
                        }
                        else if (Controller.Current.IndexInPlaylist > 0)
                        {
                            idx = Controller.Current.IndexInPlaylist;
                        }

                        if (idx != -1)
                        {
                            prev_album = Controller.GetPlaylistRowColumn(idx, Controller.GetColumnIndexByName("tagAlbum"));
                            do
                            {
                                if ((idx + 1 == listView1.Items.Count))
                                {
                                    break;
                                }
                                idx++;
                                album = Controller.GetPlaylistRowColumn(idx, Controller.GetColumnIndexByName("tagAlbum"));
                            } while (album == prev_album);
                            selectRow(idx);
                            listView1.EnsureVisible(Math.Min(idx + 5, listView1.Items.Count - 1));
                        }
                    }
                    else // J
                    {
                        // 次のトラックを選択
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
                    if (e.Modifiers == Keys.Shift) // Shift + K
                    {
                        // 前のアルバムの先頭トラックを選択
                        string prev_album = null;
                        string album = null;
                        int idx = -1;
                        if (listView1.SelectedIndices.Count > 0)
                        {
                            idx = listView1.SelectedIndices[0];
                        }
                        else if (Controller.Current.IndexInPlaylist > 0)
                        {
                            idx = Controller.Current.IndexInPlaylist;
                        }

                        if (idx > 0)
                        {
                            prev_album = Controller.GetPlaylistRowColumn(idx - 1, Controller.GetColumnIndexByName("tagAlbum"));
                            do
                            {
                                idx--;
                                if (idx == 0)
                                {
                                    break;
                                }
                                album = Controller.GetPlaylistRowColumn(idx - 1, Controller.GetColumnIndexByName("tagAlbum"));
                            } while (album == prev_album);
                            selectRow(idx);
                        }
                    }
                    else
                    {
                        // 前のトラックを選択
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
                case Keys.G:
                    if (e.Modifiers == Keys.Shift)
                    {
                        // 末尾へ移動
                        goto case Keys.L;
                    }
                    else
                    {
                        if (previousPressedKey != null && previousPressedKey.KeyCode == Keys.G && previousPressedKey.Modifiers == 0)
                        {
                            // 先頭へ移動
                            goto case Keys.H;
                        }
                    }
                    break;
                case Keys.Escape:
                    queryComboBox.Select();
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
                        listView1.SelectAllItems();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.OemQuestion: // FIXME: / キーはこれでいいの？
                    queryComboBox.Select();
                    queryComboBox.SelectAll();
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
            previousPressedKey = e;
        }

        private void playlistView_DoubleClick(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                Controller.PlayPlaylistItem(listView1.SelectedIndices[0]);
            }
        }

        private void listView1_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
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
                if (target.OwnerItem != null)
                    target.OwnerItem.Enabled = true;
                target.DropDownItems.Add(grp.Header, null, (e, obj) =>
                {
                    list.ContextMenuStrip.Hide();
                    list.EnsureVisible(last);
                    list.EnsureVisible(index);

                });
            }
        }


        private bool SupplessFilterViewSelectChangeEvent = false;
        /// <summary>
        /// FilterViewを更新する。ごちゃごちゃしてるのでなんとかしたい
        /// </summary>
        /// <param name="o"></param>
        public void refreshFilter(object o, string textForSelected = null)
        {
            FilterViewListView list = (FilterViewListView)(o != null ? o : dummyFilterTab.SelectedTab.Controls[0]);

            list.MouseClick += (oo, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Right) { SupplessFilterViewSelectChangeEvent = true; } };

            list.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            list.ContextMenuStrip.Items.Add("読み修正", null, correctToolStripMenuItem_Click);

            this.Invoke((MethodInvoker)(() =>
            {
                dummyFilterTab.Enabled = false;
                list.Items.Clear();
                setStatusText("読み仮名を取得しています");
                list.BeginUpdate();
            }));

            ListViewItem selected = null;
            var colid = (int)list.Parent.Tag;
            var col = Columns[colid];
            try
            {
                object[][] cache_filter = null;
                // ライブラリからfilterViewに表示する項目を取得
                using (var db = Controller.GetDBConnection())
                using (var stmt = db.Prepare("SELECT " + col.Name + " ,COUNT(*) FROM list GROUP BY " + col.Name + " ORDER BY COUNT(*) desc;"))
                {
                    cache_filter = stmt.EvaluateAll();
                }

                Dictionary<char, ListViewGroup> groups = new Dictionary<char, ListViewGroup>();
                groups.Add('\0', new ListViewGroup(" " + col.LocalText));

                int count_sum = 0;
                List<ListViewItem> items = new List<ListViewItem>();
                foreach (var e in cache_filter)
                {
                    string name = e[0].ToString();
                    string count = e[1].ToString();
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

                var grpList = groups.Select((_) => _.Value).ToList().OrderBy((_) => _.Header).ToArray();
                this.Invoke((MethodInvoker)(() =>
                {
                    setStatusText("　 ");
                    list.Groups.AddRange(grpList);
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

        #region relatedAlbumListView event
        private void listView2_DoubleClick(object sender, EventArgs e)
        {
            if (listView2.SelectedItems.Count > 0 && listView2.SelectedItems[0].Tag != null)
            {
                Controller.CreatePlaylist(listView2.SelectedItems[0].Tag.ToString());
            }
        }
        #endregion

        #region filterViewTab event
        private void filterViewTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            int pageIndex = dummyFilterTab.SelectedIndex;
            if (pageIndex < 0) return;
            int colid = (int)dummyFilterTab.TabPages[pageIndex].Tag;
            ListView list = (ListView)dummyFilterTab.TabPages[pageIndex].Controls[0];
            if (list.Items.Count == 0)
            {
                Thread th = new Thread(refreshFilter);
                th.IsBackground = true;
                th.Start(list);
                th.Priority = ThreadPriority.Lowest;
            }
        }
        #endregion

        #region playlistViewTab event
        private void playlistViewTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (tabControl1.SelectedIndex)
            {
                case 0:
                    listView1.Refresh();
                    break;
                case 1:
                    InitAlbumArtList();
                    break;
            }
        }
        #endregion

        #region PlaylistView Tab event
        #endregion

        #region splitContainer3 event
        private void splitContainer3_SplitterMoved(object sender, SplitterEventArgs e)
        {
            splitContainer4.SplitterDistance = splitContainer3.SplitterDistance;
            ResetSpectrumRenderer();
        }
        #endregion

        #region Form ToolStripMenu event
        private void componentToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            for (var i = componentToolStripMenuItem.DropDownItems.Count - 1; i >= 0; i--)
            {
                if (componentToolStripMenuItem.DropDownItems[i].Tag == null) continue;
                if (componentToolStripMenuItem.DropDownItems[i].Tag is LuteaComponentInterface)
                {
                    componentToolStripMenuItem.DropDownItems.RemoveAt(i);
                }
            }
            var components = Lutea.Core.Controller.GetComponents();
            foreach (var component in components)
            {
                LuteaComponentInfo[] info = (LuteaComponentInfo[])component.GetType().GetCustomAttributes(typeof(LuteaComponentInfo), false);
                var menuitem = new ToolStripMenuItem(info.Length > 0 ? info[0].name : component.ToString());
                menuitem.Enabled = component.CanSetEnable();
                menuitem.Checked = component.GetEnable();
                menuitem.Tag = component;
                if (menuitem.Enabled)
                {
                    menuitem.Click += (o, ea) =>
                    {
                        var com = (LuteaComponentInterface)((ToolStripMenuItem)o).Tag;
                        com.SetEnable(!com.GetEnable());
                    };
                }
                componentToolStripMenuItem.DropDownItems.Add(menuitem);
            }
        }
        
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
                    iform = new ImportForm(dlg.SelectedPath, sender == importToolStripMenuItem1 ? true : false);
                    iform.Show();
                    iform.Start();
                }
            }
        }

        private void libraryDBのカスタマイズToolStripMenuItem_Click(object sender, EventArgs e)
        {
            (new DBCustomize()).Show();
        }
        #endregion

        #region playlistView ToolStripMenu event
        private void ScrollToPlayingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var index = Controller.Current.IndexInPlaylist;
            if (index >= 0)
            {
                listView1.EnsureVisible(index);
            }
        }
        
        private void propertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                Shell32.OpenPropertiesDialog(this.Handle, Controller.GetPlaylistRowColumn(listView1.SelectedIndices[0], Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)).Trim());
            }
        }

        private void explorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count > 0)
            {
                System.Diagnostics.Process.Start("explorer.exe", "/SELECT, \"" + Controller.GetPlaylistRowColumn(listView1.SelectedIndices[0], Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)) + "\"");
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> file_names = new List<string>();
            if (listView1.SelectedIndices.Count > 0)
            {
                foreach (int i in listView1.SelectedIndices)
                {
                    file_names.Add(Controller.GetPlaylistRowColumn(i, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)));
                }
            }
            var dlg = new DeleteFilesDialog(file_names.ToArray());
            dlg.ShowDialog(this);
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
            if (this.findDeadLinkDialog != null && !this.findDeadLinkDialog.IsDisposed) return;
            this.findDeadLinkDialog = new FindDeadLinkDialog(this);
            this.findDeadLinkDialog.Show(this);
        }

        private void ReImportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n = listView1.SelectedIndices.Count;
            if (n > 0)
            {
                List<string> filenames = new List<string>();
                int colIndexOfFilename = Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name);
                foreach(int i in listView1.SelectedIndices)
                {
                    filenames.Add(Controller.GetPlaylistRowColumn(i, colIndexOfFilename));
                }
                var importer = new Importer(filenames);
                importer.Start();
            }
        }

        /// <summary>
        /// プレイリストのソート条件をクリアする
        /// PlaylistView→ContextMenuStrip→ソート解除を選択したときのハンドラ
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void clearSortOrderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Controller.SetSortColumn(null);
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

        #region album art list view event
        private void albumArtListView_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            e.Item = dummyPlaylistViewItem;
        }
        private void albumArtListView_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            var albums = AlbumsFiltered;
            if (e.ItemIndex >= albums.Length) return;
            var g = e.Graphics;
            var index = e.ItemIndex;
            var x = e.Bounds.X;
            var y = e.Bounds.Y;
            var album = albums[index][0].ToString();
            var file_name = albums[index][1].ToString();
            var hasCoverArtPic = false;
            var xp = x + 3;
            var yp = y + 3;
            var w = e.Bounds.Width - 4 - 3;
            var h = e.Bounds.Width - 4 - 3;

            // background
            if (coverArts.ContainsKey(album))
            {
                var coverArt = coverArts[album];
                if (coverArt != null && coverArt.Width != 1)
                {
                    hasCoverArtPic = true;
                    var hdc = g.GetHdc();
                    xp = x + 3 + (CoverArtSizeInPlaylistView - coverArt.Width) / 2;
                    yp = y + 3 + (CoverArtSizeInPlaylistView - coverArt.Height) / 2;
                    w = coverArt.Width - 4;
                    h = coverArt.Height - 4;
                    GDI.BitBlt(hdc, xp, yp, CoverArtSizeInPlaylistView + 2, CoverArtSizeInPlaylistView + 2, coverArt.HDC, 0, 0, 0x00CC0020);
                    g.ReleaseHdc(hdc);
                }
                else
                {
                    g.DrawRectangle(Pens.Silver, x + 2, y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4);
                }
            }
            else
            {
                g.DrawRectangle(Pens.Silver, x + 2, y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4);
                if (!string.IsNullOrEmpty(album))
                {
                    lock (playlistViewImageLoadQueue)
                    {
                        if (playlistViewImageLoadQueue.Exists((_) => _.Key == album))
                        {
                            playlistViewImageLoadQueue.First((_) => _.Key == album).redrawIndexesAlbumlist = index;
                        }
                        else
                        {
                            playlistViewImageLoadQueue.Add(new ImageLoaderQueueEntry(album, file_name, new List<int>(new int[] { }), index));
                        }
                    }
                    if (playlistViewImageLoader == null)
                    {
                        playlistViewImageLoader = new Thread(playlistViewImageLoadProc);
                        playlistViewImageLoader.Priority = ThreadPriority.Lowest;
                        playlistViewImageLoader.Start();
                    }
                    if (playlistViewImageLoaderInSleep)
                    {
                        playlistViewImageLoader.Interrupt();
                    }
                }
            }

            // selection
            if ((e.State & ListViewItemStates.Selected) != 0)
            {
                var blush = new SolidBrush(Color.FromArgb(80, 0, 0, 128));
                g.FillRectangle(blush, e.Bounds);
            }

            // overlay
            if (((e.State & ListViewItemStates.Selected) != 0) || !hasCoverArtPic)
            {
                g.DrawString(album, PlaylistViewFont, System.Drawing.Brushes.White, new RectangleF(xp + 2, yp + 2, w - 20, h));
            }
            double rate = 0;
            double.TryParse(albums[index][3].ToString(), out rate);
            g.FillEllipse(new SolidBrush(Color.FromArgb(128, 255, 0, 0)), xp + w - 20, yp + 2, 18, 15);
            g.DrawEllipse(new Pen(Color.FromArgb(128, 255, 255, 255)), xp + w - 20, yp + 2, 18, 15);
            var sf = new StringFormat();
            sf.Trimming = StringTrimming.None;
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            sf.FormatFlags = StringFormatFlags.NoWrap;
            g.DrawString(albums[index][2].ToString(), albumArtListView.Font, System.Drawing.Brushes.White, new RectangleF(xp + w - 18, yp + 2, 15, 15), sf);
        }

        private void albumArtListView_DoubleClick(object sender, EventArgs e)
        {
            if (albumArtListView.SelectedIndices.Count > 0)
            {
                Controller.CreatePlaylist("SELECT * FROM list WHERE tagAlbum IN ('" + AlbumsFiltered[albumArtListView.SelectedIndices[0]][0].ToString().EscapeSingleQuotSQL() + "')", true);
            }
        }
        #endregion

        #region album art list view search text box event
        private void albumArtListViewSearchTextBox_MouseHover(object sender, EventArgs e)
        {
            if (albumArtListViewSearchTextBox.Width < 20)
            {
                var w = albumArtListViewSearchTextBox.Width;
                albumArtListViewSearchTextBox.Width = 100;
                albumArtListViewSearchTextBox.Left -= (100 - w);
                albumArtListViewSearchTextBox.Select();
            }
        }

        private void albumArtListViewSearchTextBox_Leave(object sender, EventArgs e)
        {
            var w = albumArtListViewSearchTextBox.Width;
            albumArtListViewSearchTextBox.Width = 15;
            albumArtListViewSearchTextBox.Left += (w - 15);
        }

        private void albumArtListViewSearchTextBox_TextChanged(object sender, EventArgs e)
        {
            var search = albumArtListViewSearchTextBox.Text;

            albumArtListView.BeginUpdate();
            if (search == "")
            {
                AlbumsFiltered = Albums;
                albumArtListView.VirtualListSize = Albums.Length;
            }
            else
            {
                var migemo = Controller.GetMigemo();
                if (migemo != null)
                {
                    Regex re = null;
                    try
                    {
                        re = migemo.GetRegex(search, RegexOptions.IgnoreCase);
                    }
                    catch
                    {
                        re = new Regex(search, RegexOptions.IgnoreCase);
                    }
                    AlbumsFiltered = Albums.Where((_) => re.IsMatch(_[0].ToString())).ToArray();
                }
                else
                {
                    AlbumsFiltered = Albums.Where((_) => _[0].ToString().IndexOf(search) >= 0).ToArray();
                }
                albumArtListView.VirtualListSize = AlbumsFiltered.Length;
            }
            albumArtListView.EndUpdate();
        }
        #endregion
        #endregion

        #region pluginInterface methods

        private void parseSetting(Dictionary<string, object> setting)
        {
            Util.Util.TryAll(new MethodInvoker[]{
                ()=>{
                    ColumnOrder = (Dictionary<string, int>)setting["PlaylistViewColumnOrder"];
                    ColumnWidth = (Dictionary<string, int>)setting["PlaylistViewColumnWidth"];
                },
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
                ()=>FFTNum = (DefaultUIPreference.FFTNum)setting["FFTNum"],
                ()=>SpectrumColor1 = (Color)setting["SpectrumColor1"],
                ()=>SpectrumColor2 = (Color)setting["SpectrumColor2"],
                ()=>displayColumns = (string[])setting["DisplayColumns"],
                ()=>PlaylistViewFont = (System.Drawing.Font)setting["Font_PlaylistView"],
                ()=>TrackInfoViewFont = (System.Drawing.Font)setting["Font_TrackInfoView"],
                ()=>PlaylistViewLineHeightAdjustment = (int)setting["PlaylistViewLineHeightAdjustment"],
                ()=>ShowCoverArtInPlaylistView = (Boolean)setting["ShowCoverArtInPlaylistView"],
                ()=>CoverArtSizeInPlaylistView = (int)setting["CoverArtSizeInPlaylistView"],
                ()=>ColoredAlbum = (bool)setting["ColoredAlbum"],
                ()=>UseMediaKey = (bool)setting["UseMediaKey"],
                ()=>hotkey_PlayPause = (Keys)setting["Hotkey_PlayPause"],
                ()=>hotkey_Stop = (Keys)setting["Hotkey_Stop"],
                ()=>hotkey_NextTrack = (Keys)setting["Hotkey_NextTrack"],
                ()=>hotkey_PrevTrack = (Keys)setting["Hotkey_PrevTrack"],
            }, null);
        }

        private List<HotKey> hotkeys = new List<HotKey>();
        public void ResetHotKeys()
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
            // プレファレンスを適用
            if (_setting != null)
            {
                parseSetting((Dictionary<string, object>)_setting);
            }

            // 表示するカラムが空の時、タグのカラムを表示することにする
            if (displayColumns == null || displayColumns.Length == 0)
            {
                displayColumns = Columns.Where(_ => _.MappedTagField != null).OrderBy(_ => _.Type).Select(_ => _.Name).ToArray();
            }

            // レーティングの☆描画準備
            ratingRenderer = new RatingRenderer(@"components\rating_on.gif", @"components\rating_off.gif");

            // プレイリストビュー初期化
            ResetPlaylistView();

            // ウィンドウ位置を復元
            if (!config_FormLocation.IsEmpty)
            {
                var locationBackup = this.Location;
                this.StartPosition = FormStartPosition.Manual;
                this.Location = config_FormLocation;
                if (System.Windows.Forms.Screen.GetWorkingArea(this).IntersectsWith(this.Bounds) == false)
                {
                    this.Location = locationBackup;
                }
            }

            // ウィンドウサイズを復元
            if (this.WindowState != FormWindowState.Maximized && !config_FormSize.IsEmpty)
            {
                this.ClientSize = config_FormSize;
            }

            // ウィンドウ表示
            this.Show();
            pictureBox1.Width = pictureBox1.Height = splitContainer4.SplitterDistance = splitContainer3.SplitterDistance = settingCoverArtSize;
            splitContainer3_SplitterMoved(null, null);
            pictureBox1.Image = new Bitmap(pictureBox1.Width, pictureBox1.Height);

            // プレイリストビューの右クリックにColumn選択を生成
            var column_select = new ToolStripMenuItem("表示する項目");
            for (int i = 0; i < Columns.Length; i++)
            {
                var col = Columns[i];
                ToolStripMenuItem item = new ToolStripMenuItem(col.LocalText, null, (e, o) =>
                {
                    List<string> displayColumns_list = new List<string>();
                    foreach (ToolStripMenuItem _ in column_select.DropDownItems)
                    {
                        if (_.Checked)
                        {
                            displayColumns_list.Add(Columns[(int)_.Tag].Name);
                        }
                    }
                    displayColumns = displayColumns_list.ToArray();
                    ResetPlaylistView();
                });
                item.CheckOnClick = true;
                item.Tag = i;
                if (displayColumns.Contains(col.Name))
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
                var images = new Bitmap[] { Properties.Resources.stop, Properties.Resources.prev, Properties.Resources.pause, Properties.Resources.next };
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

            ResetHotKeys();
            ResetTrackInfoView();
        }

        public object GetSetting()
        {
            var setting = new Dictionary<string, object>();
            setting["splitContainer1.SplitterDistance"] = splitContainer1.SplitterDistance;
            setting["splitContainer2.SplitterDistance"] = splitContainer2.SplitterDistance;
            setting["splitContainer3.SplitterDistance"] = splitContainer3.SplitterDistance;
            Dictionary<string, int> PlaylistViewColumnOrder = new Dictionary<string, int>();
            Dictionary<string, int> PlaylistViewColumnWidth = new Dictionary<string, int>();
            for (int i = 0; i < listView1.Columns.Count; i++)
            {
                PlaylistViewColumnOrder[Columns[(int)listView1.Columns[i].Tag].Name] = listView1.Columns[i].DisplayIndex;
                PlaylistViewColumnWidth[Columns[(int)listView1.Columns[i].Tag].Name] = Math.Max(10, listView1.Columns[i].Width);
            }
            setting["PlaylistViewColumnOrder"] = PlaylistViewColumnOrder;
            setting["PlaylistViewColumnWidth"] = PlaylistViewColumnWidth;
            setting["WindowState"] = this.WindowState;
            if (this.WindowState == FormWindowState.Minimized)
            {
                setting["WindowLocation"] = this.config_FormLocation;
            }
            else
            {
                setting["WindowLocation"] = this.Location;
            }
            if (this.WindowState == FormWindowState.Normal)
            {
                setting["WindowSize"] = this.ClientSize;
            }
            else
            {
                setting["WindowSize"] = this.config_FormSize;
            }
            setting["LastExecutedSQL"] = queryComboBox.Text;
            setting["LibraryLatestDir"] = LibraryLatestDir;

            setting["SpectrumMode"] = SpectrumMode;
            setting["FFTLogarithmic"] = FFTLogarithmic;
            setting["FFTNum"] = (int)FFTNum;
            setting["SpectrumColor1"] = SpectrumColor1;
            setting["SpectrumColor2"] = SpectrumColor2;
            setting["DisplayColumns"] = displayColumns;
            setting["Font_PlaylistView"] = PlaylistViewFont;
            setting["Font_TrackInfoView"] = TrackInfoViewFont;
            setting["PlaylistViewLineHeightAdjustment"] = PlaylistViewLineHeightAdjustment;
            setting["ShowCoverArtInPlaylistView"] = ShowCoverArtInPlaylistView;
            setting["CoverArtSizeInPlaylistView"] = CoverArtSizeInPlaylistView;
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
            var pref = new DefaultUIPreference(this);
            pref.SpectrumMode = (DefaultUIPreference.SpectrumModes)this.SpectrumMode;
            pref.FFTLogarithmic = this.FFTLogarithmic;
            pref.FFTNumber = this.FFTNum;
            pref.SpectrumColor1 = this.SpectrumColor1;
            pref.SpectrumColor2 = this.SpectrumColor2;
            pref.Font_playlistView = new Font(PlaylistViewFont, 0); // styleが設定されていないcloneを作る
            pref.Font_trackInfoView = new Font(TrackInfoViewFont, 0);
            pref.PlaylistViewLineHeightAdjustment = this.PlaylistViewLineHeightAdjustment;
            pref.ColoredAlbum = this.ColoredAlbum;
            pref.ShowCoverArtInPlaylistView = this.ShowCoverArtInPlaylistView;
            pref.CoverArtSizeInPlaylistView = this.CoverArtSizeInPlaylistView;
            pref.UseMediaKey = this.UseMediaKey;
            pref.Hotkey_PlayPause = this.hotkey_PlayPause;
            pref.Hotkey_Stop = this.hotkey_Stop;
            pref.Hotkey_NextTrack = this.hotkey_NextTrack;
            pref.Hotkey_PrevTrack = this.hotkey_PrevTrack;

            return pref;
        }

        public void SetPreferenceObject(object _pref)
        {
            var pref = (DefaultUIPreference)_pref;
            this.FFTLogarithmic = pref.FFTLogarithmic;
            this.FFTNum = pref.FFTNumber;
            this.SpectrumColor1 = pref.SpectrumColor1;
            this.SpectrumColor2 = pref.SpectrumColor2;
            this.SpectrumMode = (int)pref.SpectrumMode;
            this.PlaylistViewFont = pref.Font_playlistView;
            this.TrackInfoViewFont = pref.Font_trackInfoView;
            this.PlaylistViewLineHeightAdjustment = pref.PlaylistViewLineHeightAdjustment;
            this.ShowCoverArtInPlaylistView = pref.ShowCoverArtInPlaylistView;
            if (this.CoverArtSizeInPlaylistView != pref.CoverArtSizeInPlaylistView)
            {
                this.CoverArtSizeInPlaylistView = pref.CoverArtSizeInPlaylistView;
                lock (coverArts)
                {
                    playlistViewImageLoader.Interrupt();
                    playlistViewImageLoader = null;
                    playlistViewImageLoaderInSleep = false;
                    coverArts.Clear();
                    if (tabControl1.SelectedIndex == 1)
                    {
                        InitAlbumArtList();
                    }
                }
            }
            this.ColoredAlbum = pref.ColoredAlbum;
            this.UseMediaKey = pref.UseMediaKey;
            this.hotkey_PlayPause = pref.Hotkey_PlayPause;
            this.hotkey_Stop = pref.Hotkey_Stop;
            this.hotkey_NextTrack = pref.Hotkey_NextTrack;
            this.hotkey_PrevTrack = pref.Hotkey_PrevTrack;
            ResetHotKeys();
            ResetPlaylistView();
            ResetTrackInfoView();
        }

        public void LibraryInitializeRequired()
        {
            throw new NotImplementedException();
        }

        public void Quit()
        {
            quitFromCore = true;
            this.Invoke((MethodInvoker)(() =>
            {
                if (logview != null)
                {
                    logview.Close();
                }
                try
                {
                    if (SpectrumRenderer != null) SpectrumRenderer.Abort();
                }
                catch { }

                yomigana.Dispose();
            }));
            coverArtImageLoaderThread.Abort();
            coverArtImageLoaderThread.Join();

            this.Close();
        }
        #endregion

        private Thread playlistViewImageLoader = null;
        private bool playlistViewImageLoaderInSleep = false;
        private void playlistViewImageLoadProc()
        {
            while (true)
            {
                try
                {
                    while (true)
                    {
                        // キューの先頭の要素を取得
                        ImageLoaderQueueEntry task;
                        lock (playlistViewImageLoadQueue)
                        {
                            if (playlistViewImageLoadQueue.Count > 0)
                            {
                                task = playlistViewImageLoadQueue.Last();
                            }
                            else
                            {
                                // キューが空になったら無限ループを向けて待ちに入る
                                break;
                            }
                        }

                        playlistViewImageLoadQueueItemConsume(task);

                        //                        Thread.Sleep(50);
                    }
                    playlistViewImageLoaderInSleep = true;
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (ThreadInterruptedException)
                {
                    playlistViewImageLoaderInSleep = false;
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }
        }

        private class ImageLoaderQueueEntry
        {
            public ImageLoaderQueueEntry(string key, string file_name, List<int> redrawIndexesPlaylist, int redrawIndexesAlbumlist = -1)
            {
                this.Key = key;
                this.file_name = file_name;
                this.redrawIndexesPlaylist = redrawIndexesPlaylist;
                this.redrawIndexesAlbumlist = redrawIndexesAlbumlist;
            }
            public string Key;
            public string file_name;
            public List<int> redrawIndexesPlaylist;
            public int redrawIndexesAlbumlist = -1;
        }

        private void playlistViewImageLoadQueueItemConsume(ImageLoaderQueueEntry tasks)
        {
            try
            {
                if (tasks.Key != null)
                {
                    var album = tasks.Key;
                    if ((CoverArtSizeInPlaylistView == 0) || (coverArts.ContainsKey(album)))
                    {
                        return;
                    }
                    var file_name = tasks.file_name.Trim(); //Controller.GetPlaylistRowColumn(task.Value[0], Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)).ToString().Trim();
                    var orig = Controller.CoverArtImageForFile(file_name);
                    if (orig != null)
                    {
                        var size = CoverArtSizeInPlaylistView;

                        var resize = ImageUtil.GetResizedImageWithoutPadding(orig, size, size);
                        var w = resize.Width;
                        var h = resize.Height;
                        var bordered = new Bitmap(w + 3, h + 3);
                        using (var gg = Graphics.FromImage(bordered))
                        {
                            // ここでアルファ使うと描画が重くなる
                            gg.FillRectangle(Brushes.Silver, new Rectangle(3, 3, w, h));
                            gg.DrawImage(resize, 1, 1);
                            gg.DrawRectangle(Pens.Gray, new Rectangle(0, 0, w + 1, h + 1));
                        }
                        lock (coverArts)
                        {
                            coverArts[album] = new GDI.GDIBitmap(bordered);
                        }

                    }
                    else
                    {
                        coverArts[album] = dummyEmptyBitmapGDI;
                    }
                }
            }
            finally
            {
                List<int> rowsToUpdate = null;
                int rowToUpdateAlbumList = -1;

                lock (playlistViewImageLoadQueue)
                {
                    rowsToUpdate = tasks.redrawIndexesPlaylist;
                    rowToUpdateAlbumList = tasks.redrawIndexesAlbumlist;
                    playlistViewImageLoadQueue.Remove(tasks);
                }

                listView1.Invoke((MethodInvoker)(() =>
                {
                    foreach (var index in rowsToUpdate)
                    {
                        if (index < listView1.VirtualListSize)
                        {
                            listView1.RedrawItems(index, index, true);
                        }
                    }
                    if (rowToUpdateAlbumList != -1)
                    {
                        if (rowToUpdateAlbumList < albumArtListView.VirtualListSize)
                        {
                            albumArtListView.RedrawItems(rowToUpdateAlbumList, rowToUpdateAlbumList, true);

                        }
                    }
                }));
            }
        }
        private List<ImageLoaderQueueEntry> playlistViewImageLoadQueue = new List<ImageLoaderQueueEntry>();
        private Dictionary<string, GDI.GDIBitmap> coverArts = new Dictionary<string, GDI.GDIBitmap>();
        WorkerThread worker = new WorkerThread(true);
        private readonly GDI.GDIBitmap dummyEmptyBitmapGDI = new GDI.GDIBitmap(new Bitmap(1, 1));
        private void listView1_DrawItem(object sender, DrawListViewItemEventArgs e)
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
            var isCont = album == Controller.GetPlaylistRowColumn(index + 1, colIdOfAlbum);
            var isFirstTrack = indexInGroup == 1;

            using (var g = e.Graphics)
            {
                IntPtr hDC = g.GetHdc();
                var bounds_X = bounds.X;
                var bounds_Y = bounds.Y;
                var bounds_Width = bounds.Width;
                var bounds_Height = bounds.Height;


                // 背景色描画
                // SystemBrushはsolidBrushのはずだけど
                GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.DC_BRUSH));
                GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.DC_PEN));
                if (ColoredAlbum & !isSelected)
                {
                    int c = (album.GetHashCode() & 0xFFFFFF) | 0x00c0c0c0;
                    int red = c >> 16;
                    int green = (c >> 8) & 0xff;
                    int blue = c & 0xff;
                    if (index % 2 == 0)
                    {
                        red = 255 - (int)((255 - red) * 0.7);
                        green = 255 - (int)((255 - green) * 0.7);
                        blue = 255 - (int)((255 - blue) * 0.7);
                    }
                    GDI.SetDCBrushColor(hDC, red, green, blue);
                    GDI.SetDCPenColor(hDC, red, green, blue);
                }
                else
                {
                    var fillcolor = ((SolidBrush)(isSelected
                            ? SystemBrushes.Highlight
                            : index % 2 == 0
                                ? SystemBrushes.Window
                                : SystemBrushes.ControlLight)).Color;
                    GDI.SetDCBrushColor(hDC, fillcolor);
                    GDI.SetDCPenColor(hDC, fillcolor);
                }
                GDI.Rectangle(hDC, bounds_X, bounds_Y, bounds_X + bounds_Width, bounds_Y + bounds_Height);

                // カバアート読み込みをキューイング
                if (ShowCoverArtInPlaylistView)
                {
                    if (!string.IsNullOrEmpty(album))
                    {
                        if (((indexInGroup - 2) * bounds_Height) < CoverArtSizeInPlaylistView && !coverArts.ContainsKey(album))
                        {
                            lock (playlistViewImageLoadQueue)
                            {
                                if (playlistViewImageLoadQueue.Exists((_) => _.Key == album))
                                {
                                    playlistViewImageLoadQueue.First((_) => _.Key == album).redrawIndexesPlaylist.Add(index);
                                }
                                else
                                {
                                    playlistViewImageLoadQueue.Add(new ImageLoaderQueueEntry(album, file_name, new List<int>(new int[] { index })));
                                }
                            }
                            if (playlistViewImageLoader == null)
                            {
                                playlistViewImageLoader = new Thread(playlistViewImageLoadProc);
                                playlistViewImageLoader.Priority = ThreadPriority.Lowest;
                                playlistViewImageLoader.Start();
                            }
                            if (playlistViewImageLoaderInSleep)
                            {
                                playlistViewImageLoader.Interrupt();
                            }
                        }
                    }
                }

                // アルバム先頭マーク描画
                if (isFirstTrack)
                {
                    GDI.SetDCPenColor(hDC, SystemPens.ControlDark.Color);
                    GDI.MoveToEx(hDC, bounds_X, bounds_Y, IntPtr.Zero);
                    GDI.LineTo(hDC, bounds_X + bounds_Width, bounds_Y);
                }

                // columnを表示順にソート
                var cols = new ColumnHeader[listView1.Columns.Count];
                foreach (ColumnHeader head in listView1.Columns)
                {
                    cols[head.DisplayIndex] = head;
                }

                // 各column描画準備
                int pc = 0;
                IntPtr hFont = (emphasizedRowId == index ? new Font(PlaylistViewFont, FontStyle.Bold) : PlaylistViewFont).ToHfont();
                IntPtr hOldFont = GDI.SelectObject(hDC, hFont);

                // 強調枠描画
                if (emphasizedRowId == index)
                {
                    GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.NULL_BRUSH));
                    GDI.SetDCPenColor(hDC, Color.Navy);
                    GDI.Rectangle(hDC, bounds_X, bounds_Y, bounds_X + bounds_Width, bounds_Y + bounds_Height);
                }

                Size size_dots;
                GDI.GetTextExtentPoint32(hDC, "...", "...".Length, out size_dots);
                int y = bounds_Y + (bounds_Height - size_dots.Height) / 2;
                int size_dots_Width = size_dots.Width;

                GDI.SelectObject(hDC, GDI.GetStockObject(GDI.StockObjects.WHITE_PEN));
                GDI.SetTextColor(hDC, (uint)(isSelected ? SystemColors.HighlightText.ToArgb() : SystemColors.ControlText.ToArgb()) & 0xffffff);


                // 各column描画
                var row_Length = row.Length;
                foreach (ColumnHeader head in cols)
                {
                    GDI.MoveToEx(hDC, bounds_X + pc - 1, bounds_Y, IntPtr.Zero);
                    GDI.LineTo(hDC, bounds_X + pc - 1, bounds_Y + bounds_Height);
                    int colidx = (int)head.Tag;

                    if (colidx >= row_Length) continue;

                    var col = Columns[colidx];

                    if (col.Type == Library.LibraryColumnType.Rating)
                    {
                        int stars = 0;
                        int.TryParse(row[colidx].ToString(), out stars);
                        stars /= 10;
                        g.ReleaseHdc(hDC);
                        ratingRenderer.Draw(stars, g, bounds_X + pc + 2, bounds_Y, head.Width - 2, bounds_Height);
                        hDC = g.GetHdc();
                        pc += head.Width;
                        continue;
                    }

                    if (ShowCoverArtInPlaylistView && col.Type == Library.LibraryColumnType.TrackNumber)
                    {
                        if (coverArts.ContainsKey(album))
                        {
                            var img = coverArts[album];
                            var margin = 2;
                            if (img != null && img.Width > 1)
                            {
                                GDI.BitBlt(hDC,
                                    bounds_X + pc + (CoverArtSizeInPlaylistView - img.Width) / 2 + margin,
                                    bounds_Y + (indexInGroup == 1 ? margin : 0),
                                    img.Width,
                                    bounds_Height - (indexInGroup == 1 ? margin : 0),
                                    img.HDC,
                                    0,
                                    (indexInGroup - 1) * bounds_Height - (indexInGroup != 1 ? margin : 0) + ((isFirstTrack && !isCont) ? (int)(img.Height*0.30-(bounds_Height/2)) : 0),
                                    0x00CC0020);
                            }
                        }
                    }

                    var w = head.Width - 2;
                    var str = row[colidx].ToString();
                    switch (col.Type)
                    {
                        case Library.LibraryColumnType.Timestamp64:
                            str = str == "0" ? "-" : Util.Util.timestamp2DateTime(long.Parse(str)).ToString();
                            break;
                        case Library.LibraryColumnType.Time:
                            str = Util.Util.getMinSec(int.Parse(str));
                            break;
                        case Library.LibraryColumnType.Bitrate:
                            str = str == "" ? "" : (int.Parse(str)) / 1000 + "kbps";
                            break;
                        case Library.LibraryColumnType.FileSize:
                            int sz = int.Parse(str);
                            str = sz > 1024 * 1024 ? String.Format("{0:0.00}MB", sz / 1024.0 / 1024) : String.Format("{0}KB", sz / 1024);
                            break;
                        default:
                            str = str.Replace("\n", "; ");
                            break;
                    }
                    Size size;
                    GDI.GetTextExtentPoint32(hDC, str, str.Length, out size);
                    if (size.Width < w)
                    {
                        var padding = col.Type == Library.LibraryColumnType.TrackNumber || col.Type == Library.LibraryColumnType.Timestamp64 || col.Type == Library.LibraryColumnType.Bitrate || col.Type == Library.LibraryColumnType.Time ? (w - size.Width) - 1 : 1;
                        GDI.TextOut(hDC, bounds_X + pc + padding, y, str, str.Length);
                    }
                    else
                    {
                        int cnt = str.Length + 1;
                        // 文字のサイズが表示領域の倍以上ある場合一気に切り詰める
                        if (size.Width > (w * 1.1))
                        {
                            cnt = (int)(cnt * (w * 1.1) / (size.Width));
                        }
                        if (w > size_dots_Width)
                        {
                            do
                            {
                                GDI.GetTextExtentPoint32(hDC, str, --cnt, out size);
                            } while (size.Width + size_dots_Width > w && cnt > 0);
                            GDI.TextOut(hDC, bounds_X + pc + 1, y, str.Substring(0, cnt) + "...", cnt + 3);
                        }
                    }
                    pc += head.Width;
                }

                GDI.DeleteObject(GDI.SelectObject(hDC, hOldFont));
                g.ReleaseHdc(hDC);

            }
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            int starwidth = ratingRenderer.EachWidth;
            var item = listView1.GetItemAt(e.X, e.Y);
            if (item == null) return;
            var sub = item.GetSubItemAt(e.X, e.Y);
            if (sub == null) return;
            if (Columns[Controller.GetColumnIndexByName(displayColumns[item.SubItems.IndexOf(sub)])].Type == Library.LibraryColumnType.Rating)
            {
                if (item.GetSubItemAt(e.X - starwidth * 4, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)), 50);
                }
                else if (item.GetSubItemAt(e.X - starwidth * 3, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)), 40);
                }
                else if (item.GetSubItemAt(e.X - starwidth * 2, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)), 30);
                }
                else if (item.GetSubItemAt(e.X - starwidth * 1, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)), 20);
                }
                else if (item.GetSubItemAt(e.X - starwidth / 2, e.Y) == sub)
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)), 10);
                }
                else
                {
                    Controller.SetRating(Controller.GetPlaylistRowColumn(item.Index, Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)), 0);
                }
            }
        }

        private void listView1_MouseMove(object sender, MouseEventArgs e)
        {
            var item = listView1.GetItemAt(e.X, e.Y);
            if (item == null) return;
            var sub = item.GetSubItemAt(e.X, e.Y);
            if (sub == null) return;
            if (Columns[Controller.GetColumnIndexByName(displayColumns[item.SubItems.IndexOf(sub)])].Type == Library.LibraryColumnType.Rating)
            {
                if (listView1.Cursor != Cursors.Hand)
                {
                    listView1.Cursor = Cursors.Hand;
                }
                return;
            }
            if (listView1.Cursor != Cursors.Arrow)
            {
                listView1.Cursor = Cursors.Arrow;
            }
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            Controller.SetSortColumn(Columns[(int)listView1.Columns[e.Column].Tag].Name);
        }

        private void OnPlaylistSortOrderChange(string columnText, Controller.SortOrders sortOrder)
        {
            for (int i = 0; i < listView1.Columns.Count; i++)
            {
                if ((int)listView1.Columns[i].Tag == Controller.GetColumnIndexByName(columnText))
                {
                    listView1.SetSortArrow(i, sortOrder == Controller.SortOrders.Asc ? SortOrder.Ascending : SortOrder.Descending);
                }
                else
                {
                    listView1.SetSortArrow(i, SortOrder.None);
                }
            }
        }

        private void listView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            try
            {
                var count = listView1.SelectedIndices.Count;
                if (count < 1) return;
                List<string> filenames = new List<string>();
                var colIndexOfFilename = Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name);
                foreach(int i in listView1.SelectedIndices)
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

        private object[][] Albums = null;
        private object[][] AlbumsFiltered = null;
        private void InitAlbumArtList()
        {
            albumArtListView.BeginUpdate();
            albumArtListView.Enabled = false;
            albumArtListView.SmallImageList = new ImageList();
            albumArtListView.SmallImageList.ImageSize = new System.Drawing.Size(CoverArtSizeInPlaylistView + 7, CoverArtSizeInPlaylistView + 7);
            albumArtListView.Columns[0].Width = CoverArtSizeInPlaylistView + 7;
            Albums = null;
            AlbumsFiltered = null;
            using (var db = Controller.GetDBConnection())
            {
                using (var stmt = db.Prepare("SELECT tagAlbum,file_name,COUNT(*),AVG(rating) FROM list WHERE tagAlbum != '' GROUP BY tagAlbum ORDER BY tagAlbum ASC;"))
                {
                    Albums = stmt.EvaluateAll();
                    AlbumsFiltered = Albums;
                }
            }
            albumArtListView.VirtualListSize = Albums.Length;
            albumArtListView.Enabled = true;
            albumArtListView.Dock = DockStyle.None;
            albumArtListView.Dock = DockStyle.Fill;
            albumArtListView.EndUpdate();

            albumArtListViewSearchTextBox_Leave(null, null);

            var th = new Thread(() =>
            {
                int index = 0;
                var albums = Albums;
                while (index < albums.Length)
                {
                    if (albums != Albums) return;
                    var e = albums[index];
                    var album = e[0].ToString();
                    var file_name = e[1].ToString();
                    if (playlistViewImageLoadQueue.Count == 0)
                    {
                        if (coverArts.ContainsKey(album))
                        {
                            index++;
                            continue;
                        }
                        lock (playlistViewImageLoadQueue)
                        {
                            playlistViewImageLoadQueue.Add(new ImageLoaderQueueEntry(album, file_name, new List<int>(new int[] { }), index));
                        }

                        if (playlistViewImageLoaderInSleep)
                        {
                            playlistViewImageLoader.Interrupt();
                        }
                        index++;
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            });
            th.Priority = ThreadPriority.BelowNormal;
            th.Start();
        }

        public bool CanSetEnable()
        {
            return false;
        }

        public void SetEnable(bool enable)
        {
        }

        public bool GetEnable()
        {
            return true;
        }
    }
}