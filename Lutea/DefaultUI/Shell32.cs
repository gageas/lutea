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

        public static void OpenPropertiesDialog(IntPtr hWnd, string file)
        {
            var info = new SHELLEXECUTEINFO(hWnd);
            info.lpVerb = "properties";
            info.lpFile = file;
            info.fMask = SHELLEXECUTEINFO.FMASK.SEE_MASK_CONNECTNETDRV | SHELLEXECUTEINFO.FMASK.SEE_MASK_UNICODE | SHELLEXECUTEINFO.FMASK.SEE_MASK_NOCLOSEPROCESS | SHELLEXECUTEINFO.FMASK.SEE_MASK_INVOKEIDLIST | SHELLEXECUTEINFO.FMASK.SEE_MASK_FLAG_NO_UI;
            var ret = ShellExecuteExW(ref info);
        }

        [DllImport("Shell32.dll", CharSet=CharSet.Unicode)]
        extern static int ShellExecuteExW(ref SHELLEXECUTEINFO lpExecInfo);
        struct SHELLEXECUTEINFO
        {
            public Int32 cbSize;
            public FMASK fMask;
            IntPtr hWnd;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpDirectory;
            int nShow;
            IntPtr hInstApp;
            IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPWStr)]
            string lpClass;
            IntPtr hkeyClass;
            UInt32 dwHotKey;
            IntPtr hIcon;
            IntPtr hProcess;

            [Flags]
            public enum FMASK : uint
            {
                SEE_MASK_CLASSKEY = 0x03,
                SEE_MASK_CLASSNAME = 0x01,
                SEE_MASK_CONNECTNETDRV = 0x80,
                SEE_MASK_DOENVSUBST = 0x200,
                SEE_MASK_FLAG_DDEWAIT = 0x100,
                SEE_MASK_FLAG_NO_UI = 0x400,
                SEE_MASK_HOTKEY = 0x20,
                SEE_MASK_ICON = 0x10,
                SEE_MASK_IDLIST = 0x04,
                SEE_MASK_INVOKEIDLIST = 0xC,
                SEE_MASK_NOCLOSEPROCESS = 0x40,
                SEE_MASK_UNICODE = 0x00004000,
            }

            public SHELLEXECUTEINFO(IntPtr hWnd){
                this.fMask = 0;
                this.hWnd = hWnd;
                this.lpVerb = null;
                this.lpFile = null;
                this.lpParameters = null;
                this.lpDirectory = null;
                this.nShow = 0;
                this.hInstApp = IntPtr.Zero;
                this.lpIDList = IntPtr.Zero;
                this.lpClass = null;
                this.hkeyClass = IntPtr.Zero;
                this.dwHotKey = 0;
                this.hIcon = IntPtr.Zero;
                this.hProcess = IntPtr.Zero;
                this.cbSize = 0;
                cbSize = Marshal.SizeOf(typeof(SHELLEXECUTEINFO));
            }
        }
    }
}
