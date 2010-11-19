using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Gageas.Lutea.DefaultUI
{
    class Shell32
    {
        [DllImport("Shell32.dll")]
        public extern static int ExtractIconEx(string libName, int iconIndex,
        IntPtr[] largeIcon, IntPtr[] smallIcon, int nIcons);

        public static System.Drawing.Icon GetShellIcon(int id, bool isLarge){
            IntPtr[] large = new IntPtr[1];
            IntPtr[] small = new IntPtr[1];
            ExtractIconEx("shell32.dll", id, large, small, 1);
            return System.Drawing.Icon.FromHandle((isLarge ? large : small)[0]);
        }
    }
}
