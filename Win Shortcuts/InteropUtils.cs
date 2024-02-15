using System.IO;
using System.Runtime.InteropServices;


class Logger
{
    // Enum for log levels
    public enum Level
    {
        DEBUG,
        INFO,
        WARNING,
        ERROR,
        CRITICAL,
        ALERT,
        EMERGENCY,
        NONE
    }

    // Flag for current logging level
    private const Level LOG_LEVEL = Level.NONE; // Set the log level here

    // Function to log messages to a file
    public static void Log(string message, Level level = Level.INFO)
    {
        // Check if the log level is higher than the current logging level
        if (level < LOG_LEVEL)
        {
            return;
        }
        if (message == null)
        {
            return;
        }

        // Path to the log file
        string logFilePath = "log.txt";

        // Get the current date and time
        DateTime now = DateTime.Now;

        // Convert log level enum to string
        string levelStr = Enum.GetName(typeof(Level), level);

        // Write log message to file with timestamp and log level
        using (StreamWriter logFile = new StreamWriter(logFilePath, true))
        {
            logFile.WriteLine($"[{now:yyyy-MM-ddTHH:mm:ss.fffK}] [{levelStr}] {message}");
        }
    }
}


class NativeMethods
{
    #region Window Management
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

    // Constants for SetWindowPos
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const int SW_RESTORE = 9;
    public const int SW_MAXIMIZE = 3;
    public const int SW_MINIMIZE = 6;
    #endregion


    #region Hotkeys
    // Constants for hotkeys
    public const int WM_HOTKEY = 0x0312;
    public const int MOD_ALT = 0x0001;
    public const int VK_RIGHT = 0x27;
    public const int VK_LEFT = 0x25;


    // P/Invoke declarations
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    #endregion

}
