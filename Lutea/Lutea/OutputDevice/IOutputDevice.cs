using System;
using Gageas.Lutea.Core;

namespace Gageas.Lutea.OutputDevice
{
    interface IOutputDevice : IDisposable
    {
        int Freq { get; }
        int Chans { get; }
        bool CanAbort { get; }
        bool Pause { get; set; }
        float Volume { get; set; }
        ulong BufferedSamples { get; }
        Controller.OutputModeEnum OutputMode { get; }
        Controller.Resolutions OutputResolution { get; }
        void Start();
        void Stop();
        void Resume();
        uint GetDataFFT(float[] buffer, Controller.FFTNum fftopt);
    }
}
