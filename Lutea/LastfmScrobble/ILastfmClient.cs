using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gageas.Lutea.LastfmScrobble
{
    interface ILastfmClient : IDisposable
    {
        void OnTrackChange(int i);
        void OnExceedThresh();
        void OnPause();
        void OnResume();
    }
}
