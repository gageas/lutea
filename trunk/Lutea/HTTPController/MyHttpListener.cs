using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace Gageas.Lutea.HTTPController
{
    class MyHttpListener
    {
        public delegate void HttpRequestHandler(MyHttpListenerContext ctx);

        private readonly int port;
        private TcpListener tcpl;
        private HttpRequestHandler handler;

        public MyHttpListener(int port)
        {
            this.port = port;
        }

        public void Start(HttpRequestHandler handler)
        {
            this.handler = handler;
            tcpl = new System.Net.Sockets.TcpListener(IPAddress.Any, port);
            tcpl.Start();
            tcpl.BeginAcceptTcpClient(listenerCallback, tcpl);
        }

        private void listenerCallback(IAsyncResult result)
        {
            try
            {
                TcpListener lsnr = (TcpListener)result.AsyncState;
                var context = lsnr.EndAcceptTcpClient(result);
                lsnr.BeginAcceptTcpClient(listenerCallback, lsnr);
                var httpContext = parseHTTPRequest(context);
                if (httpContext != null)
                {
                    handler(httpContext);
                }
            }
            catch { }
        }

        private MyHttpListenerContext parseHTTPRequest(TcpClient tcpc)
        {
            var strm = tcpc.GetStream();
            var requestLine = readOneLine(strm);
            if (requestLine == null) return null;
            var requestTokens = requestLine.Split(' ');
            Logger.Log(requestTokens[1]);
            MyHttpListenerRequest req = new MyHttpListenerRequest();
            MyHttpListenerResponse res = new MyHttpListenerResponse(strm, tcpc);
            if (requestTokens[1].Contains('?'))
            {
                var queryStringAll = requestTokens[1].Substring(requestTokens[1].IndexOf('?') + 1).Split('&');
                foreach (var q in queryStringAll)
                {
                    var kv = q.Split('=');
                    if (kv.Length != 2) continue;
                    req.QueryString.Add(kv[0], kv[1]);
                }
            }
            string line;
            while ((line = readOneLine(strm)) != "")
            {
                var kv = line.Split(new char[] { ':' }, 2);
                if (kv.Length != 2) continue;
                req.Headers.Add(kv[0].Trim(), kv[1].Trim());
            }
            return new MyHttpListenerContext(req, res);
        }

        private string readOneLine(Stream strm)
        {
            char[] buffer = new char[1024];
            int pos = 0;
            bool r = false;
            while (pos < buffer.Length)
            {
                int read = strm.ReadByte();
                if (read == -1) return null;
                if ((read == '\n') && r)
                {
                    return new string(buffer, 0, pos - 1);
                }
                r = (read == '\r');
                buffer[pos] = (char)read;
                pos++;
            }
            return null;
        }

        public void Abort()
        {
            tcpl.Stop();
        }
    }
}
