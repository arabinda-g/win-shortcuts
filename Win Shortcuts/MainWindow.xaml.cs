using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.IO; // To resolve ambiguity between System.Windows and System.Windows.Forms


namespace Win_Shortcuts
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Constants for hotkeys
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int VK_RIGHT = 0x27;
        private const int VK_LEFT = 0x25;


        // P/Invoke declarations
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);


        public MainWindow()
        {
            InitializeComponent();

            RegisterHotKeys();
        }

        private void RegisterHotKeys()
        {
            var hWnd = new WindowInteropHelper(this).EnsureHandle();
            if (!RegisterHotKey(hWnd, 1, MOD_ALT, VK_RIGHT))
            {
                LogMessage("Failed to register Alt+Right Arrow hotkey.", LogLevel.ERR);
            }
            else
            {
                LogMessage("Alt+Right Arrow hotkey registered successfully.", LogLevel.INFO);
            }

            if (!RegisterHotKey(hWnd, 2, MOD_ALT, VK_LEFT))
            {
                LogMessage("Failed to register Alt+Left Arrow hotkey.", LogLevel.ERR);
            }
            else
            {
                LogMessage("Alt+Left Arrow hotkey registered successfully.", LogLevel.INFO);
            }

            ComponentDispatcher.ThreadFilterMessage += ComponentDispatcher_ThreadFilterMessage;
        }

        private void ComponentDispatcher_ThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (msg.message == WM_HOTKEY)
            {
                if (msg.wParam.ToInt32() == 1)
                {
                    LogMessage("Alt+Right Arrow pressed.", LogLevel.DEBUG);
                    MoveWindowToMonitor(true);
                }
                else if (msg.wParam.ToInt32() == 2)
                {
                    LogMessage("Alt+Left Arrow pressed.", LogLevel.DEBUG);
                    MoveWindowToMonitor(false);
                }
            }
        }

        private void MoveWindowToMonitor(bool moveToNext)
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                LogMessage("No foreground window found.", LogLevel.ERR);
                return;
            }

            NativeMethods.RECT windowRect;
            if (!NativeMethods.GetWindowRect(hwnd, out windowRect))
            {
                LogMessage("Failed to get window rectangle.", LogLevel.ERR);
                return;
            }

            // Check if the window is maximized
            bool isMaximized = NativeMethods.IsZoomed(hwnd);
            if (isMaximized)
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

            // Apply the new placement
            NativeMethods.SetWindowPlacement(hwnd, ref placement);

            if (isMaximized)
            {
                // Maximize the window again after it has been moved
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MAXIMIZE);
            }
            NativeMethods.SetForegroundWindow(hwnd);

            LogMessage("Window moved to another monitor successfully.", LogLevel.INFO);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            UnregisterHotKeys();
        }

        private void UnregisterHotKeys()
        {
            var hWnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hWnd, 1);
            UnregisterHotKey(hWnd, 2);
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcher_ThreadFilterMessage;
            LogMessage("Application exiting.", LogLevel.INFO);
        }




        // Enum for log levels
        public enum LogLevel
        {
            DEBUG,
            INFO,
            WARNING,
            ERR,
            CRITICAL,
            ALERT,
            EMERGENCY,
            NONE
        }

        // Flag for current logging level
        private LogLevel LOG_LEVEL = LogLevel.NONE; // Set the log level here

        // Function to log messages to a file
        private void LogMessage(string message, LogLevel level = LogLevel.INFO)
        {
            // Path to the log file
            string logFilePath = "log.txt";

            // Get the current date and time
            DateTime now = DateTime.Now;

            // Convert log level enum to string
            string levelStr = Enum.GetName(typeof(LogLevel), level);

            // Write log message to file with timestamp and log level
            using (StreamWriter logFile = new StreamWriter(logFilePath, true))
            {
                logFile.WriteLine($"[{now:yyyy-MM-ddTHH:mm:ss.fffK}] [{levelStr}] {message}");
            }
        }
    }


    class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);


        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        //public struct RECT
        //{
        //    public int Left;
        //    public int Top;
        //    public int Right;
        //    public int Bottom;
        //}

        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const int SW_RESTORE = 9;
        public const int SW_MAXIMIZE = 3;
        public const int SW_MINIMIZE = 6;


        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}