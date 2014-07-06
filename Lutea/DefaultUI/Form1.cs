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
    [LuteaComponentInfo("DefaultUI", "Gageas", 1.000, "標準GUI Component")]
    public partial class DefaultUIForm : Form, Lutea.Core.LuteaUIComponentInterface
    {
        /// <summary>
        /// NowPlayingツイートのデフォルトの書式
        /// </summary>
        internal const string DefaultNowPlayingFormat = "#NowPlaying %tagTitle% - %tagArtist% [Lutea]";

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
        /// Windows7以降のタスクバー拡張のための疑似メインウィンドウ
        /// </summary>
        PseudoMainForm pseudoMainForm;

        /// <summary>
        /// 通知Form
        /// </summary>
        NotifyPopupForm NotifyPopup;

        /// <summary>
        /// カバーアートを表示するFormを保持
        /// </summary>
        CoverViewerForm coverViewForm;

        /// <summary>
        /// Importerのインスタンスを保持
        /// </summary>
        Importer importer;

        /// <summary>
        /// プレイリストのカバーアートをバックグラウンドで読み込むオブジェクト
        /// </summary>
        private BackgroundCoverartsLoader backgroundCoverartLoader;

        /// <summary>
        /// filter viewに表示するcolumnを定義
        /// </summary>
        string[] filterColumns = { "tagDate", LibraryDBColumnTextMinimum.infoCodec_sub, LibraryDBColumnTextMinimum.rating, };

        /// <summary>
        /// ライブラリデータベースのカラム一覧のキャッシュ
        /// </summary>
        private Column[] Columns = null;

        /// <summary>
        /// settingから読み出した値を保持、あるいはデフォルト値
        /// </summary>
        private Size config_FormSize;
        private Point config_FormLocation;

        internal DefaultUIPreference pref = new DefaultUIPreference();
        
        private bool ShowNotifyBalloon
        {
            get { return pref.ShowNotifyBalloon; }
            set
            {
                pref.ShowNotifyBalloon = value;
                Controller.onTrackChange -= TrackChangeNotifyPopup;
                if (value)
                {
                    Controller.onTrackChange += TrackChangeNotifyPopup;
                }
            }
        }

        public DefaultUIForm()
        {
#if DEBUG
            logview = new LogViewerForm();
            logview.Show();
#endif
            Columns = Controller.Columns;
            InitializeComponent();
            trackInfoText.Text = "";
            trackInfoText2.Text = "";
            queryComboBox.ForeColor = System.Drawing.SystemColors.WindowText;
            toolStripMenuItem11.Text = "00:00/00:00";
            toolStripXTrackbar1.GetControl.ThumbWidth = TextRenderer.MeasureText("100", this.Font).Width + 10;
            toolStripXTrackbar1.GetControl.MinimumSize = new System.Drawing.Size(toolStripXTrackbar1.GetControl.ThumbWidth * 3, toolStripXTrackbar1.GetControl.Height);
            playlistView.Setup(this, Columns);
        }

        internal void SelectQueryComboBox(bool selectText)
        {
            queryComboBox.Select();
            if (selectText)
            {
                queryComboBox.SelectAll();
            }
        }

        private void ResetPlaylistView()
        {
            playlistView.BeginUpdate();
            playlistView.Enabled = false;

            playlistView.Font = new Font(this.Font.FontFamily, pref.Font_playlistView.Height + pref.PlaylistViewLineHeightAdjustment, GraphicsUnit.Pixel); // set "dummy" font
            playlistView.SetHeaderFont(pref.Font_playlistView); // set "real" font
            playlistView.UseColor = pref.ColoredAlbum;
            playlistView.TrackNumberFormat = pref.TrackNumberFormat;
            playlistView.ShowCoverArt = pref.ShowCoverArtInPlaylistView;
            playlistView.ShowGroup = pref.ShowGroup;
            playlistView.ShowVerticalGrid = pref.ShowVerticalGrid;
//            playlistView.CoverArtSize = pref.CoverArtSizeInPlaylistView;
            playlistView.ResetColumns(pref.DisplayColumns);

            playlistUpdated(null);

            playlistView.EndUpdate();
            playlistView.Enabled = true;
        }

        private void ResetSpectrumRenderer(bool forceReset = false)
        {
            if (InvokeRequired)
            {
                this.Invoke((Action)(() => { ResetSpectrumRenderer(forceReset); }));
                return;
            }
            if (forceReset)
            {
                visualizeView.Abort();
            }
            visualizeView.Setup(pref.FFTLogarithmic, pref.FFTNumber, pref.SpectrumColor1, pref.SpectrumColor2, pref.SpectrumMode);
            visualizeView.Start();
        }

        private void ResetTrackInfoView()
        {
            trackInfoText2.Font = new Font(pref.Font_trackInfoView.FontFamily, (float)Math.Max(this.Font.Size, pref.Font_trackInfoView.Size * 0.6));
            trackInfoText.Font = pref.Font_trackInfoView;
            trackInfoText2.Height = trackInfoText2.Font.Height;
            trackInfoText.Height = trackInfoText.Font.Height;
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
            xTrackBar1.ThumbWidth = this.xTrackBar1.Font.Height * 3;
            xTrackBar1.Update();
        }

        internal void OpenCoverViewForm()
        {
            try
            {
                if (coverViewForm != null)
                {
                    coverViewForm.Close();
                    coverViewForm.Dispose();
                    coverViewForm = null;
                }
                var CurrentCoverArt = Controller.Current.CoverArtImage();
                if (CurrentCoverArt == null) return;
                coverViewForm = new CoverViewerForm(CurrentCoverArt);
                coverViewForm.Show();
            }
            catch { }
        }

        #region Application core event handler
        private void TrackChangeNotifyPopup(int i)
        {
            if (!Controller.IsPlaying) return;
            var t1 = Controller.Current.MetaData("tagTitle");
            var t2 = Controller.Current.MetaData("tagArtist") + " - " + Controller.Current.MetaData("tagAlbum");
            NotifyPopup.DoNotify(t1, t2, Controller.Current.CoverArtImage(), pref.Font_trackInfoView);          
        }

        private void changeTaskbarIcon(int i)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action)(() => { changeTaskbarIcon(i); }));
                return;
            }
            var oldhIcon_Large = hIconForWindowIcon_Large;
            hIconForWindowIcon_Large = IntPtr.Zero;
            var coverArtImage = Controller.Current.CoverArtImage();
            if (coverArtImage != null)
            {
                int size = Math.Max(coverArtImage.Width, coverArtImage.Height);
                Bitmap bmp = new Bitmap(size, size);
                int iconSize;
                using (var g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(coverArtImage, (size - coverArtImage.Width) / 2, (size - coverArtImage.Height) / 2, coverArtImage.Width, coverArtImage.Height);
                    iconSize = g.DpiX > 96 ? 64 : 32;
                }
                Bitmap bmp2 = new Bitmap(iconSize, iconSize);
                int outset = 1;
                using (var g = Graphics.FromImage(bmp2))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, -outset, -outset, bmp2.Width + outset * 2, bmp2.Height + outset * 2);
                }

                hIconForWindowIcon_Large = (bmp2).GetHicon();
                // xpだとこちらからSETICONしないといけないっぽいので
                User32.SendMessage(this.Handle, WM_SETICON, (IntPtr)1, hIconForWindowIcon_Large);
            }
            else
            {
                User32.SendMessage(this.Handle, WM_SETICON, (IntPtr)1, this.Icon.Handle);
            }
            if (oldhIcon_Large != IntPtr.Zero)
            {
                User32.DestroyIcon(oldhIcon_Large);
            }
        }

        private void trackChange(int index)
        {
            var album = Controller.Current.MetaData("tagAlbum");
            var artist = Controller.Current.MetaData("tagArtist");
            var genre = Controller.Current.MetaData("tagGenre");
            var lyrics = Controller.Current.GetLyrics();
            panel1.ContextMenuStrip = null;
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
                        richTextBox1.Font = pref.Font_playlistView;
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
                    playlistView.SelectItemIndirect(index);
                    playlistView.EmphasizeRowIndirect(index);
                    if (index < 0)
                    {
                        trackInfoText.Text = "";
                        trackInfoText2.Text = "";
                        setFormTitle(null);
                        setStatusText("Ready ");
                        toolStripMenuItem11.Text = "00:00/00:00";
                        visualizeView.Abort();
                        visualizeView.Clear();

                        var hIcon = hIconForWindowIcon_Large;
                        User32.SendMessage(this.Handle, WM_SETICON, (IntPtr)1, this.Icon.Handle);
                        hIconForWindowIcon_Large = IntPtr.Zero;
                        if (hIcon != IntPtr.Zero)
                        {
                            User32.DestroyIcon(hIcon);
                        }
                        xTrackBar1.Value = 0;
                        xTrackBar1.ThumbText = null;
                        xTrackBar1.Enabled = false;
                    }
                    else
                    {
                        setStatusText("Playing " + Controller.Current.StreamFilename);
                        trackInfoText2.Text = (album + Util.Util.FormatIfExists(" #{0}", Controller.Current.MetaData("tagTracknumber"))).Replace("&", "&&");
                        trackInfoText.Text = Util.Util.FormatIfExists("{0}{1}",
                            Controller.Current.MetaData("tagTitle"),
                            Util.Util.FormatIfExists(" - {0}",
                               Controller.Current.MetaData("tagArtist").Replace("\n", "; "))
                            );
                        setFormTitle(Controller.Current.MetaData("tagTitle") + Util.Util.FormatIfExists(" / {0}", Controller.Current.MetaData("tagArtist").Replace("\n", "; ")));
                        cms = new ContextMenuStrip();

                        xTrackBar1.Enabled = true;
                    }
                    listView2.Items.Clear();
                }));
                if (index < 0) return;

                ResetSpectrumRenderer();
                var item_splitter = new char[] { '；', ';', '，', ',', '／', '/', '＆', '&', '・', '･', '、', '､', '（', '(', '）', ')', '\n', '\t' };
                var subArtists = artist.Split(item_splitter, StringSplitOptions.RemoveEmptyEntries);
                var subGenre = genre.Split(item_splitter, StringSplitOptions.RemoveEmptyEntries).ToList().FindAll(e => e.Length > 1);
                var q = String.Join(" OR ", (from __ in from _ in subArtists select _.LCMapUpper().Trim() select String.Format(__.Length > 1 ? @" LCMapUpper(tagArtist) LIKE '%{0}%' " : @" LCMapUpper(tagArtist) = '{0}' ", __.EscapeSingleQuotSQL())).ToArray());
                object[][] related_albums = null;
                object[][] multi_disc_albums = null;
                using (var db = Controller.GetDBConnection())
                {
                    // 関連アルバムを引っ張ってくる
                    if (subArtists.Length > 0)
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
                    var cms_artist = new ToolStripMenuItem("Artist: " + artist.Replace("&", "&&").Replace("\n", "; "), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE tagArtist = '" + artist.EscapeSingleQuotSQL() + "';"); });
                    var cms_genre = new ToolStripMenuItem("Genre: " + genre.Replace("&", "&&").Replace("\n", "; "), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE tagGenre = '" + genre.EscapeSingleQuotSQL() + "';"); });
                    cms.Items.AddRange(new ToolStripItem[] { cms_album, cms_artist, cms_genre, new ToolStripSeparator() });

                    // 関連アルバムを登録
                    if (related_albums != null)
                    {
                        var related_albums_where_not_null = related_albums.Where(_ => !string.IsNullOrEmpty(_[0].ToString()));
                        listView2.Items.AddRange(related_albums_where_not_null.Select(_ =>
                        {
                            var album_title = _[0].ToString();
                            var query = "SELECT * FROM list WHERE tagAlbum = '" + album_title.EscapeSingleQuotSQL() + "';";
                            var item = new ListViewItem(new string[] { "", album_title });
                            item.Tag = query;
                            return item;
                        }).ToArray());
                        cms.Items.AddRange(related_albums_where_not_null.Select(_ =>
                        {
                            var album_title = _[0].ToString();
                            var query = "SELECT * FROM list WHERE tagAlbum = '" + album_title.EscapeSingleQuotSQL() + "';";
                            var item = new ToolStripMenuItem("Album: [" + _[1].ToString() + "]" + album_title.Replace("&", "&&"), null, (e, o) => { Controller.CreatePlaylist(query); });
                            return item;
                        }).ToArray());
                    }

                    // 複数ディスクアルバムのクエリを作る
                    cms_album.DropDownItems.AddRange(multi_disc_albums.Where(_ => !string.IsNullOrEmpty(_[0].ToString()))
                        .Select(_ => 
                            new ToolStripMenuItem("Album: [" + _[1].ToString() + "]" + _[0].ToString().Replace("&", "&&"), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE tagAlbum = '" + _[0].ToString() + "';"); })
                        ).ToArray()
                    );

                    // 各サブアーティストごとのクエリを作る
                    cms_artist.DropDownItems.AddRange(
                        artist.Split(item_splitter, StringSplitOptions.RemoveEmptyEntries).Select(_ =>
                            new ToolStripMenuItem(_.Trim(), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE LCMapUpper(tagArtist) like '%" + _.LCMapUpper().Trim().EscapeSingleQuotSQL() + "%';"); })
                        ).ToArray()
                    );

                    // 各サブジャンルごとのクエリを作る
                    cms_genre.DropDownItems.AddRange(
                        subGenre.Select(_ =>
                            new ToolStripMenuItem(_.Trim(), null, (e, o) => { Controller.CreatePlaylist("SELECT * FROM list WHERE LCMapUpper(tagGenre) like '%" + _.LCMapUpper().Trim().EscapeSingleQuotSQL() + "%';"); })
                        ).ToArray()
                    );
                    panel1.ContextMenuStrip = cms;
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
            if (this.IsHandleCreated)
            {
                try
                {
                    this.Invoke(new Controller.PlaylistUpdatedEvent(refreshPlaylistView), new object[] { sql });
                }
                catch (Exception) { }
            }
        }

        private void RefreshAll()
        {
            this.Invoke((MethodInvoker)(() =>
            {
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
                    xTrackBar1.ThumbText = String.Format("{0:0.0}", Math.Floor(1000.0 * second / len) / 10) + "%";
                    toolStripMenuItem11.Text = (Util.Util.getMinSec(second) + "/" + Util.Util.getMinSec(len));

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
            setStatusText("Ready ");
            NotifyPopup = new NotifyPopupForm();
            NotifyPopup.Show();
            Controller.PlaylistUpdated += new Controller.PlaylistUpdatedEvent(playlistUpdated);
            Controller.onElapsedTimeChange += new Controller.VOIDINT(elapsedTimeChange);
            Controller.onTrackChange += new Controller.VOIDINT(trackChange);
            Controller.onTrackChange += changeTaskbarIcon;
            if (pref.ShowNotifyBalloon)
            {
                Controller.onTrackChange -= TrackChangeNotifyPopup;
                Controller.onTrackChange += TrackChangeNotifyPopup;
            }
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

            listView2.Columns[1].Width = listView2.Width;
            ResetProgressBar();
            backgroundCoverartLoader = new BackgroundCoverartsLoader(pref.CoverArtSizeInCoverArtList);
            backgroundCoverartLoader.Complete += new BackgroundCoverartsLoader.LoadComplete(backgroundCoverartLoader_Complete);

            albumArtListViewSearchTextBox.Left = albumArtListViewSearchTextBox.Parent.ClientSize.Width - albumArtListViewSearchTextBox.Width - SystemInformation.VerticalScrollBarWidth;

            splitContainer2.SplitterWidth = 10; // デザイナで設定してもなぜか反映されない
            yomigana = new Yomigana(Controller.UserDirectory + System.IO.Path.DirectorySeparatorChar + "yomiCache", this);
            InitFilterView();
            queryComboBox.Select();
        }

        void backgroundCoverartLoader_Complete(IEnumerable<int> indexes)
        {
            playlistView.Invoke((Action)(() =>
            {
                foreach (var index in indexes)
                {
                    if (albumArtListView.Visible && (index < albumArtListView.VirtualListSize))
                    {
                        albumArtListView.RedrawItems(index, index, true);
                    }
                }
            }));
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
                playlistView.Select();
            }
            else if (tabControl1.SelectedIndex == 1)
            {
                albumArtListView.Select();
            }
        }

        private const int WM_GETICON = 0x007f;
        private const int WM_SETICON = 0x0080;
        private static IntPtr hIconForWindowIcon_Large;
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
            if (!omitBaseProc) base.WndProc(ref m);
        }

        private FormWindowState prevWindowsState = FormWindowState.Minimized;
        private FormWindowState beforeMinimizeWindowState = FormWindowState.Normal;
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
            if (this.WindowState != FormWindowState.Minimized)
            {
                beforeMinimizeWindowState = this.WindowState;
            }
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
                if (this.ClientSize.Height > 0 && this.ClientSize.Width > 0)
                {
                    this.config_FormSize = this.ClientSize;
                }
            }
            if (pref.HideIntoTrayOnMinimize && this.WindowState == FormWindowState.Minimized)
            {
                if (pseudoMainForm != null)
                {
                    pseudoMainForm.QuitOnClose = false;
                    pseudoMainForm.Close();
                    pseudoMainForm.Dispose();
                }
                this.ShowInTaskbar = false;
            }
            else
            {
                try
                {
                    if (this.ShowInTaskbar == false)
                    {
                        visualizeView.Abort();
                        this.ShowInTaskbar = true;
                        treeView1.ExpandAll();
                        ResetSpectrumRenderer(true);
                        ResetProgressBar();
                        if (pseudoMainForm != null)
                        {
                            this.Invoke((Action)(() => { 
                                pseudoMainForm.Dispose(); 
                                pseudoMainForm = new PseudoMainForm(this); 
                                pseudoMainForm.Show(); 
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                }
            }
        }
        #endregion

        #region Form utility methods
        private void setFormTitle(String title)
        {
            this.Invoke((MethodInvoker)(
                () => this.Text
                    = (string.IsNullOrEmpty(title) ? "" : title + " - ") 
                    + "Lutea✻" 
                    + Controller.OutputMode.ToString()
                    + (Controller.IsPlaying ? "@" + Controller.OutputResolution.ToString() : "")
                ));
        }

        private void setStatusText(String text)
        {
            this.toolStripStatusLabel2.Text = text.ToString().Replace("&", "&&");
        }
        #endregion

        #region FilterView utility methods
        private void InitFilterPage(Column col, bool metaTableMode)
        {
            var page = new TabPage(col.LocalText);
            var list = new FilterViewListView(yomigana);
            list.MetaTableMode = metaTableMode;

            list.DoubleClick += (o, arg) => { Controller.CreatePlaylist(list.GetQueryString(), true); };
            list.KeyDown += (o, arg) => { if (arg.KeyCode == Keys.Return)Controller.PlayPlaylistItem(0); };
            list.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            page.Controls.Add(list);
            page.Padding = new System.Windows.Forms.Padding(0);
            page.Margin = new System.Windows.Forms.Padding(0);
            page.BorderStyle = BorderStyle.None;
            dummyFilterTab.TabPages.Add(page);
            page.Tag = Controller.Columns.ToList().IndexOf(col);
        }

        private void InitFilterView()
        {
            // clearするとtabControl全体が真っ白になって死ぬ
            int selected = dummyFilterTab.SelectedIndex;
            if (selected < 0) selected = 0;
            while (dummyFilterTab.TabPages.Count > 1)
            {
                dummyFilterTab.TabPages.RemoveAt(1);
            }
            var columns = Controller.Columns.Where(_ => _.IsTextSearchTarget).Where(_ => _.Name != "tagTitle" && _.Name != "tagComment");
            foreach (var col in columns)
            {
                InitFilterPage(col, true);
            }
            foreach (var colName in filterColumns)
            {
                var colid = Controller.GetColumnIndexByName(colName);
                if (colid == -1) return;
                InitFilterPage(Controller.Columns[colid], false);
            }
            dummyFilterTab.SelectedIndex = -1;
            dummyFilterTab.SelectedIndex = selected;
        }
        #endregion

        #region playlistView utility methods
        private void refreshPlaylistView(string sql) // playlistの内容を更新
        {
            int itemCount = Controller.PlaylistRowCount;
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
            int index = Controller.Current.IndexInPlaylist;
            playlistView.RefreshPlaylist(sql != null, index);
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

        #region UI Component events
        #region CoverArtView event
        private void coverArtView_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                OpenCoverViewForm();
            }
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                splitContainer2.SplitterDistance = 0;
            }
        }
        #endregion

        #region mainMenu event
        private void twToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Controller.IsPlaying) return;
            var re = new Regex("%([0-9a-z_]+)%", RegexOptions.IgnoreCase);
            try
            {
                var text = re.Replace(pref.NowPlayingFormat, (_) => { return Controller.Current.MetaData(Controller.GetColumnIndexByName(_.Groups[1].Value)); });
                Shell32.OpenPath("https://twitter.com/intent/tweet?text=" + Uri.EscapeDataString(text));
            }
            catch (Exception ee)
            {
                Logger.Log(ee);
            }
        }

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
                playlistView.Select();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Escape)
            {
                playlistView.Select();
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

        private bool SupplessFilterViewSelectChangeEvent = false;
        /// <summary>
        /// FilterViewを更新する。ごちゃごちゃしてるのでなんとかしたい
        /// </summary>
        /// <param name="o"></param>
        public void refreshFilter(object o, string textForSelected = null)
        {
            FilterViewListView list = (FilterViewListView)(o != null ? o : dummyFilterTab.SelectedTab.Controls[0]);
            list.SetupContents(textForSelected);
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
            if (!dummyFilterTab.Enabled) return;
            int pageIndex = dummyFilterTab.SelectedIndex;
            if (pageIndex < 0) return;
            if (dummyFilterTab.TabPages[pageIndex].Tag == null) return;
            int colid = (int)dummyFilterTab.TabPages[pageIndex].Tag;
            ListView list = (ListView)dummyFilterTab.TabPages[pageIndex].Controls[0];
            // ratingは黙って更新されている場合があるので，毎回キャッシュを破棄
            // TODO: リアルタイムに追従するようにする
            if (Columns[colid].Name == LibraryDBColumnTextMinimum.rating) list.Items.Clear();
            if (list.Items.Count == 0)
            {
                ThreadPool.QueueUserWorkItem(refreshFilter, list);
            }
        }
        #endregion

        #region playlistViewTab event
        private void playlistViewTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (tabControl1.SelectedIndex)
            {
                case 0:
                    playlistView.Refresh();
                    break;
                case 1:
                    InitAlbumArtList();
                    break;
            }
        }
        #endregion

        #region PlaylistView Tab event
        #endregion

        #region splitContainer1 event
        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (splitContainer2.SplitterDistance != 0)
            {
                splitContainer2.SplitterDistance = splitContainer1.SplitterDistance;
                ResetSpectrumRenderer();
                playlistView.Select();
            }
            splitContainer2.Invalidate(); // Paintを呼ばせるため強制的に再描画をかける
        }
        #endregion

        #region splitContainer2 event
        private void splitContainer2_MouseClick(object sender, MouseEventArgs e)
        {
            var sc = (SplitContainer)sender;
            if (sc.SplitterDistance == 0)
            {
                splitContainer2.SplitterDistance = splitContainer1.SplitterDistance;
            }
            else
            {
                sc.SplitterDistance = 0;
            }
        }

        private void splitContainer2_Paint(object sender, PaintEventArgs e)
        {
            var sc = splitContainer2;
            e.Graphics.FillRectangle(SystemBrushes.ControlDark, sc.Width / 2 - 10 - e.ClipRectangle.X, sc.SplitterDistance + 4, 2, 2);
            e.Graphics.FillRectangle(SystemBrushes.ControlDark, sc.Width / 2, sc.SplitterDistance + 4, 2, 2);
            e.Graphics.FillRectangle(SystemBrushes.ControlDark, sc.Width / 2 + 10, sc.SplitterDistance + 4, 2, 2);
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

        private void importToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (toolStripProgressBar1.Visible) return;
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.SelectedPath = pref.LibraryLatestDir;
            DialogResult result = dlg.ShowDialog();
            ((ToolStripMenuItem)sender).GetCurrentParent().Visible = false;
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                pref.LibraryLatestDir = dlg.SelectedPath;
                DoImport(new string[] { dlg.SelectedPath }, sender == importToolStripMenuItem1 ? true : false);
            }
        }

        private void DoImport(IEnumerable<string> paths, bool fastMode, bool filemode = false)
        {
            if (importer != null) return;
            toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
            if (toolStripProgressBar1.Tag != null)
            {
                toolStripProgressBar1.Click -= (EventHandler)toolStripProgressBar1.Tag;
            }
            toolStripProgressBar1.Visible = true;
            toolStripProgressBar1.Value = 0;
            toolStripProgressBar1.Maximum = int.MaxValue;
            if (filemode)
            {
                importer = Importer.CreateFileImporter(paths, fastMode);
            }
            else
            {
                importer = Importer.CreateFolderImporter(paths, fastMode);
            }
            EventHandler evt = (x, y) => { 
                var ret = MessageBox.Show("中断しますか？", "インポート処理", MessageBoxButtons.OKCancel); 
                if (ret == System.Windows.Forms.DialogResult.OK) { 
                    importer.Abort(); 
                    toolStripProgressBar1.Visible = false;
                    importer = null;
                } 
            };
            toolStripProgressBar1.Click += evt;
            toolStripProgressBar1.Tag = evt;
            importer.SetMaximum_read += (_) =>
            {
                this.Invoke((MethodInvoker)(() =>
                {
                    toolStripProgressBar1.Style = ProgressBarStyle.Continuous;
                    toolStripProgressBar1.Maximum = _ + 1;
                }));
            };
            importer.Step_read += () => { this.Invoke((MethodInvoker)(() => { toolStripProgressBar1.PerformStep(); })); };
            importer.Complete += () => { this.Invoke((MethodInvoker)(() => { toolStripProgressBar1.Visible = false; importer = null; })); };
            importer.Message += (s) => { Logger.Debug(s); };
            importer.Start();
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
                playlistView.EnsureVisibleIndirect(index);
            }
        }
        
        private void propertyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (playlistView.SelectedIndices.Count > 0)
            {
                Shell32.OpenPropertiesDialog(this.Handle, Controller.GetPlaylistRowColumn(playlistView.GetSelectedObjects()[0], Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)).Trim());
            }
        }

        private void explorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (playlistView.SelectedIndices.Count > 0)
            {
                System.Diagnostics.Process.Start("explorer.exe", "/SELECT, \"" + Controller.GetPlaylistRowColumn(playlistView.GetSelectedObjects()[0], Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name)) + "\"");
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (playlistView.SelectedIndices.Count > 0)
            {
                int colIndexOfFilename = Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name);
                var dlg = new DeleteFilesDialog(
                    playlistView
                        .GetSelectedObjects()
                        .Select(_ => Controller.GetPlaylistRowColumn(_, colIndexOfFilename)).ToArray());
                dlg.ShowDialog(this);
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            playlistView.SetRatingForSelectedItems(0);
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            playlistView.SetRatingForSelectedItems(10);
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            playlistView.SetRatingForSelectedItems(20);
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            playlistView.SetRatingForSelectedItems(30);
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            playlistView.SetRatingForSelectedItems(40);
        }

        private void toolStripMenuItem7_Click(object sender, EventArgs e)
        {
            playlistView.SetRatingForSelectedItems(50);
        }

        private void removeDeadLinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (toolStripProgressBar1.Visible) return;
            toolStripProgressBar1.Style = ProgressBarStyle.Continuous;
            if (toolStripProgressBar1.Tag != null)
            {
                toolStripProgressBar1.Click -= (EventHandler)toolStripProgressBar1.Tag;
            }
            toolStripProgressBar1.Visible = true;
            ThreadPool.QueueUserWorkItem((__) =>
            {
                try
                {
                    var result = Controller.GetDeadLink(
                        (_) => { this.Invoke((MethodInvoker)(() => { toolStripProgressBar1.Maximum = _; })); },
                        (_) => { this.Invoke((MethodInvoker)(() => { toolStripProgressBar1.Value = _; })); }
                    );
                    this.Invoke((MethodInvoker)(() =>
                    {
                        var dlg = new DeleteFilesDialog(result.ToArray());
                        dlg.Show();
                    }));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
                finally
                {
                    this.Invoke((MethodInvoker)(() =>
                    {
                        toolStripProgressBar1.Visible = false;
                    }));
                }
            });
        }

        private void ReImportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (playlistView.SelectedIndices.Count > 0)
            {
                int colIndexOfFilename = Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name);
                DoImport(playlistView
                        .GetSelectedObjects()
                        .Select(_ => Controller.GetPlaylistRowColumn(_, colIndexOfFilename)).ToArray(), true, true);
            }
        }

        private void ReImportFullToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (playlistView.SelectedIndices.Count > 0)
            {
                int colIndexOfFilename = Controller.GetColumnIndexByName(LibraryDBColumnTextMinimum.file_name);
                DoImport(playlistView
                        .GetSelectedObjects()
                        .Select(_ => Controller.GetPlaylistRowColumn(_, colIndexOfFilename)).ToArray(), false, true);
            }
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var setstr = playlistView.lastSelectedString;
            if (!String.IsNullOrEmpty(setstr))
            {
                try
                {
                    Clipboard.SetDataObject(setstr, true, 10, 50);
                }
                catch (Exception)
                {
                    // なんか成功しても例外吐いてくることあって意味分からんから握りつぶす
                }
            }
        }

        private void SearchByThisValueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (playlistView.lastSelectedColumnId >= 0)
            {
                var colName = Controller.Columns[playlistView.lastSelectedColumnId].Name;
                Controller.CreatePlaylist("SELECT file_name FROM list WHERE " + colName + " = '" + playlistView.lastSelectedString.EscapeSingleQuotSQL() + "'");
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

        private void QueueSelectedTrackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (playlistView.GetSelectedObjects().Length > 0)
            {
                Controller.QueueNext(playlistView.GetSelectedObjects()[0]);
            }
        }

        private void QueueStopPlayingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Controller.QueueStop();
        }

        private void QueueClearCurrentQueueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Controller.QueueClear();
        }
        #endregion

        #region album art list view ToolStripMenu event
        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!toolStripComboBox1.Enabled) return;
            pref.CoverArtSizeInCoverArtList = int.Parse(toolStripComboBox1.SelectedItem.ToString());
            backgroundCoverartLoader.Reset(pref.CoverArtSizeInCoverArtList);
            InitAlbumArtList();
        }

        private void coverArtViewContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            var item = toolStripComboBox1;
            toolStripComboBox1.Enabled = false;
            item.SelectedItem = pref.CoverArtSizeInCoverArtList.ToString();
            toolStripComboBox1.Enabled = true;
        }
        #endregion

        #region album art list view event

        private ListViewItem dummyPlaylistViewItem = new ListViewItem(new string[99]);
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
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // background
            if (backgroundCoverartLoader.IsCached(album))
            {
                var coverArt = backgroundCoverartLoader.GetCache(album);
                if (coverArt != null && coverArt.Width != 1)
                {
                    hasCoverArtPic = true;
                    var hdc = g.GetHdc();
                    xp = x + 3 + (pref.CoverArtSizeInCoverArtList - coverArt.Width) / 2;
                    yp = y + 3 + (pref.CoverArtSizeInCoverArtList - coverArt.Height) / 2;
                    w = coverArt.Width - 4;
                    h = coverArt.Height - 4;
                    GDI.BitBlt(hdc, xp, yp, pref.CoverArtSizeInCoverArtList + 2, pref.CoverArtSizeInCoverArtList + 2, coverArt.HDC, 0, 0, 0x00CC0020);
                    g.ReleaseHdc(hdc);
                }
                else
                {
                    g.DrawRectangle(Pens.DarkGray, x + 2, y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4);
                }
            }
            else
            {
                g.DrawRectangle(Pens.MidnightBlue, x + 2, y + 2, e.Bounds.Width - 4, e.Bounds.Height - 4);
                if (!string.IsNullOrEmpty(album))
                {
                    backgroundCoverartLoader.Enqueue(album, file_name, index);
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
                g.DrawString(album, pref.Font_playlistView, System.Drawing.Brushes.White, new RectangleF(xp + 2, yp + 2, w - 20, h));
            }
            double rate = 0;
            double.TryParse(albums[index][3].ToString(), out rate);
            var size = 15;
            g.FillEllipse(new SolidBrush(Color.FromArgb(128, Color.Red)), xp + w - (int)(size * 1.3), yp + 2, (int)(size*1.2), size);
            g.DrawEllipse(new Pen(Color.FromArgb(128, Color.White)), xp + w - (int)(size * 1.3), yp + 2, (int)(size * 1.2), size);
            var sf = new StringFormat();
            sf.Trimming = StringTrimming.None;
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            sf.FormatFlags = StringFormatFlags.NoWrap;
            g.DrawString(albums[index][2].ToString(), new Font("Arial Black", (int)(size * 0.8), GraphicsUnit.Pixel), System.Drawing.Brushes.White, new RectangleF(xp + w - (int)(size * 1.2), yp + 2, size + 1, size + 1), sf);
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
            if (albumArtListViewSearchTextBox.Width <= albumArtListViewSearchTextBox.Height)
            {
                var w = albumArtListViewSearchTextBox.Width;
                var xw = TextRenderer.MeasureText("ABCDEFGHIJ", albumArtListViewSearchTextBox.Font).Width; // 適当な文字数分の長さ
                albumArtListViewSearchTextBox.Width = xw;
                albumArtListViewSearchTextBox.Left -= (xw - w);
                albumArtListViewSearchTextBox.Select();
            }
        }

        private void albumArtListViewSearchTextBox_Leave(object sender, EventArgs e)
        {
            var w = albumArtListViewSearchTextBox.Width;
            albumArtListViewSearchTextBox.Width = albumArtListViewSearchTextBox.Height;
            albumArtListViewSearchTextBox.Left += (w - albumArtListViewSearchTextBox.Height);
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

        #region TaskTray Icon event
        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ActivateUI();
        }

        private void notifyIconContextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            notifyIconToolStripMenuItem_ShowBalloon.Checked = this.ShowNotifyBalloon;
        }

        private void notifyIconToolStripMenuItem_ShowBalloon_Click(object sender, EventArgs e)
        {
            this.ShowNotifyBalloon = !this.ShowNotifyBalloon;
        }
        #endregion
        #endregion

        #region pluginInterface methods

        private void parseSetting(Dictionary<string, object> _pref)
        {
            this.pref = new DefaultUIPreference(_pref);
            playlistView.CoverArtLineNum = pref.CoverArtSizeInLinesPlaylistView;
            playlistView.ColumnOrder = (Dictionary<string, int>)pref.PlaylistViewColumnOrder;
            playlistView.ColumnWidth = (Dictionary<string, int>)pref.PlaylistViewColumnWidth;
            config_FormLocation = pref.WindowLocation;
            config_FormSize = pref.WindowSize;
            this.WindowState = pref.WindowState;
            this.splitContainer1.SplitterDistance = pref.splitContainer1_SplitterDistance ?? 100;
            this.splitContainer2.SplitterDistance = pref.splitContainer2_SplitterDistance ?? 100;
            this.splitContainer3.SplitterDistance = pref.SplitContainer3_SplitterDistance;
        }

        private List<HotKey> hotkeys = new List<HotKey>();
        public void ResetHotKeys()
        {
            this.Invoke((MethodInvoker)(() =>
            {
                hotkeys.ForEach((e) => e.Dispose());
                hotkeys.Clear();
                hotkeys.Add(new HotKey(pref.Hotkey_PlayPause, (o, e) => { if (Controller.Current.Position > 0)Controller.TogglePause(); else Controller.Play(); }));
                hotkeys.Add(new HotKey(pref.Hotkey_Stop, (o, e) => Controller.Stop()));
                hotkeys.Add(new HotKey(pref.Hotkey_NextTrack, (o, e) => Controller.NextTrack()));
                hotkeys.Add(new HotKey(pref.Hotkey_PrevTrack, (o, e) => Controller.PrevTrack()));
                if (pref.UseMediaKey)
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
            pref.Font_playlistView = pref.Font_playlistView ?? this.playlistView.Font;
            pref.Font_trackInfoView = pref.Font_trackInfoView ?? this.trackInfoText.Font;
            pref.splitContainer1_SplitterDistance = pref.splitContainer1_SplitterDistance ?? this.splitContainer1.SplitterDistance;
            pref.splitContainer2_SplitterDistance = pref.splitContainer2_SplitterDistance ?? this.splitContainer2.SplitterDistance;
            // プレファレンスを適用
            if (_setting != null && _setting is Dictionary<string, object>)
            {
                parseSetting((Dictionary<string, object>)_setting);
            }

            // 表示するカラムが空の時、タグのカラムを表示することにする
            if (pref.DisplayColumns == null || pref.DisplayColumns.Length == 0)
            {
                pref.DisplayColumns = Columns.Where(_ =>
                    !string.IsNullOrEmpty(_.MappedTagField)
                    || _.Name == LibraryDBColumnTextMinimum.rating
                ).OrderBy(_ => _.Type).Select(_ => _.Name).ToArray();
            }

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
            coverArtView.Width = coverArtView.Height = splitContainer1.SplitterDistance;
            if (pref.splitContainer2_SplitterDistance == 0)
            {
                splitContainer2.SplitterDistance = 0;
            }
            else
            {
                splitContainer2.SplitterDistance = splitContainer1.SplitterDistance;
                splitContainer1_SplitterMoved(null, null);
            }

            // プレイリストビューの右クリックにColumn選択を生成
            var column_select = new ToolStripMenuItem("表示する項目");
            var temp = new ToolStripMenuItem("Coverart", null);
            temp.Checked = pref.ShowCoverArtInPlaylistView;
            temp.Click += (e, o) => { 
                temp.Checked = !temp.Checked;
                pref.ShowCoverArtInPlaylistView = temp.Checked;
                ResetPlaylistView();
            };
            column_select.DropDownItems.Add(temp);
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
                            if (_.Tag == null) continue;
                            displayColumns_list.Add(Columns[(int)_.Tag].Name);
                        }
                    }
                    pref.DisplayColumns = displayColumns_list.ToArray();
                    ResetPlaylistView();
                });
                item.CheckOnClick = true;
                item.Tag = i;
                if (pref.DisplayColumns.Contains(col.Name))
                {
                    item.Checked = true;
                }
                column_select.DropDownItems.Add(item);
            }
            playlistViewHeaderContextMenuStrip.Items.Add(column_select);

            coverArtView.Setup();
            var ver = Environment.OSVersion.Version;
            if ((ver.Major == 6 && ver.Minor >= 1) || ver.Major > 7) // win7 or above
            {
                try
                {
                    pseudoMainForm = new PseudoMainForm(this);
                    pseudoMainForm.Show();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
            ResetHotKeys();
            ResetTrackInfoView();
            if (pref.AutoImportPath != null && pref.AutoImportPath.Length > 0)
            {
                DoImport(pref.AutoImportPath.Select(_ => _), true);
            }
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 3000;
            timer.Tick += (z, zz) =>
            {
                timer.Stop();
                var updateInfo = Util.UpdateChecker.CheckNewVersion();
                if (updateInfo != null)
                {
                    notifyIcon1.BalloonTipClicked += (_, __) => { Shell32.OpenPath("http://lutea.gageas.com/"); };
                    notifyIcon1.ShowBalloonTip(1000, "Lutea Updated", "Lutea version " + String.Format("{0:0.00}",updateInfo.LuteaVersion) + " is available now.", ToolTipIcon.Info);
                }
                timer.Dispose();
            };
            timer.Start();
        }

        public object GetSetting()
        {
            this.pref.splitContainer1_SplitterDistance = splitContainer1.SplitterDistance;
            this.pref.splitContainer2_SplitterDistance = splitContainer2.SplitterDistance;
            this.pref.SplitContainer3_SplitterDistance = splitContainer3.SplitterDistance;
            this.pref.CoverArtSizeInLinesPlaylistView = playlistView.CoverArtLineNum;
            this.pref.PlaylistViewColumnOrder = new Dictionary<string, int>();
            this.pref.PlaylistViewColumnWidth = new Dictionary<string, int>();
            for (int i = 0; i < playlistView.Columns.Count; i++)
            {
                if (PlaylistView.IsCoverArtColumn(playlistView.Columns[i])) continue;
                this.pref.PlaylistViewColumnOrder[Columns[(int)playlistView.Columns[i].Tag].Name] = playlistView.Columns[i].DisplayIndex;
                this.pref.PlaylistViewColumnWidth[Columns[(int)playlistView.Columns[i].Tag].Name] = Math.Max(10, playlistView.Columns[i].Width);
            }
            this.pref.WindowState = this.WindowState;
            if (this.WindowState == FormWindowState.Minimized)
            {
                pref.WindowLocation = this.config_FormLocation;
            }
            else
            {
                pref.WindowLocation = this.Location;
            }
            if (this.WindowState == FormWindowState.Normal)
            {
                pref.WindowSize = this.ClientSize;
            }
            else
            {
                pref.WindowSize = this.config_FormSize;
            }
            return this.pref.ToDictionary();
        }

        public object GetPreferenceObject()
        {
            return this.pref.Clone<DefaultUIPreference>();
        }

        public void SetPreferenceObject(object _pref)
        {
            var prevpref = this.pref;
            var pref = (DefaultUIPreference)_pref;
            pref.Font_playlistView = pref.Font_playlistView  ?? this.playlistView.Font;
            pref.Font_trackInfoView = pref.Font_trackInfoView ?? this.trackInfoText.Font;
            this.pref = pref;
            ResetHotKeys();
            ResetPlaylistView();
            ResetTrackInfoView();
        }

        public void ActivateUI()
        {
            if (InvokeRequired)
            {
                this.Invoke((Action)(() => { this.ActivateUI(); }));
            }
            else
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    if (beforeMinimizeWindowState == FormWindowState.Maximized)
                    {
                        this.WindowState = FormWindowState.Maximized;
                    }
                    else
                    {
                        this.WindowState = FormWindowState.Normal;
                    }
                }
                this.Activate();
            }
        }

        public void Quit()
        {
            quitFromCore = true;
            if (importer != null)
            {
                try
                {
                    importer.Abort();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
            this.Invoke((MethodInvoker)(() =>
            {
                if (logview != null)
                {
                    logview.Close();
                }
                try
                {
                    visualizeView.Abort();
                }
                catch { }

                yomigana.Dispose();
            }));

            this.Close();
        }
        #endregion

        private void OnPlaylistSortOrderChange(string columnText, Controller.SortOrders sortOrder)
        {
            for (int i = 1; i < playlistView.Columns.Count; i++)
            {
                if ((int)playlistView.Columns[i].Tag == Controller.GetColumnIndexByName(columnText))
                {
                    playlistView.SetSortArrow(i, sortOrder == Controller.SortOrders.Asc ? SortOrder.Ascending : SortOrder.Descending);
                }
                else
                {
                    playlistView.SetSortArrow(i, SortOrder.None);
                }
            }
        }

        private object[][] Albums = null;
        private object[][] AlbumsFiltered = null;
        private void InitAlbumArtList()
        {
            albumArtListView.BeginUpdate();
            albumArtListView.Enabled = false;
            albumArtListView.SmallImageList = new ImageList();
            albumArtListView.SmallImageList.ImageSize = new System.Drawing.Size(pref.CoverArtSizeInCoverArtList + 7, pref.CoverArtSizeInCoverArtList + 7);
            albumArtListView.Columns[0].Width = pref.CoverArtSizeInCoverArtList + 7;
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

            ThreadPool.QueueUserWorkItem((_) =>
            {
                int index = 0;
                var albums = Albums;
                while (index < albums.Length)
                {
                    if (albums != Albums) return;
                    var e = albums[index];
                    var album = e[0].ToString();
                    var file_name = e[1].ToString();
                    if (backgroundCoverartLoader.QueueCount == 0)
                    {
                        if (backgroundCoverartLoader.IsCached(album))
                        {
                            index++;
                            continue;
                        }
                        backgroundCoverartLoader.Enqueue(album, file_name, index);
                        index++;
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            });
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