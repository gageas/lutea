using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Xml;
using System.Drawing;
using System.Drawing.Imaging;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.HTTPController
{
    public class HTTPController
    {
        /// <summary>
        /// カバーアートのサイズ
        /// </summary>
        private const int IMAGE_SIZE = 170;

        /// <summary>
        /// HttpListenerインスタンス
        /// </summary>
        private HttpListener listener;

        /// <summary>
        /// 待ちうけているポート番号
        /// </summary>
        internal readonly int port;

        /// <summary>
        /// HTTPリクエストのハンドラ
        /// </summary>
        /// <param name="req">HTTPリクエスト</param>
        /// <param name="res">HTTPレスポンス</param>
        private delegate void HTTPRequestHandler(HttpListenerContext ctx);

        /// <summary>
        /// 各modeに対するハンドラを保持するテーブル
        /// </summary>
        private Dictionary<string, HTTPRequestHandler> HTTPHandlers = new Dictionary<string, HTTPRequestHandler>();

        private List<HttpListenerContextHolder> holdingConnections_Info = new List<HttpListenerContextHolder>();
        private List<HttpListenerContextHolder> holdingConnections_Playlist = new List<HttpListenerContextHolder>();

        private struct HttpListenerContextHolder
        {
            public HttpListenerContext ctx;
            public readonly long tick;
            public HttpListenerContextHolder(HttpListenerContext ctx)
            {
                this.ctx = ctx;
                tick = System.DateTime.Now.Ticks;
            }
        }

        /// <summary>
        /// utf8で吐くため、stringではなくbyte[]を使います
        /// </summary>
        private byte[] CachedXML_Info = null;
        private byte[] CachedXML_Playlist = null;
        private byte[] CachedImagePNG_CoverArt = null;

        #region Constructor
        public HTTPController(int port)
        {
            this.port = port;
            // わざとfirewallのブロックポップアップを出す
            try
            {
                var tcpsock = new System.Net.Sockets.TcpListener(IPAddress.Any, port);
                tcpsock.Start();
                tcpsock.Stop();
            }
            catch (Exception e) { Logger.Error(e); }
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:" + port + "/");
            HTTPHandlers.Add("cover", ReturnCover);
            HTTPHandlers.Add("coverorigsize", ReturnCoverOriginalSize);
            HTTPHandlers.Add("xml", ReturnXML);
            HTTPHandlers.Add("control", KickAPI);
            HTTPHandlers.Add("blank", ReturnBlank);
            listener.Start();
            Controller.onTrackChange += (x) => {
                lock (holdingConnections_Info)
                {
                    CachedXML_Info = null;
                    CachedImagePNG_CoverArt = null;
                    holdingConnections_Info.ForEach((session) =>
                        System.Threading.ThreadPool.QueueUserWorkItem((_) =>
                        {
                            var ctx = session.ctx;
                            try { ReturnXML_Info(ctx.Request, ctx.Response); Logger.Log("return"); }
                            catch { }
                        })
                    );
                    holdingConnections_Info.Clear();
                }
            };
            Controller.PlaylistUpdated += (x) =>
            {
                lock (holdingConnections_Playlist)
                {
                    CachedXML_Playlist = null;
                    holdingConnections_Playlist.ForEach((session) => 
                        System.Threading.ThreadPool.QueueUserWorkItem((_) =>
                        {
                            var ctx = session.ctx;
                            try { ReturnXML_Playlist(ctx.Request, ctx.Response); }
                            catch { }
                        })
                    );
                    holdingConnections_Playlist.Clear();
                }
            };
        }
        #endregion

        #region Start stop server
        public void Start()
        {
            try
            {
                // Run Async
                listener.BeginGetContext(listenerCallback, listener);
            }
            catch { }
        }

        private void listenerCallback(IAsyncResult result)
        {
            try
            {
                HttpListener lsnr = (HttpListener)result.AsyncState;
                var context = lsnr.EndGetContext(result);
                lsnr.BeginGetContext(listenerCallback, lsnr);
                HandleHTTPContext(context);
            }
            catch { }
        }

        public void Abort()
        {
            listener.Abort();
        }
        #endregion

        #region Handle each request mode
        private void KickAPI(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            string op = req.QueryString["operation"];
            switch (op)
            {
                case "play":
                    Controller.Play();
                    break;
                case "next":
                    Controller.NextTrack();
                    break;
                case "prev":
                    Controller.PrevTrack();
                    break;
                case "stop":
                    Controller.Stop();
                    break;
                case "playpause":
                    Controller.TogglePause();
                    break;
                case "volup":
                    Controller.Volume += 0.1F;
                    break;
                case "voldown":
                    Controller.Volume -= 0.1F;
                    break;
                case "playitem":
                    int index = 0;
                    Lutea.Util.Util.tryParseInt(req.QueryString["index"], ref index);
                    Controller.PlayPlaylistItem(index);
                    break;
                case "createPlaylist":
                    Controller.createPlaylist(System.Web.HttpUtility.UrlDecode(req.QueryString["query"]));
                    break;
                case "quit":
                    Controller.Quit();
                    break;
            }
            res.Close();
        }

        private void ReturnBlank(HttpListenerContext ctx)
        {
            var res = ctx.Response;
            res.ContentEncoding = Encoding.UTF8;
            res.ContentType = "text/html; charset=UTF-8";

            using (var os = res.OutputStream)
            using (var sw = new System.IO.StreamWriter(os))
            {
                sw.Write("<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=8\" /><style type=\"text/css\">" + Properties.Resources.defalut_css + "</style></head><body></body></html>");
                sw.Flush();
            }
            res.Close();
        }

        private void ReturnDefault(HttpListenerContext ctx)
        {
            var res = ctx.Response;
            var req = ctx.Request;
            res.ContentEncoding = Encoding.UTF8;
            var useGzip = req.Headers["Accept-Encoding"].Split(',').Contains("gzip");
            if (useGzip) res.AppendHeader("Content-Encoding", "gzip");
            res.ContentType = "text/html; charset=UTF-8";

            using (var os = res.OutputStream)
            using (var gzips = new System.IO.Compression.GZipStream(os, System.IO.Compression.CompressionMode.Compress))
            using (var sw = new System.IO.StreamWriter(useGzip ? gzips : os))
            {
                sw.Write(Properties.Resources.template_html
                    .Replace("%%SCRIPT%%", Properties.Resources.main_js)
                    .Replace("%%STYLE%%", Properties.Resources.defalut_css)
                    .Replace("%%IMAGE_SIZE%%", IMAGE_SIZE.ToString()));
                sw.Flush();
            }
            res.Close();
        }

        /// <summary>
        /// PlaylistをXMLで返すAPIのcometセッションIDを返す
        /// </summary>
        /// <returns></returns>
        private int GetCometContext_XMLPlaylist()
        {
            return Controller.LatestPlaylistQuery.GetHashCode();
        }

        /// <summary>
        /// 再生中トラックの情報をXMLで返すAPIのcometセッションIDを返す
        /// </summary>
        /// <returns></returns>
        private int GetCometContext_XMLInfo()
        {
            return (Controller.Current.Filename + "").GetHashCode(); // null文字を空文字列にする
        }

        private void ReturnXML_Playlist(HttpListenerRequest req, HttpListenerResponse res)
        {
            var useGzip = req.Headers["Accept-Encoding"].Split(',').Contains("gzip");
            if (useGzip) res.AppendHeader("Content-Encoding", "gzip");

            byte[] xml = CachedXML_Playlist;
            if (xml == null)
            {
                var doc = new System.Xml.XmlDocument();
                var lutea = doc.CreateElement("lutea");
                doc.AppendChild(lutea);
                var comet_id = doc.CreateElement("comet_id");
                lutea.AppendChild(comet_id);

                var playlist = doc.CreateElement("playlist");
                for (int i = 0; i < Controller.CurrentPlaylistRows; i++)
                {
                    var item = doc.CreateElement("item");
                    item.SetAttribute("index", i.ToString());
                    item.SetAttribute("file_name", Controller.GetPlaylistRowColumn(i, Controller.GetColumnIndexByDBText(Library.LibraryDBColumnTextMinimum.file_name)));
                    item.SetAttribute("tagAlbum", Controller.GetPlaylistRowColumn(i, Controller.GetColumnIndexByDBText("tagAlbum")));
                    item.SetAttribute("tagArtist", Controller.GetPlaylistRowColumn(i, Controller.GetColumnIndexByDBText("tagArtist")));
                    item.SetAttribute("tagTitle", Controller.GetPlaylistRowColumn(i, Controller.GetColumnIndexByDBText("tagTitle")));
                    playlist.AppendChild(item);
                }

                lutea.AppendChild(playlist);

                // cometセッション識別子セット
                comet_id.InnerText = GetCometContext_XMLPlaylist().ToString();

                var ms = new System.IO.MemoryStream();
                var xw = new System.Xml.XmlTextWriter(ms, Encoding.UTF8);
                doc.Save(xw);
                xml = CachedXML_Playlist = ms.GetBuffer().Take((int)ms.Length).ToArray();
            }

            using (var os = res.OutputStream)
            using (var gzips = new System.IO.Compression.GZipStream(os, System.IO.Compression.CompressionMode.Compress))
            {
                try
                {
                    (useGzip ? gzips : os).Write(xml, 0, xml.Length);
                }
                catch { }
            }
            res.Close();
        }

        private void ReturnXML_Info(HttpListenerRequest req, HttpListenerResponse res)
        {
            var useGzip = req.Headers["Accept-Encoding"].Split(',').Contains("gzip");
            if (useGzip) res.AppendHeader("Content-Encoding", "gzip");

            byte[] xml = CachedXML_Info;
            if (xml == null)
            {
                var doc = new System.Xml.XmlDocument();
                var lutea = doc.CreateElement("lutea");
                doc.AppendChild(lutea);
                var comet_id = doc.CreateElement("comet_id");
                lutea.AppendChild(comet_id);

                // 現在のトラックに関する情報をセット
                var current = doc.CreateElement("current");
                foreach (var e in Controller.Columns)
                {
                    var ele = doc.CreateElement(e.ToString());
                    ele.InnerText = Controller.Current.MetaData(e);
                    current.AppendChild(ele);
                }
                lutea.AppendChild(current);

                // playlist生成クエリ文字列をセット
                var playlistquery = doc.CreateElement("playlistQuery");
                playlistquery.InnerText = Controller.LatestPlaylistQuery;
                lutea.AppendChild(playlistquery);

                // cometセッション識別子セット
                comet_id.InnerText = GetCometContext_XMLInfo().ToString();

                var ms = new System.IO.MemoryStream();
                var xw = new System.Xml.XmlTextWriter(ms, Encoding.UTF8);
                doc.Save(xw);
                xml = CachedXML_Info = ms.GetBuffer().Take((int)ms.Length).ToArray();
            }

            using (var os = res.OutputStream)
            using (var gzips = new System.IO.Compression.GZipStream(os, System.IO.Compression.CompressionMode.Compress))
            {
                try
                {
                    (useGzip ? gzips : os).Write(xml, 0, xml.Length);
                }
                catch { }
            }
            res.Close();
        }

        private void ReturnXML(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            res.ContentEncoding = Encoding.UTF8;
            res.ContentType = "text/xml; charset=UTF-8";

            string type = req.QueryString["type"];

            int comet_session = 0;

            switch (type)
            {
                case "playlist":
                    Util.Util.tryParseInt(req.QueryString["comet_id"], ref comet_session);
                    if (comet_session == GetCometContext_XMLPlaylist())
                    {
                        holdingConnections_Playlist.Add(new HttpListenerContextHolder(ctx));
                    }
                    else
                    {
                        ReturnXML_Playlist(ctx.Request, ctx.Response);
                    }
                    break;
                default:
                    Util.Util.tryParseInt(req.QueryString["comet_id"], ref comet_session);
                    if (comet_session == GetCometContext_XMLInfo())
                    {
                        holdingConnections_Info.Add(new HttpListenerContextHolder(ctx));
                    }
                    else
                    {
                        ReturnXML_Info(ctx.Request, ctx.Response);
                    }
                    break;
            }

            // 60sec以上前の接続を破棄
            var expiredConnections = holdingConnections_Playlist.FindAll(_ => (System.DateTime.Now.Ticks - _.tick) > 60 * 1000 * 1000 * 10)
                .Concat(holdingConnections_Info.FindAll(_ => (System.DateTime.Now.Ticks - _.tick) > 60 * 1000 * 1000 * 10));
            foreach (HttpListenerContextHolder e in expiredConnections)
            {
                try
                {
                    e.ctx.Response.Abort();
                }
                catch { }
            };
            holdingConnections_Playlist.RemoveAll(_ => expiredConnections.Contains(_));
            holdingConnections_Info.RemoveAll(_ => expiredConnections.Contains(_));
        }

        private void ReturnCover(HttpListenerContext ctx)
        {
            var res = ctx.Response;
            res.ContentType = "image/png";
            if (CachedImagePNG_CoverArt == null)
            {
                var image = Controller.Current.CoverArtImage();
                if (image == null)
                {
                    image = new Bitmap(1, 1);
                }
                var resized = Util.ImageUtil.GetResizedImageWithPadding(image, IMAGE_SIZE, IMAGE_SIZE);
                using (var ms = new System.IO.MemoryStream())
                {
                    resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    CachedImagePNG_CoverArt = ms.GetBuffer();
                }
            }
            res.OutputStream.Write(CachedImagePNG_CoverArt, 0, CachedImagePNG_CoverArt.Length);
            res.Close();
        }

        private void ReturnCoverOriginalSize(HttpListenerContext ctx)
        {
            var res = ctx.Response;
            res.ContentType = "image/jpeg";
            var image = Controller.Current.CoverArtImage();
            if (image == null)
            {
                image = new Bitmap(1, 1);
            }
            using (var ms = new System.IO.MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                var buffer = ms.GetBuffer();
                res.OutputStream.Write(buffer, 0, buffer.Length);
                res.Close();
            }
        }
        #endregion

        #region Handle http listener context
        private void HandleHTTPContext(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            res.AddHeader("Pragma", "no-cache");
            res.AddHeader("Cache-Control", "no-cache");

            string mode = "";
            if (req.QueryString.AllKeys.Contains("mode"))
            {
                mode = req.QueryString["mode"];
            }

            try
            {
                HTTPRequestHandler handler = ReturnDefault;
                if (HTTPHandlers.ContainsKey(mode)) handler = HTTPHandlers[mode];
                handler(ctx);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
//                res.Close();
            }
        }
        #endregion
    }
}
