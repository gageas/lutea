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
        private delegate void HTTPRequestHandler(HttpListenerRequest req, HttpListenerResponse res);

        /// <summary>
        /// 各modeに対するハンドラを保持するテーブル
        /// </summary>
        private Dictionary<string, HTTPRequestHandler> HTTPHandlers = new Dictionary<string, HTTPRequestHandler>();

        private List<System.Threading.Thread> holdingConnections_Info = new List<System.Threading.Thread>();
        private List<System.Threading.Thread> holdingConnections_Playlist = new List<System.Threading.Thread>();

        #region Constructor
        public HTTPController(int port)
        {
            this.port = port;
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:" + port + "/");
            HTTPHandlers.Add("cover", ReturnCover);
            HTTPHandlers.Add("xml", ReturnXML);
            HTTPHandlers.Add("control", KickAPI);
            HTTPHandlers.Add("blank", ReturnBlank);
            listener.Start();
            Controller.onTrackChange += (x) => {
                lock (holdingConnections_Info)
                {
                    holdingConnections_Info.ForEach((e) => e.Interrupt());
                    holdingConnections_Info.Clear();
                }
            };
            Controller.PlaylistUpdated += (x) =>
            {
                lock (holdingConnections_Playlist)
                {
                    holdingConnections_Playlist.ForEach((e) => e.Interrupt());
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
            HttpListener lsnr = (HttpListener)result.AsyncState;
            var context = lsnr.EndGetContext(result);
            lsnr.BeginGetContext(listenerCallback, lsnr);
            HandleHTTPContext(context);
        }

        public void Abort()
        {
            listener.Abort();
        }
        #endregion

        #region Handle each request mode
        private void KickAPI(HttpListenerRequest req, HttpListenerResponse res)
        {
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
        }

        private void ReturnBlank(HttpListenerRequest req, HttpListenerResponse res)
        {
            res.ContentEncoding = Encoding.UTF8;
            res.ContentType = "text/html; charset=UTF-8";

            using (var os = res.OutputStream)
            using (var sw = new System.IO.StreamWriter(os))
            {
                sw.Write("<html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=8\" /><style type=\"text/css\">" + Properties.Resources.defalut_css + "</style></head><body></body></html>");
                sw.Flush();
            }
        }

        private void ReturnDefault(HttpListenerRequest req, HttpListenerResponse res)
        {
            res.ContentEncoding = Encoding.UTF8;
            res.ContentType = "text/html; charset=UTF-8";

            using (var os = res.OutputStream)
            using (var sw = new System.IO.StreamWriter(os))
            {
                sw.Write(Properties.Resources.template_html
                    .Replace("%%SCRIPT%%", Properties.Resources.main_js)
                    .Replace("%%STYLE%%", Properties.Resources.defalut_css)
                    .Replace("%%IMAGE_SIZE%%", IMAGE_SIZE.ToString()));
                sw.Flush();
            }
        }

        private void ReturnXML(HttpListenerRequest req, HttpListenerResponse res)
        {
            string type = req.QueryString["type"];
            res.ContentEncoding = Encoding.UTF8;
            res.ContentType = "text/xml; charset=UTF-8";

            var doc = new System.Xml.XmlDocument();
            switch (type)
            {
                case "playlist":
                    if (req.QueryString["comet"] == "true")
                    {
                        lock (holdingConnections_Playlist)
                        {
                            holdingConnections_Playlist.Add(System.Threading.Thread.CurrentThread);
                        }
                        try
                        {
                            System.Threading.Thread.Sleep(20 * 1000);
                        }
                        catch { }
                        finally
                        {
                            lock (holdingConnections_Playlist)
                            {
                                holdingConnections_Playlist.Remove(System.Threading.Thread.CurrentThread);
                            }
                        }
                    }

                    var playlist = doc.CreateElement("playlist");
                    for (int i = 0; i < Controller.CurrentPlaylistRows; i++)
                    {
                        var item = doc.CreateElement("item");
                        item.SetAttribute("index", i.ToString());
                        item.SetAttribute("file_name", Controller.GetPlaylistRowColumn(i, DBCol.file_name));
                        item.SetAttribute("tagAlbum", Controller.GetPlaylistRowColumn(i, DBCol.tagAlbum));
                        item.SetAttribute("tagArtist", Controller.GetPlaylistRowColumn(i, DBCol.tagArtist));
                        item.SetAttribute("tagTitle", Controller.GetPlaylistRowColumn(i, DBCol.tagTitle));
                        playlist.AppendChild(item);
                    }
                    doc.AppendChild(playlist);
                    break;
                default:
                    if (req.QueryString["comet"] == "true")
                    {
                        lock (holdingConnections_Info)
                        {
                            holdingConnections_Info.Add(System.Threading.Thread.CurrentThread);
                        }
                        try
                        {
                            System.Threading.Thread.Sleep(20 * 1000);
                        }
                        catch { }
                        finally
                        {
                            lock (holdingConnections_Info)
                            {
                                holdingConnections_Info.Remove(System.Threading.Thread.CurrentThread);
                            }
                        }
                    }

                    var lutea = doc.CreateElement("lutea");

                    // 現在のトラックに関する情報をセット
                    var current = doc.CreateElement("current");
                    foreach (DBCol e in Enum.GetValues(typeof(DBCol)))
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

                    doc.AppendChild(lutea);
                    break;
            }
            using (var os = res.OutputStream)
            using (var sw = new System.IO.StreamWriter(os))
            {
                try
                {
                    doc.Save(sw);
                    sw.Flush();
                }
                catch { }
            }
        }

        private void ReturnCover(HttpListenerRequest req, HttpListenerResponse res)
        {
            res.ContentType = "image/png";
            var image = Controller.Current.CoverArtImage();
            if (image == null)
            {
                image = new Bitmap(10, 10);
            }
            var resized = Util.Util.GetResizedImageWithPadding(image, IMAGE_SIZE, IMAGE_SIZE);
            var ms = new System.IO.MemoryStream();
            resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var buf = ms.GetBuffer();
            res.OutputStream.Write(buf, 0, buf.Length);
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
                handler(req, res);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            finally
            {
                res.Close();
            }
        }
        #endregion
    }
}
