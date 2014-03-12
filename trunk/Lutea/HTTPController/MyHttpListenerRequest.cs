using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.HTTPController
{
    class MyHttpListenerRequest
    {
        public NameValueCollection QueryString = new NameValueCollection();
        public NameValueCollection Headers = new NameValueCollection();
        internal MyHttpListenerRequest()
        {
        }
    }
}
