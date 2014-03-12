using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Gageas.Lutea.HTTPController
{
    class MyHttpListenerResponse
    {
        private Stream outputStream;
        private Stream tcpStream;
        private TcpClient tcpc;
        private NameValueCollection headers;
        public string ContentType
        {
            get;
            set;
        }
        public Encoding ContentEncoding
        {
            get;
            set;
        }
        public Stream OutputStream
        {
            get { return outputStream; }
        }

        private void SendHeader(Stream strm, string key, string value)
        {
            var tmp = Encoding.ASCII.GetBytes(key + ": " + value + "\r\n");
            tcpStream.Write(tmp, 0, tmp.Length);
        }

        public void Close()
        {
            var resHead = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n");
            tcpStream.Write(resHead, 0, resHead.Length);
            SendHeader(tcpStream, "Content-Type", ContentType);
            foreach (var t in headers.AllKeys)
            {
                SendHeader(tcpStream, t, headers[t]);
            }
            tcpStream.WriteByte((byte)'\r');
            tcpStream.WriteByte((byte)'\n');
            var buf = ((MemoryStream)outputStream).ToArray();
            outputStream.Dispose();
            tcpStream.Write(buf, 0, buf.Length);
            tcpStream.Flush();
            tcpStream.Close();
        }

        public void AppendHeader(string name, string value)
        {
            headers.Add(name, value);
        }
        internal MyHttpListenerResponse(Stream tcpStream, TcpClient tcpc)
        {
            this.tcpStream = tcpStream;
            this.tcpc = tcpc;
            ContentType = "text/plain";
            ContentEncoding = Encoding.ASCII;
            outputStream = new MemoryStream();
            headers = new NameValueCollection();
        }
        public void Abort()
        {
            outputStream.Close();
            outputStream.Dispose();
            tcpStream.Flush();
            tcpStream.Close();
        }
    }
}
