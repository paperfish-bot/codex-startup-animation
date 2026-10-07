using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class SeamlessAgent {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry {
        public uint Size, Usage, Pid;
        public IntPtr Heap;
        public uint Module, Threads, Parent;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Name;
    }
    private struct Candidate {
        public uint Pid, Parent;
        public Candidate(uint pid, uint parent) { Pid = pid; Parent = parent; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private delegate void WinEventProc(IntPtr hook, uint eventId, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("ntdll.dll")] private static extern int NtQuerySystemInformation(int informationClass, IntPtr buffer, int length, out int requiredLength);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageNameW(IntPtr process, uint flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder title, int capacity);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flag);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    private static string logPath;
    private static string mockPath;
    private static string processName = "ChatGPT";
    private static byte[] processBuffer = new byte[2 * 1024 * 1024];
    private static byte[] basicProcessBuffer = new byte[128 * 1024];
    private static bool basicProcessUnavailable;

    private static void Log(string message) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.AppendAllText(logPath, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
        } catch { }
    }
    private static List<Candidate> Snapshot() {
        List<Candidate> basic;
        if (!basicProcessUnavailable && TryBasicSnapshot(out basic)) return basic;
        basicProcessUnavailable = true;
        return FullNativeSnapshot();
    }
    private static bool TryBasicSnapshot(out List<Candidate> result) {
        result = null;
        if (IntPtr.Size != 8) return false;
        string target = processName + ".exe";
        for (int attempt = 0; attempt < 3; attempt++) {
            GCHandle pin = GCHandle.Alloc(basicProcessBuffer, GCHandleType.Pinned);
            try {
                IntPtr start = pin.AddrOfPinnedObject();
                int required;
                // SystemBasicProcessInformation is available on recent Windows 11 builds.
                int status = NtQuerySystemInformation(252, start, basicProcessBuffer.Length, out required);
                if (status == unchecked((int)0xC0000004) && required > basicProcessBuffer.Length && required < 4 * 1024 * 1024) {
                    basicProcessBuffer = new byte[Math.Max(required + 16384, basicProcessBuffer.Length * 2)];
                    continue;
                }
                if (status != 0) return false;
                var found = new List<Candidate>();
                int offset = 0, limit = required > 0 && required <= basicProcessBuffer.Length ? required : basicProcessBuffer.Length;
                while (offset >= 0 && offset + 48 <= limit) {
                    IntPtr entry = IntPtr.Add(start, offset);
                    int next = Marshal.ReadInt32(entry, 0);
                    int nameBytes = Marshal.ReadInt16(entry, 32);
                    IntPtr name = Marshal.ReadIntPtr(entry, 40);
                    if (name != IntPtr.Zero && nameBytes == target.Length * 2) {
                        bool matches = true;
                        for (int i = 0; i < target.Length; i++)
                            if (Char.ToUpperInvariant((char)Marshal.ReadInt16(name, i * 2)) != Char.ToUpperInvariant(target[i])) { matches = false; break; }
                        if (matches) found.Add(new Candidate((uint)Marshal.ReadIntPtr(entry, 8).ToInt64(),
                            (uint)Marshal.ReadIntPtr(entry, 16).ToInt64()));
                    }
                    if (next == 0) { result = found; return true; }
                    if (next < 48 || offset + next > limit) return false;
                    offset += next;
                }
                return false;
            } catch { return false; }
            finally { pin.Free(); }
        }
        return false;
    }
    private static List<Candidate> FullNativeSnapshot() {
        // Query all process names in one native call. The documented layout is checked below;
        // keep Toolhelp as a fallback if a future Windows release changes this native API.
        if (IntPtr.Size == 8) {
            for (int attempt = 0; attempt < 3; attempt++) {
                GCHandle pin = GCHandle.Alloc(processBuffer, GCHandleType.Pinned);
                try {
                    IntPtr start = pin.AddrOfPinnedObject();
                    int required;
                    int status = NtQuerySystemInformation(5, start, processBuffer.Length, out required);
                    if (status == unchecked((int)0xC0000004) && required > processBuffer.Length && required < 32 * 1024 * 1024) {
                        processBuffer = new byte[Math.Max(required + 65536, processBuffer.Length * 2)];
                        continue;
                    }
                    if (status >= 0) {
                        var result = new List<Candidate>();
                        int offset = 0, limit = required > 0 && required <= processBuffer.Length ? required : processBuffer.Length;
                        string target = processName + ".exe";
                        while (offset >= 0 && offset + 96 <= limit) {
                            IntPtr entry = IntPtr.Add(start, offset);
                            int next = Marshal.ReadInt32(entry, 0);
                            int nameBytes = Marshal.ReadInt16(entry, 56);
                            IntPtr name = Marshal.ReadIntPtr(entry, 64);
                            if (name != IntPtr.Zero && nameBytes == target.Length * 2) {
                                bool matches = true;
                                for (int i = 0; i < target.Length; i++)
                                    if (Char.ToUpperInvariant((char)Marshal.ReadInt16(name, i * 2)) != Char.ToUpperInvariant(target[i])) { matches = false; break; }
                                if (matches) result.Add(new Candidate((uint)Marshal.ReadIntPtr(entry, 80).ToInt64(),
                                    (uint)Marshal.ReadIntPtr(entry, 88).ToInt64()));
                            }
                            if (next == 0) return result;
                            if (next < 96 || offset + next > limit) break;
                            offset += next;
                        }
                    }
                } catch { }
                finally { pin.Free(); }
                break;
            }
        }
        return ToolhelpSnapshot();
    }
    private static List<Candidate> ToolhelpSnapshot() {
        var result = new List<Candidate>();
        IntPtr snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return result;
        try {
            ProcessEntry entry = new ProcessEntry();
            entry.Size = (uint)Marshal.SizeOf(typeof(ProcessEntry));
            if (Process32FirstW(snapshot, ref entry)) do {
                if (String.Equals(entry.Name, processName + ".exe", StringComparison.OrdinalIgnoreCase))
                    result.Add(new Candidate(entry.Pid, entry.Parent));
            } while (Process32NextW(snapshot, ref entry));
        } finally { CloseHandle(snapshot); }
        return result;
    }
    private static string ProcessPath(uint pid) {
        IntPtr process = OpenProcess(0x1000, false, pid);
        if (process == IntPtr.Zero) return "";
        try {
            var path = new StringBuilder(1024);
            uint size = (uint)path.Capacity;
            return QueryFullProcessImageNameW(process, 0, path, ref size) ? path.ToString() : "";
        } finally { CloseHandle(process); }
    }
    private static bool IsOfficial(uint pid) {
        string path = ProcessPath(pid);
        if (mockPath != null) return String.Equals(path, mockPath, StringComparison.OrdinalIgnoreCase);
        return path.IndexOf("\\WindowsApps\\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) >= 0 &&
               path.EndsWith("\\app\\ChatGPT.exe", StringComparison.OrdinalIgnoreCase);
    }
    private static Rectangle WindowBounds(IntPtr window) {
        NativeRect rect;
        if (!GetWindowRect(window, out rect)) return Rectangle.Empty;
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        return width >= 300 && height >= 200 ? new Rectangle(rect.Left, rect.Top, width, height) : Rectangle.Empty;
    }
    private static bool IsMainAppWindow(IntPtr window) {
        if (mockPath != null) return true;
        if (GetWindow(window, 4) != IntPtr.Zero) return false; // owned dialogs are not the main window
        int style = GetWindowLong(window, -16);
        if ((style & 0x00C40000) != 0x00C40000) return false; // caption and sizable frame
        var className = new StringBuilder(128);
        GetClassName(window, className, className.Capacity);
        if (!String.Equals(className.ToString(), "Chrome_WidgetWin_1", StringComparison.Ordinal)) return false;
        var title = new StringBuilder(128);
        GetWindowText(window, title, title.Capacity);
        return String.Equals(title.ToString(), "ChatGPT", StringComparison.Ordinal) ||
               String.Equals(title.ToString(), "Codex", StringComparison.Ordinal);
    }
    private static IntPtr FindOfficialWindow() {
        int session = Process.GetCurrentProcess().SessionId;
        foreach (Process process in Process.GetProcessesByName(processName)) {
            try {
                IntPtr handle = process.MainWindowHandle;
                if (process.SessionId == session && handle != IntPtr.Zero && (IsWindowVisible(handle) || IsIconic(handle)) &&
                    IsOfficial((uint)process.Id) && IsMainAppWindow(handle) &&
                    (IsIconic(handle) || WindowBounds(handle) != Rectangle.Empty)) return handle;
            } catch { }
            finally { process.Dispose(); }
        }
        return IntPtr.Zero;
    }

    private sealed class AgentContext : ApplicationContext {
        private const long ReopenDelayMs = 1800;
        private readonly SeamlessOverlay overlay;
        private readonly SeamlessWallpaper wallpaper;
        private readonly System.Windows.Forms.Timer poll;
        private readonly System.Windows.Forms.Timer wallpaperPoll;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly string boundsPath;
        private readonly bool probe;
        private readonly int seconds;
        private readonly WinEventProc callback;
        private IntPtr hook;
        private IntPtr nameHook;
        private IntPtr locationHook;
        private IntPtr foregroundHook;
        private HashSet<uint> seen;
        private bool hadProcess;
        private bool hadWindow;
        private bool everOpened;
        private bool ignoreNextShow;
        private bool dialogDuringAbsence;
        private uint lastWindowPid;
        private uint pendingColdRootPid;
        private readonly HashSet<IntPtr> ownedDialogs = new HashSet<IntPtr>();
        private long windowGoneAt;
        private long lastTriggerAt = -10000;
        private IntPtr officialWindow;
        private Rectangle lastBounds;

        public AgentContext(string root, bool probeOnly, int runSeconds) {
            probe = probeOnly;
            seconds = runSeconds;
            boundsPath = mockPath != null ? Path.Combine(Path.GetDirectoryName(mockPath), "window-bounds.txt") : Path.Combine(Path.GetDirectoryName(root), "window-bounds.txt");
            lastBounds = LoadBounds();
            seen = new HashSet<uint>();
            foreach (Candidate candidate in Snapshot()) if (IsOfficial(candidate.Pid)) seen.Add(candidate.Pid);
            hadProcess = seen.Count > 0;
            officialWindow = FindOfficialWindow();
            hadWindow = officialWindow != IntPtr.Zero;
            if (hadWindow) GetWindowThreadProcessId(officialWindow, out lastWindowPid);
            everOpened = hadProcess || hadWindow;
            windowGoneAt = hadWindow ? -1 : clock.ElapsedMilliseconds;
            if (hadWindow) RememberBounds(WindowBounds(officialWindow));
            if (!probe) {
                overlay = new SeamlessOverlay(root, logPath);
                overlay.Signal += OnOverlaySignal;
                overlay.Prewarm();
                try { wallpaper = new SeamlessWallpaper(root, logPath); wallpaper.Prewarm(); }
                catch (Exception error) { Log("wallpaper-error " + error.Message); }
            }
            callback = HandleWindowEvent;
            hook = SetWinEventHook(0x8000, 0x8003, IntPtr.Zero, callback, 0, 0, 0x0002);
            if (hook == IntPtr.Zero) Log("window-hook-failed " + Marshal.GetLastWin32Error());
            nameHook = SetWinEventHook(0x800C, 0x800C, IntPtr.Zero, callback, 0, 0, 0x0002);
            if (nameHook == IntPtr.Zero) Log("window-name-hook-failed " + Marshal.GetLastWin32Error());
            locationHook = SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, callback, 0, 0, 0x0002);
            if (locationHook == IntPtr.Zero) Log("window-location-hook-failed " + Marshal.GetLastWin32Error());
            foregroundHook = SetWinEventHook(3, 3, IntPtr.Zero, callback, 0, 0, 0x0002);
            poll = new System.Windows.Forms.Timer { Interval = hadProcess ? 1000 : 100 };
            poll.Tick += OnPoll;
            poll.Start();
            if (wallpaper != null) {
                wallpaperPoll = new System.Windows.Forms.Timer { Interval = 150 };
                wallpaperPoll.Tick += (s, e) => UpdateWallpaper();
                wallpaperPoll.Start();
            }
            Log("agent-start probe=" + probe + " existing=" + hadProcess + " visible=" +
                (hadWindow && IsWindowVisible(officialWindow) && !IsIconic(officialWindow)) +
                " minimized=" + (hadWindow && IsIconic(officialWindow)) + " bounds=" + lastBounds);
        }

        private Rectangle LoadBounds() {
            try {
                string[] parts = File.ReadAllText(boundsPath).Split(',');
                var bounds = new Rectangle(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
                if (bounds.Width >= 300 && bounds.Height >= 200) return bounds;
            } catch { }
            return Screen.PrimaryScreen.WorkingArea;
        }
        private void RememberBounds(Rectangle bounds) {
            if (bounds == Rectangle.Empty || bounds == lastBounds) return;
            lastBounds = bounds;
            try { File.WriteAllText(boundsPath, bounds.X + "," + bounds.Y + "," + bounds.Width + "," + bounds.Height); } catch { }
        }
        private void Trigger(string reason, bool resume) {
            long now = clock.ElapsedMilliseconds;
            if (now - lastTriggerAt < 1500 || (overlay != null && overlay.IsPlaying)) return;
            lastTriggerAt = now;
            Log("trigger " + reason + " resume=" + resume + " bounds=" + lastBounds);
            if (wallpaper != null) wallpaper.HideForApp();
            if (overlay != null) overlay.StartAnimation(lastBounds, resume);
        }

        private void UpdateWallpaper() {
            if (wallpaper == null) return;
            bool appVisible = officialWindow != IntPtr.Zero && IsWindow(officialWindow) &&
                IsWindowVisible(officialWindow) && !IsIconic(officialWindow);
            bool revealing = overlay != null && overlay.IsRevealing;
            if (!appVisible || (overlay != null && overlay.IsActive && !revealing)) {
                wallpaper.HideForApp();
                return;
            }
            Rectangle bounds = WindowBounds(officialWindow);
            if (bounds != Rectangle.Empty) wallpaper.ShowFor(bounds, officialWindow);
        }
        private void OnOverlaySignal(string action) {
            Log("overlay " + action);
            if (wallpaper != null) {
                if (action == "reveal") wallpaper.BeginReveal();
                else if (action == "complete") wallpaper.EndReveal();
            }
            if (action == "complete" && officialWindow != IntPtr.Zero && IsWindowVisible(officialWindow))
                SetForegroundWindow(officialWindow);
            if (action == "reveal" || action == "complete" || action == "assetError" || action == "error") UpdateWallpaper();
        }
        private bool IsOwnedByOfficial(IntPtr window) {
            if (officialWindow == IntPtr.Zero) return false;
            IntPtr owner = GetWindow(window, 4);
            for (int i = 0; i < 8 && owner != IntPtr.Zero; i++) {
                if (owner == officialWindow) return true;
                owner = GetWindow(owner, 4);
            }
            return false;
        }
        private void HandleWindowEvent(IntPtr eventHook, uint eventId, IntPtr window, int objectId, int childId, uint thread, uint time) {
            if (eventId == 3) {
                if (overlay != null && overlay.IsActive) {
                    IntPtr foreground = GetForegroundWindow();
                    if (foreground == officialWindow) overlay.KeepAboveApp();
                    else if (foreground != overlay.Handle) overlay.YieldToOtherApp();
                }
                UpdateWallpaper();
                return;
            }
            if (objectId != 0 || childId != 0 || window == IntPtr.Zero) return;
            if (ownedDialogs.Contains(window) && (eventId == 0x8001 || eventId == 0x8003)) {
                ownedDialogs.Remove(window);
                return;
            }
            if (GetAncestor(window, 2) != window) return;
            if (window != officialWindow && IsOwnedByOfficial(window)) {
                if (eventId == 0x8002) {
                    ownedDialogs.Add(window);
                    if (!hadWindow) dialogDuringAbsence = true;
                }
                return;
            }
            if (eventId == 0x800B && window == officialWindow) {
                Rectangle updated = WindowBounds(window);
                if (updated != Rectangle.Empty) {
                    RememberBounds(updated);
                    UpdateWallpaper();
                }
                return;
            }
            if (eventId == 0x8003 && window == officialWindow && !IsIconic(window)) {
                hadWindow = false;
                windowGoneAt = clock.ElapsedMilliseconds;
                dialogDuringAbsence = ownedDialogs.Count > 0;
                Log("window-hide " + window);
                UpdateWallpaper();
                return;
            }
            if (eventId != 0x8000 && eventId != 0x8002 && eventId != 0x800C) return;
            uint pid;
            GetWindowThreadProcessId(window, out pid);
            if (!IsOfficial(pid) || !IsMainAppWindow(window)) return;
            Rectangle bounds = WindowBounds(window);
            if (bounds == Rectangle.Empty) return;
            Log("window-event " + eventId.ToString("X") + " window=" + window + " visible=" + IsWindowVisible(window));
            officialWindow = window;
            RememberBounds(bounds);
            bool pendingColdWindow = pendingColdRootPid != 0 && pid != lastWindowPid;
            bool coldLaunch = !hadWindow && !pendingColdWindow &&
                (!seen.Contains(pid) || (lastWindowPid != 0 && pid != lastWindowPid) ||
                 (!everOpened && !hadProcess));
            bool reopen = !hadWindow && !pendingColdWindow && !coldLaunch &&
                !dialogDuringAbsence && everOpened && windowGoneAt >= 0 &&
                clock.ElapsedMilliseconds - windowGoneAt >= ReopenDelayMs;
            if (coldLaunch || reopen) {
                Trigger("window-event-" + eventId.ToString("X"), reopen);
                ignoreNextShow = true;
                everOpened = true;
            }
            if (pendingColdWindow) {
                Log("window-event cold-process-already-started " + pid);
                pendingColdRootPid = 0;
                ignoreNextShow = true;
            }
            lastWindowPid = pid;
            seen.Add(pid);
            hadProcess = true;
            if (dialogDuringAbsence && eventId == 0x8002) {
                Log("window-show dialog-skip " + window);
                ignoreNextShow = true;
                dialogDuringAbsence = false;
            }
            if (eventId == 0x8002 && overlay != null) overlay.KeepAboveApp();
        }
        private void OnPoll(object sender, EventArgs args) {
            if (seconds > 0 && clock.Elapsed.TotalSeconds >= seconds) { ExitThread(); return; }
            List<Candidate> snapshot = Snapshot();
            var current = new HashSet<uint>();
            foreach (Candidate candidate in snapshot)
                if (seen.Contains(candidate.Pid) || IsOfficial(candidate.Pid)) current.Add(candidate.Pid);
            foreach (Candidate candidate in snapshot) {
                if (!current.Contains(candidate.Pid) || seen.Contains(candidate.Pid)) continue;
                if (current.Contains(candidate.Parent)) continue;
                bool appWindowPresent = officialWindow != IntPtr.Zero && IsWindow(officialWindow) &&
                    (IsWindowVisible(officialWindow) || IsIconic(officialWindow));
                bool oldMainProcessAlive = lastWindowPid != 0 && current.Contains(lastWindowPid);
                if (!appWindowPresent && !oldMainProcessAlive) {
                    Trigger("process-start", false);
                    pendingColdRootPid = candidate.Pid;
                    ignoreNextShow = true;
                } else Log("process-start existing-app-skip " + candidate.Pid);
                everOpened = true;
            }
            seen = current;
            hadProcess = current.Count > 0;
            if (pendingColdRootPid != 0 && !current.Contains(pendingColdRootPid)) pendingColdRootPid = 0;
            IntPtr visibleWindow = officialWindow != IntPtr.Zero && IsWindow(officialWindow) &&
                IsWindowVisible(officialWindow) && WindowBounds(officialWindow) != Rectangle.Empty
                ? officialWindow : current.Count > 0 ? FindOfficialWindow() : IntPtr.Zero;
            bool visibleNow = visibleWindow != IntPtr.Zero;
            bool minimizedNow = !visibleNow && officialWindow != IntPtr.Zero &&
                IsWindow(officialWindow) && IsIconic(officialWindow);
            if (visibleNow) {
                officialWindow = visibleWindow;
                Rectangle bounds = WindowBounds(visibleWindow);
                RememberBounds(bounds);
            }
            if (hadWindow && !visibleNow && !minimizedNow) windowGoneAt = clock.ElapsedMilliseconds;
            if (!hadWindow && visibleNow) {
                uint visiblePid;
                GetWindowThreadProcessId(visibleWindow, out visiblePid);
                if (pendingColdRootPid != 0 && visiblePid != lastWindowPid) {
                    Log("window-show cold-process-already-started " + visiblePid);
                    pendingColdRootPid = 0;
                    ignoreNextShow = true;
                }
                if (!ignoreNextShow && everOpened && windowGoneAt >= 0 && clock.ElapsedMilliseconds - windowGoneAt >= ReopenDelayMs)
                    Trigger("window-show", true);
                ignoreNextShow = false;
                everOpened = true;
                lastWindowPid = visiblePid;
            }
            hadWindow = visibleNow || minimizedNow;
            if (!hadProcess && !visibleNow && !minimizedNow) everOpened = false;
            poll.Interval = hadProcess || visibleNow || minimizedNow ? 1000 : 100;
            UpdateWallpaper();
        }
        protected override void ExitThreadCore() {
            poll.Stop(); poll.Dispose();
            if (wallpaperPoll != null) { wallpaperPoll.Stop(); wallpaperPoll.Dispose(); }
            if (hook != IntPtr.Zero) UnhookWinEvent(hook);
            if (nameHook != IntPtr.Zero) UnhookWinEvent(nameHook);
            if (locationHook != IntPtr.Zero) UnhookWinEvent(locationHook);
            if (foregroundHook != IntPtr.Zero) UnhookWinEvent(foregroundHook);
            if (overlay != null) { overlay.HideOverlay(); overlay.Close(); overlay.Dispose(); }
            if (wallpaper != null) { wallpaper.HideForApp(); wallpaper.Close(); wallpaper.Dispose(); }
            Log("agent-stop");
            base.ExitThreadCore();
        }
    }

    [STAThread] private static void Main(string[] args) {
        if (args.Length < 1) return;
        string root = Path.GetFullPath(args[0]);
        foreach (string argument in args) if (argument.StartsWith("--mock=")) {
            mockPath = Path.GetFullPath(argument.Substring(7));
            processName = Path.GetFileNameWithoutExtension(mockPath);
        }
        logPath = mockPath != null ? Path.Combine(Path.GetDirectoryName(mockPath), "SeamlessAgentMock.log") : Path.Combine(Path.GetDirectoryName(root), "Logs", "SeamlessAgent.log");
        bool probe = Array.IndexOf(args, "--probe") >= 0;
        int seconds = 0;
        foreach (string argument in args) if (argument.StartsWith("--seconds="))
            Int32.TryParse(argument.Substring(10), out seconds);
        bool created;
        using (var mutex = new Mutex(true, mockPath != null ? "Local\\CodexSeamlessAnimationAgentMock" : "Local\\CodexSeamlessAnimationAgent", out created)) {
            if (!created) return;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AgentContext(root, probe, seconds));
        }
    }
}
