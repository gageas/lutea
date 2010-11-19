﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

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
            using (var db = Controller.DBConnection)
            {
                // 関連アルバムを引っ張ってくる
                using (var stmt = db.Prepare("SELECT tagAlbum,COUNT(*) FROM list WHERE tagAlbum IN (SELECT tagAlbum FROM list WHERE " + q + " ) GROUP BY tagAlbum ORDER BY COUNT(*) DESC;"))
                {
                    related_albums = stmt.EvaluateAll();
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
                    xTrackBar1.Value = second;
                    toolStripStatusLabel1.Text = (Util.Util.getMinSec(second) + "/" + Util.Util.getMinSec(Controller.Current.Length));
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
            if (logview != null)
            {
                logview.Close();
            }
            if (CoverArtTransitionTickTimer != null)
            {
                CoverArtTransitionTickTimer.Dispose();
                CoverArtTransitionTickTimer = null;
            }
            yomigana.Dispose();
            coverArtImageLoaderThread.Abort();
            Controller.Quit();
        }

        private void DefaultUIForm_Activated(object sender, EventArgs e)
        {
            listView1.Select();
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
                if (sql == textBox1.Text)
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
        private void reloadDynamicPlaylist()
        {
            char sep = System.IO.Path.DirectorySeparatorChar;
            treeView1.Nodes.Clear();
            TreeNode folder = new TreeNode("クエリ");
            string querydir = Controller.UserDirectory + sep + "query";
            DynamicPlaylist.Load(querydir, folder, null);
            treeView1.Nodes.Add(folder);
            treeView1.ExpandAll();
            previouslyClicked = null;
        }

        private void ExecQueryViewQuery(TreeNode node)
        {
            if (node == null) return;
            if (node.Tag == null) return;
            if (node.Tag is PlaylistEntry)
            {
                PlaylistEntry ent = (PlaylistEntry)node.Tag;
                textBox1.Text = null;
                textBox1.Text = ent.sql;
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
        /// 画像を切り替える際のフェード効果のためのタイマ
        /// </summary>
        System.Timers.Timer CoverArtTransitionTickTimer;

        /// <summary>
        /// 現在のカバーアートをImageオブジェクトとして返す。
        /// カバーアートが無ければdefault.jpgのImageオブジェクトを返す。
        /// default.jpgも見つからなければnullを返す。
        /// FIXME?: この機能はCoreに移すかも
        /// </summary>
        /// <returns></returns>
        private Image GetCoverArtImage()
        {
            string streamFilename = Controller.Current.StreamFilename;
            Image image = null;
            if (streamFilename != null)
            {
                List<KeyValuePair<string, object>> tag = MetaTag.readTagByFilename(streamFilename, true);
                if (tag != null)
                {
                    //                    tag.ForEach((x) => { Logger.Debug(x.Key + " , " + x.Value.ToString().Replace("\0", "\r\n")); });
                    image = (Image)tag.Find((match) => match.Value is System.Drawing.Image).Value;
                    pictureBox1.Tag = null;
                }
                if (image == null)
                {
                    String name = System.IO.Path.GetDirectoryName(streamFilename);
                    String[] searchPatterns = { "folder.jpg", "folder.jpeg", "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp" };
                    foreach (String searchPattern in searchPatterns)
                    {
                        String[] filename_candidate = System.IO.Directory.GetFiles(name, searchPattern);
                        if (filename_candidate.Length > 0)
                        {
                            Logger.Log("CoverArt image is " + filename_candidate[0]);
                            image = Image.FromFile(filename_candidate[0]);
                            if (image == null) continue;
                            pictureBox1.Tag = filename_candidate[0];
                            break;
                        }
                    }
                }
            }
            if (image == null)
            {
                try
                {
                    image = Image.FromFile("default.jpg");
                }
                catch { }
            }
            return image;
        }

        /// <summary>
        /// CoverArt画像をバックグラウンドで読み込むスレッドとして動作。
        /// 常に起動したままで、平常時はsleepしている。
        /// 必要になった時にInterruptする。
        /// </summary>

        int CoverArtWidth = 10;
        int CoverArtHeight = 10;
        //

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
                    Image coverArtImage = GetCoverArtImage();
                    if (coverArtImage == null) coverArtImage = new Bitmap(1, 1);
                    Image resized = null;

                    Image transitionBeforeImage = null;

                    try // Mutex ここから
                    {
                        // 新しい画像をリサイズ
                        coverArtImage.Tag = GetResizedImageWithPadding(coverArtImage, CoverArtWidth, CoverArtHeight);

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

        /// <summary>
        /// 画像を指定したwidth*heightに収まるようにアスペクト比を保ったまま縮小する。
        /// Imageのサイズがwidth*heightになるように画像の周囲には余白をつける。
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        private Image GetResizedImageWithPadding(Image image, int width, int height)
        {
            return GetResizedImageWithPadding(image, width, height, Color.White);
        }
        private Image GetResizedImageWithPadding(Image image, int width, int height, Color backgroundColor)
        {
            double xZoomMax = (double)width / image.Width;
            double yZoomMax = (double)height / image.Height;

            double zoom = Math.Min(xZoomMax, yZoomMax);

            int resizedWidth = 0;
            int resizedHeight = 0;

            int padX = 0;
            int padY = 0;

            if (xZoomMax > yZoomMax)
            {
                resizedWidth = (int)(yZoomMax * image.Width);
                resizedHeight = height;
                padY = 0;
                padX = (width - resizedWidth) / 2;
            }
            else
            {
                resizedWidth = width;
                resizedHeight = (int)(xZoomMax * image.Height);
                padX = 0;
                padY = (height - resizedHeight) / 2;
            }

            Image dest = new Bitmap(width, height);
            using (var g = Graphics.FromImage(dest))
            {
                g.Clear(backgroundColor);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, padX, padY, resizedWidth, resizedHeight);
            }
            return dest;
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
                Controller.createPlaylist(textBox1.Text);
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
                    //                    System.Diagnostics.Process.Start((string)pictureBox1.Tag);
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
                    Image newSize = GetResizedImageWithPadding(CurrentCoverArt, CoverArtWidth, CoverArtHeight);
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
            Controller.PlayPlaylistItem(listView1.SelectedIndices[0]);
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
            ToolStripMenuItem toolstrip_index = new ToolStripMenuItem("索引");
            ToolStripMenuItem toolstrip_index_other = new ToolStripMenuItem("その他");
            ToolStripMenuItem toolstrip_index_num = new ToolStripMenuItem("数字");
            ToolStripMenuItem toolstrip_index_alpha = new ToolStripMenuItem("A-Z");
            ToolStripMenuItem toolstrip_index_kana = new ToolStripMenuItem("あ-ん");
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

            toolstrip_index_kana.DropDownItems.AddRange(kanas);

            var charTypes = new ToolStripMenuItem[]{
                    toolstrip_index_num,
                    toolstrip_index_alpha,
                    toolstrip_index_kana,
                    toolstrip_index_other,
                };
            toolstrip_index.DropDownItems.AddRange(charTypes);

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
                target.OwnerItem.Enabled = true;
                target.DropDownItems.Add(grp.Header, null, (e, obj) =>
                {
                    list.ContextMenuStrip.Hide();
                    list.EnsureVisible(last);
                    list.EnsureVisible(index);

                });
            }
            list.ContextMenuStrip.Items.Add(toolstrip_index);
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
                using (var db = Controller.DBConnection)
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
                ()=>{
                    settingCoverArtSize = (int)setting["CoverArtPaneSize"];
                },
                ()=>LibraryLatestDir = (string)setting["LibraryLatestDir"],
                ()=>SpectrumMode = (int)setting["SpectrumMode"],
                ()=>FFTLogarithmic = (bool)setting["FFTLogarithmic"],
                ()=>FFTNum = (Preference.FFTNum)setting["FFTNum"],
                ()=>SpectrumColor1 = (Color)setting["SpectrumColor1"],
                ()=>SpectrumColor2 = (Color)setting["SpectrumColor2"],
                ()=>displayColumns = (DBCol[])setting["DisplayColumns"],
                ()=>listView1.Font = (System.Drawing.Font)setting["Font_PlaylistView"],
            }, null);
        }

        private Bitmap[] StarImages = new Bitmap[6];
        public void Init(object _setting)
        {
            //            this.Show();
            if (_setting != null)
            {
                parseSetting((Dictionary<string, object>)_setting);
            }

            // レーティング用の画像を準備
            Image StarImage_on, StarImage_off;

            try
            {
                StarImage_on = Image.FromFile(@"components\rating_on.gif");
            }
            catch
            {
                StarImage_on = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(StarImage_on))
                {
                    g.FillEllipse(SystemBrushes.ControlText, 2, 2, 12, 12);
                }
            }

            try
            {
                StarImage_off = Image.FromFile(@"components\rating_off.gif");
            }
            catch
            {
                StarImage_off = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(StarImage_off))
                {
                    g.FillRectangle(SystemBrushes.GrayText, 6, 6, 4, 4);
                }
            }

            for (int i = 0; i <= 5; i++)
            {
                StarImages[i] = new Bitmap(StarImage_on.Width * 5, StarImage_on.Height);
                using (var g = Graphics.FromImage(StarImages[i]))
                {
                    for (int j = 0; j < 5; j++)
                    {
                        g.DrawImage(i > j ? StarImage_on : StarImage_off, j * StarImage_on.Width, 0);
                    }
                }
            }

            setupPlaylistView();
            this.Show();
            if (this.WindowState == FormWindowState.Normal)
            {
                if (!config_FormLocation.IsEmpty) this.Location = config_FormLocation;
                if (!config_FormSize.IsEmpty) this.Size = config_FormSize;
            }
            pictureBox1.Width = pictureBox1.Height = splitContainer4.SplitterDistance = splitContainer3.SplitterDistance = settingCoverArtSize;
            splitContainer3_SplitterMoved(null, null);
            pictureBox1.Image = new Bitmap(pictureBox1.Width, pictureBox1.Height);

            // プレイリストビューの右クリックにColumn選択を生成
            var column_select = new ToolStripMenuItem("Display");
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
        }

        public object GetSetting()
        {
            var setting = new Dictionary<string, object>();
            setting["CoverArtPaneSize"] = splitContainer3.SplitterDistance;
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
                setting["WindowSize"] = this.Size;
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
            setupPlaylistView();
        }

        public void LibraryInitializeRequired()
        {
            throw new NotImplementedException();
        }
        #endregion

        private class DoubleBufferedListView : ListView
        {
            public DoubleBufferedListView()
            {
                DoubleBuffered = true;
            }
        }

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

        /*
        private TextFormatFlags flags = TextFormatFlags.WordEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        private static StringFormat sf = new StringFormat(StringFormatFlags.NoWrap);
        private void listView1_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            return;
            using (var g = e.Graphics)
            {
                var b = e.Bounds;
                if (b.Height < 10) return;
                var str = row[(int)(displayColumns[e.ColumnIndex])].ToString();
                if (displayColumns[e.ColumnIndex] == DBCol.rating)
                {
                    int stars = 0;
                    int.TryParse(str,out stars);
                    stars /= 10;
                    for(int i=0;i<stars;i++){
                        g.FillEllipse(SystemBrushes.ControlDarkDark,b.X + 10*i + 3,b.Y + 3,10,10);
                    }

//                    g.FillRectangle(Brushes.Blue, e.Bounds.X, e.Bounds.Y, e.Item.Text.Length * 10, 10);
//                    g.FillRectangle(Brushes.Blue, e.Bounds.X, e.Bounds.Y,  , 10);
                }
                else
                {
                    switch (displayColumns[e.ColumnIndex])
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
                    }
                    IntPtr hDC = g.GetHdc();
                    IntPtr hFont = this.Font.ToHfont();
                    IntPtr hOldFont = SelectObject(hDC, hFont);
                    TextOut(hDC, b.X, b.Y, str, str.Length);

                    DeleteObject(SelectObject(hDC, hOldFont));
                    g.ReleaseHdc(hDC);

                    
                    if (emphasizedRowId == e.ItemIndex)
                    {
                        TextRenderer.DrawText(g, str, new Font(listView1.Font,FontStyle.Bold), e.Bounds, SystemColors.ControlText,flags);
                    }
                    else
                    {
                        TextRenderer.DrawText(g, str, listView1.Font, e.Bounds, SystemColors.ControlText,flags);
                    }
                }
                g.DrawLine(Pens.White, b.X, b.Y, b.X, b.Y+99);
                e.DrawDefault = false;
            }
        }*/

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "MoveToEx")]
        private static extern bool MoveToEx(IntPtr hDC, int x, int y, IntPtr lpPoint);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "LineTo")]
        private static extern bool LineTo(IntPtr hDC, int xEnd, int yEnd);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetTextExtentPoint32")]
        private static extern bool GetTextExtentPoint32(IntPtr hDC, String str, int length, out Size sz);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetStockObject")]
        private static extern IntPtr GetStockObject(int id);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SelectObject")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGDIOBJ);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "DeleteObject")]
        private static extern bool DeleteObject(IntPtr hGDIOBJ);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetTextColor")]
        private static extern uint SetTextColor(IntPtr hDC, uint COLORREF);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "TextOutW")]
        private static extern bool TextOut(IntPtr hDC, int nXStart, int nYStart, string str, int length);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "BitBlt")]
        private static extern bool BitBlt(
    IntPtr hdcDest,    // コピー先デバイスコンテキスト
    int nXDest,     // コピー先x座標
    int nYDest,     // コピー先y座標
    int nWidth,     // コピーする幅
    int nHeight,    // コピーする高さ
    IntPtr hdcSource,  // コピー元デバイスコンテキスト
    int nXSource,   // コピー元x座標
    int nYSource,   // コピー元y座標
    uint dwRaster    // ラスタオペレーションコード
);

        private const int WHITE_PEN = 6;
        private object[] row;
        //        IntPtr hBmpSrc = IntPtr.Zero;
        //        private GDIBitmap StarImage_on = null;
        //        private GDIBitmap StarImage_off = null;
        private class GDIBitmap : IDisposable
        {
            public Image orig;
            private Bitmap bitmap;
            private Graphics g;
            private IntPtr hDC;
            public IntPtr HDC
            {
                get
                {
                    return hDC;
                }
            }
            private IntPtr hBMP;
            public GDIBitmap(Bitmap bitmap)
            {
                this.orig = bitmap;
                this.bitmap = new Bitmap(bitmap);
                using (var g = Graphics.FromImage(this.bitmap))
                {
                    g.DrawImage(bitmap, 0, 0);
                }
                //                this.bitmap = bitmap;
                this.g = Graphics.FromImage(this.bitmap);
                this.hDC = this.g.GetHdc();
                this.hBMP = this.bitmap.GetHbitmap();
                SelectObject(this.hDC, this.hBMP);
            }
            public void Dispose()
            {
                DeleteObject(this.hBMP);
                g.ReleaseHdc();
                g.Dispose();
                bitmap.Dispose();
            }
        }
        private void listView1_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            row = Controller.GetPlaylistRow(e.ItemIndex);
            if (row == null || row.Length == 0) return;
            var bounds = e.Bounds;
            var isSelected = (e.State & ListViewItemStates.Selected) != 0;
            using (var g = e.Graphics)
            {
                Brush brush = isSelected ? SystemBrushes.Highlight : e.ItemIndex % 2 == 0 ? SystemBrushes.Window : SystemBrushes.ControlLight;
                g.FillRectangle(brush, bounds);

                var cols = new List<ColumnHeader>();
                foreach (ColumnHeader head in listView1.Columns)
                {
                    cols.Add(head);
                }
                cols.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));
                int pc = 0;

                // 本当はbitbltするといいんだろうけど面倒だしたかが知れてるのでdrawImageする
                foreach (ColumnHeader head in cols)
                {
                    DBCol col = (DBCol)head.Tag;
                    if (col == DBCol.rating)
                    {
                        int stars = 0;
                        int.TryParse(row[(int)col].ToString(), out stars);
                        stars /= 10;
                        int _y = (bounds.Height - StarImages[0].Height) / 2 + bounds.Y;
                        int _x = bounds.X + pc + 2;
                        g.DrawImage(StarImages[stars], _x, _y, new Rectangle(0, 0, head.Width - 4, StarImages[stars].Height), GraphicsUnit.Pixel);
                    }
                    pc += head.Width;
                }

                pc = 0;
                IntPtr hDC = g.GetHdc();
                IntPtr hFont = (emphasizedRowId == e.ItemIndex ? new Font(listView1.Font, FontStyle.Bold) : listView1.Font).ToHfont();
                IntPtr hOldFont = SelectObject(hDC, hFont);

                Size size_dots;
                GetTextExtentPoint32(hDC, "...", "...".Length, out size_dots);
                int y = bounds.Y + (bounds.Height - size_dots.Height) / 2;

                SelectObject(hDC, GetStockObject(WHITE_PEN));
                SetTextColor(hDC, (uint)(isSelected ? SystemColors.HighlightText.ToArgb() : SystemColors.ControlText.ToArgb())&0xffffff);
                foreach (ColumnHeader head in cols)
                {
                    MoveToEx(hDC, bounds.X + pc - 1, bounds.Y, IntPtr.Zero);
                    LineTo(hDC, bounds.X + pc - 1, bounds.Y + bounds.Height);
                    DBCol col = (DBCol)head.Tag;

                    if (col == DBCol.rating)
                    {
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
                    }
                    Size size;
                    GetTextExtentPoint32(hDC, str, str.Length, out size);
                    if (size.Width < w)
                    {
                        var padding = col == DBCol.tagTracknumber || col == DBCol.playcount || col == DBCol.lastplayed || col == DBCol.statBitrate || col == DBCol.statDuration ? (w - size.Width) - 1 : 1;
                        TextOut(hDC, bounds.X + pc + padding, y, str, str.Length);
                    }
                    else
                    {
                        if (w > size_dots.Width)
                        {
                            int cnt = str.Length + 1;
                            do
                            {
                                GetTextExtentPoint32(hDC, str, --cnt, out size);
                                if (size.Width > w * 2) cnt = (int)(cnt * 0.75);
                            } while (size.Width + size_dots.Width > w && cnt > 0);
                            TextOut(hDC, bounds.X + pc + 1, y, str.Substring(0, cnt) + "...", cnt + 3);
                        }
                    }
                    pc += head.Width;
                }

                DeleteObject(SelectObject(hDC, hOldFont));
                g.ReleaseHdc(hDC);

                if (emphasizedRowId == e.ItemIndex)
                {
                    g.DrawRectangle(Pens.Navy, bounds.X - 1, bounds.Y, bounds.Width, bounds.Height - 1);
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
    }

    /*
    public class Track : TrackBar
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, out _RECT lParam);

        private const int TBM_GETCHANNELRECT = 1050;
        public _RECT Rect
        {
            get
            {
                _RECT rect;
                SendMessage(this.Handle, TBM_GETCHANNELRECT, IntPtr.Zero, out rect);
                return rect;
            }
        }

        public Track()
        {
            this.Maximum = 200;
            this.Minimum = 0;
        }

        public struct _RECT
        {
            public UInt32 left;
            public UInt32 top;
            public UInt32 right;
            public UInt32 bottom;
        }
    }
    */
}