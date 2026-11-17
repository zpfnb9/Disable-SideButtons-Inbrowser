// EdgeSideGuard - 在 Microsoft Edge 中屏蔽鼠标侧键（前进 / 后退）
//
// 原理：安装一个 WH_MOUSE_LL 低级鼠标钩子，在系统把 XBUTTON1/XBUTTON2
// 事件投递给 Edge 之前直接吞掉。两道判定，覆盖两种误触场景：
//   1) Edge 拥有前台窗口                      -> reason=foreground
//   2) Edge 在后台，但鼠标正悬停在它的窗口上    -> reason=under-cursor
//
// 只匹配目标进程名（默认 msedge），其他任何程序完全不受影响。
//
// MIT License
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

[assembly: AssemblyTitle("EdgeSideGuard")]
[assembly: AssemblyDescription("Disables mouse side-button Back/Forward in Microsoft Edge only")]
[assembly: AssemblyProduct("EdgeSideGuard")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace EdgeSideGuard
{
    internal static class Program
    {
        private const string Version = "1.1.0";

        // Win32 常量
        private const int WH_MOUSE_LL = 14;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_XBUTTONDBLCLK = 0x020D;
        private const uint GA_ROOT = 2;

        private const int ERROR_HOOK_FAILED = 2;
        private const int ERROR_ALREADY_RUNNING = 3;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        private static readonly HashSet<string> Targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<uint, string> NameCache = new Dictionary<uint, string>();
        private static readonly object LogLock = new object();

        private static LowLevelMouseProc _proc;      // 必须用字段保住委托，否则会被 GC 回收
        private static IntPtr _hook = IntPtr.Zero;
        private static StreamWriter _log;
        private static bool _debug;

        [STAThread]
        private static int Main(string[] args)
        {
            // 先解析参数：--version / --help 不需要占用单实例，也不该被已在运行的实例挡住
            if (!ParseArgs(args)) return 0;
            if (Targets.Count == 0) Targets.Add("msedge");

            bool isNewInstance;
            using (Mutex mutex = new Mutex(true, "EdgeSideGuard.SingleInstance.v1", out isNewInstance))
            {
                if (!isNewInstance)
                {
                    lock (LogLock)
                    {
                        if (_log != null) { _log.Dispose(); _log = null; }
                    }
                    return ERROR_ALREADY_RUNNING;
                }

                try
                {
                    _proc = HookProc;
                    _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
                    if (_hook == IntPtr.Zero)
                    {
                        Log("FATAL hook install failed, error=" + Marshal.GetLastWin32Error());
                        return ERROR_HOOK_FAILED;
                    }

                    Log("started v" + Version + " targets=" + string.Join(",", Targets));

                    // 低级钩子的回调在安装它的线程上执行，所以必须跑消息循环。
                    MSG msg;
                    while (GetMessage(out msg, IntPtr.Zero, 0, 0))
                    {
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                    }
                }
                finally
                {
                    if (_hook != IntPtr.Zero)
                    {
                        UnhookWindowsHookEx(_hook);
                        _hook = IntPtr.Zero;
                    }
                    lock (LogLock)
                    {
                        if (_log != null) { _log.Flush(); _log.Dispose(); _log = null; }
                    }
                }
            }
            return 0;
        }

        private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int id = wParam.ToInt32();
                if (id == WM_XBUTTONDOWN || id == WM_XBUTTONUP || id == WM_XBUTTONDBLCLK)
                {
                    // 场景 1：目标程序正处于前台。
                    if (IsTargetWindow(GetForegroundWindow()))
                    {
                        Log("blocked msg=0x" + id.ToString("X4") + " reason=foreground");
                        return (IntPtr)1;   // 1 = 吞掉，不再传给后面的钩子和窗口
                    }

                    // 场景 2：目标程序在后台（例如另一个显示器上），但鼠标悬停在它上面。
                    // 此时点击仍然会投递给光标下的窗口，所以同样要拦。
                    MSLLHOOKSTRUCT info = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    IntPtr under = GetAncestor(WindowFromPoint(info.pt), GA_ROOT);
                    if (under != IntPtr.Zero && IsTargetWindow(under))
                    {
                        Log("blocked msg=0x" + id.ToString("X4") + " reason=under-cursor");
                        return (IntPtr)1;
                    }

                    if (_debug)
                    {
                        Log("debug event msg=0x" + id.ToString("X4")
                            + " fg=[" + ProcessNameForWindow(GetForegroundWindow()) + "]"
                            + " under=[" + ProcessNameForWindow(under) + "]"
                            + " -> pass");
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private static bool IsTargetWindow(IntPtr hWnd)
        {
            string name = ProcessNameForWindow(hWnd);
            return name != null && Targets.Contains(name);
        }

        private static string ProcessNameForWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return null;

            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == 0) return null;

            lock (NameCache)
            {
                string cached;
                if (NameCache.TryGetValue(pid, out cached)) return cached;
            }

            string name;
            try { name = Process.GetProcessById((int)pid).ProcessName; }
            catch { name = null; }

            lock (NameCache)
            {
                if (NameCache.Count > 256) NameCache.Clear();
                NameCache[pid] = name;
            }
            return name;
        }

        private static bool ParseArgs(string[] args)
        {
            bool noLog = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == "--only" && i + 1 < args.Length)
                {
                    foreach (string part in args[++i].Split(','))
                    {
                        string t = part.Trim();
                        if (t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            t = t.Substring(0, t.Length - 4);
                        if (t.Length > 0) Targets.Add(t);
                    }
                }
                else if (a == "--log" && i + 1 < args.Length)
                {
                    OpenLog(args[++i]);
                }
                else if (a == "--nolog")
                {
                    noLog = true;
                }
                else if (a == "--debug")
                {
                    _debug = true;
                }
                else if (a == "--version" || a == "-v")
                {
                    AttachParentConsole();
                    Console.WriteLine("EdgeSideGuard " + Version);
                    return false;
                }
                else if (a == "--help" || a == "-h" || a == "/?")
                {
                    AttachParentConsole();
                    Console.WriteLine("EdgeSideGuard " + Version);
                    Console.WriteLine("  Swallows mouse side buttons (Back/Forward) in the target application.");
                    Console.WriteLine("  Default target: msedge. Other applications are not affected.");
                    Console.WriteLine();
                    Console.WriteLine("  --only <a,b>     target process names, comma separated (default: msedge)");
                    Console.WriteLine("  --log <path>     write interception log to a specific file");
                    Console.WriteLine("  --nolog          do not write a log file");
                    Console.WriteLine("  --debug          log every side-button event with the resolved names");
                    Console.WriteLine("  --version        print version");
                    return false;
                }
            }

            if (!noLog && _log == null)
            {
                try
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "EdgeSideGuard");
                    OpenLog(Path.Combine(dir, "guard.log"));
                }
                catch { }
            }
            return true;
        }

        private static void AttachParentConsole()
        {
            // winexe 默认没有控制台，--version / --help 时挂到调用者的控制台上才有输出
            try
            {
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    StreamWriter stdout = new StreamWriter(Console.OpenStandardOutput());
                    stdout.AutoFlush = true;
                    Console.SetOut(stdout);
                }
            }
            catch { }
        }

        private static void OpenLog(string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                string dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // 日志超过 512KB 就轮转一次，避免无限增长
                FileInfo fi = new FileInfo(full);
                if (fi.Exists && fi.Length > 512 * 1024)
                {
                    string old = full + ".old";
                    try { if (File.Exists(old)) File.Delete(old); } catch { }
                    try { File.Move(full, old); } catch { }
                }

                _log = new StreamWriter(new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
                _log.AutoFlush = true;
            }
            catch { _log = null; }
        }

        private static void Log(string line)
        {
            lock (LogLock)
            {
                if (_log == null) return;
                try { _log.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + line); }
                catch { }
            }
        }
    }
}
