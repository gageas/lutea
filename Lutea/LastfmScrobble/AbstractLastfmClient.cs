using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.LastfmScrobble
{
    abstract class AbstractLastfmClient : IDisposable
    {
        public abstract void Dispose();
        public abstract void OnTrackChange(int i);
        public abstract void OnExceedThresh();
        public abstract void OnPause();
        public abstract void OnResume();
    }
}
