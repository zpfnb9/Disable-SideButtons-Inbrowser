// XButtonHarness - EdgeSideGuard 的测试工具
//
// 它本身不是产品的一部分，只在仓库里用来验证拦截器是否生效。两种用途：
//   recv   打开一个测试窗口，把收到的 XBUTTON 事件写进日志（用来证明“事件确实到达了窗口”）
//   send   从外部注入一个 XBUTTON 事件（模拟按下鼠标侧键）
//
// 常用组合（详见 README 的“验证”一节）：
//   XButtonHarness.exe recv <log> <seconds> [窗口标题] [x] [y]
//   XButtonHarness.exe send <次数> [窗口标题] [1|2]
//   XButtonHarness.exe sendat <窗口标题> [1|2]     // 只把光标移过去，不抢前台
//   XButtonHarness.exe click <窗口标题>            // 真实左键点击，用于激活窗口
//   XButtonHarness.exe probe [窗口标题]            // 打印前台窗口 / 查找窗口
//
// 把本程序复制一份改名为 msedge.exe，就可以让拦截器误以为它是 Edge，
// 从而在没有浏览器的环境下测试生产配置。
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace XButtonHarness
{
    internal static class Program
    {
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_XBUTTONDBLCLK = 0x020D;

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_XDOWN = 0x0080;
        private const uint MOUSEEVENTF_XUP = 0x0100;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORPOINT { public int X; public int Y; }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string cls, string title);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out CURSORPOINT pt);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(CURSORPOINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length >= 3 && args[0] == "recv")
            {
                string title = args.Length > 3 ? args[3] : "XBUTTON-RECV";
                int x = args.Length > 4 ? int.Parse(args[4]) : -1;
                int y = args.Length > 5 ? int.Parse(args[5]) : -1;
                return Recv(args[1], int.Parse(args[2]), title, x, y);
            }
            if (args.Length >= 2 && args[0] == "send")
            {
                int count = int.Parse(args[1]);
                string part = args.Length > 2 ? args[2] : null;
                int btn = args.Length > 3 ? int.Parse(args[3]) : 1;
                return Send(count, part, btn);
            }
            if (args.Length >= 2 && args[0] == "sendat")
            {
                int btn = args.Length > 2 ? int.Parse(args[2]) : 1;
                return SendAt(args[1], btn);
            }
            if (args.Length >= 2 && args[0] == "click")
            {
                return Click(args[1]);
            }
            if (args.Length >= 1 && args[0] == "sendraw")
            {
                int btn = args.Length > 1 ? int.Parse(args[1]) : 1;
                Console.WriteLine(DescribeWindowAtCursor());
                Console.WriteLine("fg=" + DescribeForeground());
                SendOnce(btn);
                return 0;
            }
            if (args.Length >= 1 && args[0] == "probe")
            {
                string part = args.Length > 1 ? args[1] : null;
                Console.WriteLine("fg=" + DescribeForeground());
                if (part != null)
                    Console.WriteLine("found=0x" + FindWindowByTitlePart(part).ToInt64().ToString("X"));
                return 0;
            }

            Console.Error.WriteLine("usage: XButtonHarness.exe recv|send|sendat|click|probe ...");
            return 64;
        }

        // ---------------------------------------------------------------- recv

        private static int Recv(string logPath, int seconds, string title, int x, int y)
        {
            Application.EnableVisualStyles();
            Application.Run(new RecvForm(logPath, seconds, title, x, y));
            return 0;
        }

        private sealed class RecvForm : Form
        {
            private readonly System.IO.StreamWriter _w;
            private readonly Stopwatch _sw = Stopwatch.StartNew();
            private readonly int _seconds;
            private readonly Timer _timer;

            public RecvForm(string logPath, int seconds, string title, int x, int y)
            {
                _seconds = seconds;
                _w = new System.IO.StreamWriter(
                    new System.IO.FileStream(logPath, System.IO.FileMode.Append,
                        System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite));
                _w.AutoFlush = true;

                Text = title;
                if (x >= 0 && y >= 0)
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = new Point(x, y);
                }
                else
                {
                    StartPosition = FormStartPosition.CenterScreen;
                }
                Size = new Size(420, 160);
                TopMost = true;

                _timer = new Timer();
                _timer.Interval = 200;
                _timer.Tick += delegate
                {
                    if (_sw.Elapsed.TotalSeconds >= _seconds)
                    {
                        _timer.Stop();
                        Close();
                    }
                };
                _timer.Start();
            }

            protected override void OnShown(EventArgs e)
            {
                base.OnShown(e);
                Activate();
                BringToFront();
                ForceForeground(Handle);
                _w.WriteLine("recv-ready pid=" + Process.GetCurrentProcess().Id + " fg=" + DescribeForeground());
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                base.OnFormClosing(e);
                _w.WriteLine("recv-done");
                _w.Dispose();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_XBUTTONDOWN || m.Msg == WM_XBUTTONUP || m.Msg == WM_XBUTTONDBLCLK)
                {
                    long hi = ((long)m.WParam >> 16) & 0xFFFF;
                    _w.WriteLine("recv msg=0x" + m.Msg.ToString("X4") + " xbutton=" + hi);
                }
                base.WndProc(ref m);
            }
        }

        // ------------------------------------------------------- send / sendat

        private static int Send(int count, string titlePart, int btn)
        {
            IntPtr target = titlePart != null ? FindWindowByTitlePart(titlePart) : FindWindow(null, "XBUTTON-RECV");
            if (target == IntPtr.Zero)
            {
                Console.WriteLine("TARGET NOT FOUND: " + (titlePart ?? "XBUTTON-RECV"));
                return 2;
            }

            CURSORPOINT saved;
            bool haveCursor = GetCursorPos(out saved);
            RECT rect;
            if (GetWindowRect(target, out rect))
                SetCursorPos((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
            ForceForeground(target);
            System.Threading.Thread.Sleep(250);

            Console.WriteLine("target=0x" + target.ToInt64().ToString("X") + " before fg=" + DescribeForeground());
            for (int i = 0; i < count; i++)
            {
                SendOnce(btn);
                System.Threading.Thread.Sleep(150);
            }
            if (haveCursor) SetCursorPos(saved.X, saved.Y);
            return 0;
        }

        // 只把光标移到窗口上，故意不改变前台窗口：用于测试 under-cursor 分支
        private static int SendAt(string titlePart, int btn)
        {
            IntPtr target = FindWindowByTitlePart(titlePart);
            if (target == IntPtr.Zero)
            {
                Console.WriteLine("TARGET NOT FOUND: " + titlePart);
                return 2;
            }

            RECT rect;
            CURSORPOINT saved;
            bool haveCursor = GetCursorPos(out saved);
            if (GetWindowRect(target, out rect))
                SetCursorPos((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
            System.Threading.Thread.Sleep(250);

            Console.WriteLine("cursor over '" + titlePart + "' fg=" + DescribeForeground());
            Console.WriteLine(DescribeWindowAtCursor());
            SendOnce(btn);
            System.Threading.Thread.Sleep(150);
            if (haveCursor) SetCursorPos(saved.X, saved.Y);
            return 0;
        }

        private static void SendOnce(int btn)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.mouseData = (uint)btn;
            inputs[0].mi.dwFlags = MOUSEEVENTF_XDOWN;
            inputs[1].type = INPUT_MOUSE;
            inputs[1].mi.mouseData = (uint)btn;
            inputs[1].mi.dwFlags = MOUSEEVENTF_XUP;

            uint sent = SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
            Console.WriteLine("SendInput inserted=" + sent + " after fg=" + DescribeForeground());
        }

        // ------------------------------------------------------------- click

        // 真实左键点击：即使 SetForegroundWindow 被前台锁定拒绝，点击也能激活窗口
        private static int Click(string titlePart)
        {
            IntPtr target = FindWindowByTitlePart(titlePart);
            if (target == IntPtr.Zero)
            {
                Console.WriteLine("TARGET NOT FOUND: " + titlePart);
                return 2;
            }

            RECT rect;
            if (!GetWindowRect(target, out rect))
            {
                Console.WriteLine("GetWindowRect failed");
                return 2;
            }
            SetCursorPos((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
            System.Threading.Thread.Sleep(200);

            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
            inputs[1].type = INPUT_MOUSE;
            inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));

            System.Threading.Thread.Sleep(400);
            Console.WriteLine("after click: fg=" + DescribeForeground());
            return 0;
        }

        // ------------------------------------------------------------- helpers

        private static void ForceForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr fg = GetForegroundWindow();
            uint fgPid;
            uint fgThread = GetWindowThreadProcessId(fg, out fgPid);
            uint myThread = GetCurrentThreadId();

            if (fgThread != 0 && fgThread != myThread)
            {
                AttachThreadInput(fgThread, myThread, true);
                SetForegroundWindow(hWnd);
                AttachThreadInput(fgThread, myThread, false);
            }
            else
            {
                SetForegroundWindow(hWnd);
            }
        }

        private static IntPtr FindWindowByTitlePart(string part)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h)) return true;
                StringBuilder sb = new StringBuilder(512);
                GetWindowText(h, sb, sb.Capacity);
                string t = sb.ToString();
                if (t.Length > 0 && t.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static string DescribeWindowAtCursor()
        {
            CURSORPOINT c;
            if (!GetCursorPos(out c)) return "<no cursor>";
            IntPtr h = GetAncestor(WindowFromPoint(c), 2);
            if (h == IntPtr.Zero) return "cursor-window=<none>";

            uint pid;
            GetWindowThreadProcessId(h, out pid);
            string name = "?";
            try { name = Process.GetProcessById((int)pid).ProcessName; } catch { }

            StringBuilder sb = new StringBuilder(512);
            GetWindowText(h, sb, sb.Capacity);
            return "cursor-window=" + name + " '" + sb + "'";
        }

        private static string DescribeForeground()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return "<none>";

            uint pid;
            GetWindowThreadProcessId(h, out pid);
            string name = "?";
            try { name = Process.GetProcessById((int)pid).ProcessName; } catch { }

            StringBuilder sb = new StringBuilder(512);
            GetWindowText(h, sb, sb.Capacity);
            return name + "(pid=" + pid + ",'" + sb + "')";
        }
    }
}
