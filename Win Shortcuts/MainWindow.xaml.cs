using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Diagnostics; // To resolve ambiguity between System.Windows and System.Windows.Forms


namespace Win_Shortcuts
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // List of applications that require special handling when moving to another monitor
        private readonly List<string> specialHandlingApps = ["Code"];

        public MainWindow()
        {
            InitializeComponent();

            //RegisterHotKeys();

            // Set up the mouse hook
            MouseHook.SetHook();

        }

        private void RegisterHotKeys()
        {
            var hWnd = new WindowInteropHelper(this).EnsureHandle();
            if (!NativeMethods.RegisterHotKey(hWnd, 1, NativeMethods.MOD_ALT, NativeMethods.VK_RIGHT))
            {
                Logger.Log("Failed to register Alt+Right Arrow hotkey.", Logger.Level.ERROR);
            }
            else
            {
                Logger.Log("Alt+Right Arrow hotkey registered successfully.", Logger.Level.INFO);
            }

            if (!NativeMethods.RegisterHotKey(hWnd, 2, NativeMethods.MOD_ALT, NativeMethods.VK_LEFT))
            {
                Logger.Log("Failed to register Alt+Left Arrow hotkey.", Logger.Level.ERROR);
            }
            else
            {
                Logger.Log("Alt+Left Arrow hotkey registered successfully.", Logger.Level.INFO);
            }

            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;
        }

        private void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == NativeMethods.WM_HOTKEY)
            {
                if (msg.wParam.ToInt32() == 1)
                {
                    Logger.Log("Alt+Right Arrow pressed.", Logger.Level.DEBUG);
                    MoveWindowToMonitor(true);
                }
                else if (msg.wParam.ToInt32() == 2)
                {
                    Logger.Log("Alt+Left Arrow pressed.", Logger.Level.DEBUG);
                    MoveWindowToMonitor(false);
                }
            }
        }

        private string GetProcessNameByWindowHandle(IntPtr hWnd)
        {
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            try
            {
                Process process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch (ArgumentException)
            {
                // The process might no longer be running.
                return string.Empty;
            }
        }

        private void MoveWindowToMonitor(bool moveToNext)
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                Logger.Log("No foreground window found.", Logger.Level.ERROR);
                return;
            }

            NativeMethods.RECT windowRect;
            if (!NativeMethods.GetWindowRect(hwnd, out windowRect))
            {
                Logger.Log("Failed to get window rectangle.", Logger.Level.ERROR);
                return;
            }

            // Check if the window is maximized
            bool isMaximized = NativeMethods.IsZoomed(hwnd);
            string processName = GetProcessNameByWindowHandle(hwnd);
            bool requiresSpecialHandling = specialHandlingApps.Contains(processName);

            // Restore the window if it's maximized or requires special handling
            if (requiresSpecialHandling)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                // Small delay to ensure the window has time to restore
                Thread.Sleep(100);
            }
            else if (isMaximized)
            {
                // Restore the window before moving it to adjust its position
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
            }

            NativeMethods.WINDOWPLACEMENT placement = new NativeMethods.WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(typeof(NativeMethods.WINDOWPLACEMENT));
            NativeMethods.GetWindowPlacement(hwnd, ref placement);

            var currentScreen = Screen.FromHandle(hwnd);
            var allScreens = Screen.AllScreens;
            int currentIndex = Array.IndexOf(allScreens, currentScreen);
            int targetIndex = moveToNext ? (currentIndex + 1) % allScreens.Length : (currentIndex - 1 + allScreens.Length) % allScreens.Length;
            var targetScreen = allScreens[targetIndex];

            // Calculate the new position for the window, assuming it's not maximized
            if (isMaximized)
            {
                int width = placement.rcNormalPosition.Right - placement.rcNormalPosition.Left;
                int height = placement.rcNormalPosition.Bottom - placement.rcNormalPosition.Top;
                placement.rcNormalPosition.Left = targetScreen.Bounds.Left + (targetScreen.Bounds.Width - width) / 2;
                placement.rcNormalPosition.Top = targetScreen.Bounds.Top + (targetScreen.Bounds.Height - height) / 2;
                placement.rcNormalPosition.Right = placement.rcNormalPosition.Left + width;
                placement.rcNormalPosition.Bottom = placement.rcNormalPosition.Top + height;
            }
            else
            {
                placement.rcNormalPosition.Left = targetScreen.Bounds.Left + windowRect.Left - currentScreen.Bounds.Left;
                placement.rcNormalPosition.Top = targetScreen.Bounds.Top + windowRect.Top - currentScreen.Bounds.Top;
                placement.rcNormalPosition.Right = placement.rcNormalPosition.Left + windowRect.Right - windowRect.Left;
                placement.rcNormalPosition.Bottom = placement.rcNormalPosition.Top + windowRect.Bottom - windowRect.Top;
            }

            // Apply the new placement. This will set the window's position and size even if it was minimized
            NativeMethods.SetWindowPlacement(hwnd, ref placement);

            if (isMaximized)
            {
                // Maximize the window again after it has been moved
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MAXIMIZE);
            }

            // Bring the window to the front
            NativeMethods.SetForegroundWindow(hwnd);

            Logger.Log("Window moved to another monitor successfully.", Logger.Level.INFO);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            //UnregisterHotKeys();

            // Release the mouse hook
            MouseHook.ReleaseHook();
        }

        private void UnregisterHotKeys()
        {
            var hWnd = new WindowInteropHelper(this).Handle;
            NativeMethods.UnregisterHotKey(hWnd, 1);
            NativeMethods.UnregisterHotKey(hWnd, 2);
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcher_ThreadFilterMessage;
            Logger.Log("Application exiting.", Logger.Level.INFO);
        }
    }

    class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private static LowLevelMouseProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static bool _isDragging = false;
        private static POINT _startPoint;


        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static void SetHook()
        {
            _hookID = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
        }

        public static void ReleaseHook()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                switch ((int)wParam)
                {
                    case WM_RBUTTONDOWN:
                        _isDragging = true;
                        _startPoint = (POINT)Marshal.PtrToStructure(lParam, typeof(POINT));
                        break;
                    case WM_MOUSEMOVE:
                        if (_isDragging)
                        {
                            POINT currentPoint = (POINT)Marshal.PtrToStructure(lParam, typeof(POINT));
                            MoveWindowWithCursor(_startPoint, currentPoint);
                            _startPoint = currentPoint; // Update start point for smooth movement
                        }
                        break;
                    case WM_RBUTTONUP:
                        _isDragging = false;
                        break;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        // Add remaining P/Invoke signatures for moving the window
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_SHOWWINDOW = 0x0040;

        private static void MoveWindowWithCursor(POINT startPoint, POINT currentPoint)
        {
            // Calculate deltas
            int deltaX = currentPoint.x - startPoint.x;
            int deltaY = currentPoint.y - startPoint.y;

            // Get the window handle from the cursor position
            if (GetCursorPos(out POINT cursorPos))
            {
                IntPtr windowHandle = WindowFromPoint(cursorPos);
                if (windowHandle != IntPtr.Zero)
                {
                    // Get current window position
                    GetWindowRect(windowHandle, out RECT windowRect);
                    int newX = windowRect.Left + deltaX;
                    int newY = windowRect.Top + deltaY;

                    // Move the window
                    SetWindowPos(windowHandle, IntPtr.Zero, newX, newY, 0, 0, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
            }
        }

    }
}