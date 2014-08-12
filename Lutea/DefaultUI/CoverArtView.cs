using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Drawing;
using Gageas.Lutea.Core;
using Gageas.Lutea.Util;

namespace Gageas.Lutea.DefaultUI
{
    /// <summary>
    /// カバーアートを表示するコントロール
    /// </summary>
    class CoverArtView : UserControl, System.ComponentModel.ISupportInitialize
    {
        #region 定数
        private const int TRANSITION_STEPS = 32;
        private const int TRANSITION_INTERVAL = 20;
        private const int WAIT_BEFORE_TRANSITION = 100;
        internal const string ALTERNATIVE_FILE_NAME = "default.jpg";
        #endregion

        #region フィールド
        /// <summary>
        /// Transitionのスレッドを保持
        /// </summary>
        private Thread transitionThread;

        /// <summary>
        /// Transitionの進み具合
        /// </summary>
        private int transitionPhase = 0;

        /// <summary>
        /// 現在表示しているCovertArtのリサイズしていないImage
        /// </summary>
        private Image currentCoverArt;

        /// <summary>
        /// 現在表示しているCovertArtのリサイズ後のImage
        /// </summary>
        private Image currentCoverArtResized;

        /// <summary>
        /// TransitionのベースとなるImage
        /// </summary>
        private Image transitionFromImage = new Bitmap(1, 1);

        /// <summary>
        /// 表示領域のサイズ
        /// </summary>
        private Size coverArtSize = new Size(1, 1);
        #endregion

        #region publicメソッド
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public CoverArtView()
        {
            this.DoubleBuffered = true;
            this.Resize += CoverArtView_Resize;
            this.Paint += CoverArtView_Paint;
            Controller.onTrackChange += _ => { transitionThread.Interrupt(); };
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public void Setup()
        {
            // カバーアート関連。これはこの順番で
            // 走らせっぱなしにし、必要な時にinterruptする
            transitionThread = new Thread(CoverArtLoaderProc);
            CoverArtView_Resize(null, null); // PictureBoxのサイズを憶えるためにここで実行する
            transitionThread.IsBackground = true;
            transitionThread.Start();
        }

        #region ISupportInitializeの実装
        public void BeginInit()
        {
        }

        public void EndInit()
        {
        }
        #endregion
        #endregion

        #region Privateメソッド
        /// <summary>
        /// コンテンツを描画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoverArtView_Paint_Intl(object sender, PaintEventArgs e)
        {
            if (currentCoverArtResized == null) return;
            if (transitionFromImage == null) return;
            Bitmap img = (Bitmap)ImageUtil.GetAlphaComposedImage(transitionFromImage, currentCoverArtResized, (float)transitionPhase / TRANSITION_STEPS);
            using (var gdiimg = new GDI.GDIBitmap(img))
            {
                GDI.BitBlt(e.Graphics.GetHdc(), 0, 0, this.Width, this.Height, gdiimg.HDC, 0, 0, 0xCC0020);
                e.Graphics.ReleaseHdc();
            }
        }

        /// <summary>
        /// 画像の周りに枠を描画
        /// 画像のエッジ部の荒れを隠す。枠自体が主張しすぎないように。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoverArtView_Paint_Outline(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawRectangle(SystemPens.Control, 0, 0, Width - 1, Height - 1);
            e.Graphics.DrawLine(SystemPens.ControlDark, 2, Height - 1, Width - 1, Height - 1);
            e.Graphics.DrawLine(SystemPens.ControlDark, Width - 1, 2, Width - 1, Height - 1);
            e.Graphics.DrawRectangle(SystemPens.ControlDarkDark, 1, 1, Width - 3, Height - 3);
        }

        #region イベントハンドラ
        /// <summary>
        /// 描画
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoverArtView_Paint(object sender, PaintEventArgs e)
        {
            CoverArtView_Paint_Intl(sender, e);
            CoverArtView_Paint_Outline(sender, e);
        }

        /// <summary>
        /// コントロールがリサイズされた時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CoverArtView_Resize(object sender, EventArgs e)
        {
            if (!this.IsHandleCreated) return;
            if (!this.Created) return;

            // 新しいサイズを取得
            coverArtSize = new Size(Math.Max(1, this.Width + 2), Math.Max(1, this.Height + 2));

            // 新しいサイズでカバーアートを描画
            if (currentCoverArt != null)
            {
                Image newSize = ImageUtil.GetResizedImageWithPadding(currentCoverArt, coverArtSize.Width, coverArtSize.Height);
                Image crop = new Bitmap(newSize);
                using (var g = Graphics.FromImage(crop))
                {
                    g.DrawImage(newSize, -1,-1);
                }
                currentCoverArtResized = crop;
            }
        }
        #endregion

        /// <summary>
        /// 現在のトラックのカバーアートか、無ければ代替画像を取得
        /// </summary>
        /// <returns></returns>
        internal static Image GetCoverArtOrAlternativeImage()
        {
            Image image = Controller.Current.CoverArtImage();
            if (image == null)
            {
                try
                {
                    using (var fs = new System.IO.FileStream(ALTERNATIVE_FILE_NAME, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        image = System.Drawing.Image.FromStream(fs);
                    }
                }
                catch
                {
                    image = new Bitmap(1, 1);
                }
            }
            return image;
        }

        /// <summary>
        /// トランジションスレッド。
        /// 常に起動したままで、平常時はsleepしている。
        /// 必要になった時にInterruptする。
        /// </summary>
        private void CoverArtLoaderProc()
        {
            while (true)
            {
                try
                {
                    // Nextを連打したような場合に実際の処理が走らないように少しウェイト
                    Thread.Sleep(WAIT_BEFORE_TRANSITION);

                    // 現在の描画のコピーを作成
                    Bitmap bmp = new Bitmap(coverArtSize.Width, coverArtSize.Height);
                    this.Invoke((Action)(() =>
                    {
                        try
                        {
                            DrawToBitmap(bmp, new Rectangle(0, 0, coverArtSize.Width, coverArtSize.Height));
                        }
                        catch (Exception) { }
                    }));
                    transitionFromImage = bmp;

                    // 新しい画像を取得
                    currentCoverArt = GetCoverArtOrAlternativeImage();

                    // 新しい画像をリサイズ
                    var newSize = ImageUtil.GetResizedImageWithPadding(currentCoverArt, coverArtSize.Width, coverArtSize.Height);
                    var crop = new Bitmap(newSize);
                    using (var g = Graphics.FromImage(crop))
                    {
                        g.DrawImage(newSize, -1, -1);
                    }
                    currentCoverArtResized = crop;

                    // トランジションを開始
                    for (int i = 0; i <= TRANSITION_STEPS; i++)
                    {
                        transitionPhase = i;
                        this.Invalidate();
                        Thread.Sleep(TRANSITION_INTERVAL);
                    }
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (ThreadInterruptedException)
                {
                }
            }
        }
        #endregion
    }
}
