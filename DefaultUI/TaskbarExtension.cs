using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Gageas.Lutea.DefaultUI
{
    /// <summary>
    /// http://msdn.microsoft.com/en-us/library/dd391692(VS.85).aspx 
    /// に貼られてたコードをベースに修正を加えつつラップ
    /// </summary>
    class TaskbarExtension
    {
        [ComImport(), Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ITaskbarList
        {
            #region ITaskbarList
            void HrInit();
            void AddTab(IntPtr hWnd);
            void DeleteTab(IntPtr hWnd);
            void ActivateTab(IntPtr hWnd);
            void SetActiveAlt(IntPtr hWnd);
            #endregion
            #region ITaskbarList2
            IntPtr MarkFullscreenWindow(IntPtr hWnd, bool fFullscreen);
            #endregion
            #region ITaskbarList3
            /// <summary>
            /// 设置任务栏显示进度
            /// </summary>
            /// <param name="hWnd">任务栏对应的窗口句柄</param>
            /// <param name="Completed">进度的当前值</param>
            /// <param name="Total">总的进度值</param>
            void SetProgressValue(IntPtr hWnd, ulong Completed, ulong Total);
            /// <summary>
            /// 设置任务栏状态
            /// </summary>
            /// <param name="hWnd">任务栏对应的窗口句柄</param>
            /// <param name="Flags">状态指示，具体见TbpFlag定义</param>
            void SetProgressState(IntPtr hWnd, TbpFlag Flags);
            /// <summary>
            /// 
            /// </summary>
            /// <param name="hWndTab"></param>
            /// <param name="hWndMDI"></param>
            void RegisterTab(IntPtr hWndTab, IntPtr hWndMDI);
            /// <summary>
            /// 
            /// </summary>
            /// <param name="hWndTab"></param>
            void UnregisterTab(IntPtr hWndTab);
            void SetTabOrder(IntPtr hWndTab, IntPtr hwndInsertBefore);
            void SetTabActive(IntPtr hWndTab, IntPtr hWndMDI, uint dwReserved);
            void ThumbBarAddButtons(IntPtr hWnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray)] ThumbButton[] pButtons);
            void ThumbBarUpdateButtons(IntPtr hWnd, uint cButtons, [MarshalAs(UnmanagedType.LPArray)]ThumbButton[] pButtons);
            void ThumbBarSetImageList(IntPtr hWnd, IntPtr himl);
            void SetOverlayIcon(IntPtr hWnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)]string pszDescription);
            void SetThumbnailTooltip(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)]string pszTip);
            void SetThumbnailClip(IntPtr hWnd, ref tagRECT prcClip);

            #endregion
        }
        [ComImport(), Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
        internal class TaskbarList { }

        [Flags]
        public enum TbpFlag : uint
        {
            NoProgress = 0x00, //不显示进度
            Indeterminate = 0x01, //进度循环显示
            Normal = 0x02, //进度条显示绿色
            Error = 0x04, //进度条红色显示
            Paused = 0x08 //进度条黄色显示
        }
        /// <summary>
        /// 指名在ThumbButton结构中哪个成员包含有有效信息
        /// </summary>
        [Flags]
        public enum ThumbButtonMask : uint
        {
            Bitmap = 0x01, //ThumbButton.iBitmap包含有效信息
            Icon = 0x02, //ThumbButton.hIcon包含有效信息
            ToolTip = 0x04, //ThumbButton.szTip包含有效信息
            Flags = 0x08 //ThumbButton.dwFlags包含有效信息
        }
        [Flags]
        public enum ThumbButtonFlags : uint
        {
            Enabled = 0x00, //按钮是可用的
            Disabled = 0x01, //按钮是不可用的
            DisMissonClick = 0x02, //当按钮被点击，任务栏按钮的弹出立刻关闭
            NoBackground = 0x04, //不标示按钮边框，只显示按钮图像
            Hidden = 0x08, //隐藏按钮
            NonInterActive = 0x10 //该按钮启用，但没有互动，没有按下按钮的状态绘制。此值用于按钮所在的通知是在使用实例。
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tagRECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ThumbButton
        {
            public ThumbButtonMask dwMask;
            public uint iID;
            public uint iBitmap;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szTip;
            public ThumbButtonFlags dwFlags;
        }



        IntPtr pTaskbarList = IntPtr.Zero;
        ITaskbarList taskbar = null;

        public ITaskbarList Taskbar
        {
            get { return taskbar; }
        }

        private readonly Guid CLSID_TaskbarList = new Guid(0x56fdf344, 0xfd6d, 0x11d0, 0x95, 0x8a, 0x00, 0x60, 0x97, 0xc9, 0xa0, 0x90);

        private IntPtr hWnd;
        public readonly uint WM_TBC;
        public TaskbarExtension(IntPtr hWnd)
        {
            Com.CoCreateInstance(CLSID_TaskbarList, IntPtr.Zero, Com.CLSCTX_ALL, typeof(ITaskbarList).GUID, out pTaskbarList);
            if (pTaskbarList == IntPtr.Zero)
            {
                throw new Exception();
            }
            taskbar = Marshal.GetTypedObjectForIUnknown(pTaskbarList, typeof(ITaskbarList)) as ITaskbarList;

            this.hWnd = hWnd;
            WM_TBC = RegisterWindowMessage("TaskbarButtonCreated");
        }

        [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
        static extern uint RegisterWindowMessage(string lpString);

        public void ThumbBarAddButtons(ThumbButton[] buttons){
            taskbar.ThumbBarAddButtons(this.hWnd, (uint)buttons.Length, buttons);
        }
        public void ThumbBarSetImageList(System.Windows.Forms.ImageList imagelist)
        {
            taskbar.ThumbBarSetImageList(hWnd, imagelist.Handle);
        }
    }
}
