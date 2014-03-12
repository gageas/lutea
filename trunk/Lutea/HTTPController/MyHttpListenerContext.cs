using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.HTTPController
{
    class MyHttpListenerContext
    {
        private MyHttpListenerRequest request;
        public MyHttpListenerRequest Request
        {
            get { return request; }
        }
        private MyHttpListenerResponse response;
        public MyHttpListenerResponse Response
        {
            get { return response; }
        }
        internal MyHttpListenerContext(MyHttpListenerRequest req, MyHttpListenerResponse res)
        {
            request = req;
            response = res;
        }

    }
}
