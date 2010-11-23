using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Gageas.Lutea.DefaultUI
{
    class Com
    {
        [DllImport("ole32.dll")]
        public static extern int CLSIDFromString([MarshalAs(UnmanagedType.LPWStr)] string lpsz, out Guid pclsid);

        [DllImport("ole32.dll")]
        public static extern int CoCreateInstance([MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            IntPtr pUnkOuter, uint dwClsContext, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr rpv);

        [DllImport("ole32.dll")]
        public static extern int CoInitialize(IntPtr pvReserved);

        [DllImport("ole32.dll")]
        public static extern int CoUninitialize();

        public const int CLSCTX_ALL = 0x17;
    }
}
