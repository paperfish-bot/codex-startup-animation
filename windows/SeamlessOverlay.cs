using System;
using System.Drawing;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal sealed class SeamlessOverlay : Form {
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    private readonly string root;
    private readonly WebView2 browser;
    private readonly string logPath;
    private bool initializing;
    private bool ready;
    private bool pendingStart;
    private bool pendingResume;
    private bool yielded;
    private readonly System.Windows.Forms.Timer revealTimer;
    private long revealStarted;
    public bool IsPlaying { get; private set; }
    public bool IsActive { get { return IsPlaying || pendingStart; } }
    public bool IsRevealing { get; private set; }
    public bool IsReady { get { return ready; } }
    public event Action<string> Signal;

    public SeamlessOverlay(string animationRoot, string logFile) {
        root = Path.GetFullPath(animationRoot);
        logPath = logFile;
        BackColor = Color.FromArgb(8, 6, 13);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(-30000, -30000, 1000, 660);
        KeyPreview = true;
        KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Escape && IsPlaying) {
                e.Handled = true;
                browser.ExecuteScriptAsync("window.launcherUI && window.launcherUI.skip && window.launcherUI.skip()");
            }
        };
        browser = new WebView2();
        browser.Dock = DockStyle.Fill;
        browser.DefaultBackgroundColor = BackColor;
        Controls.Add(browser);
        revealTimer = new System.Windows.Forms.Timer { Interval = 16 };
        revealTimer.Tick += (s, e) => {
            double elapsedMs = (Stopwatch.GetTimestamp() - revealStarted) * 1000.0 / Stopwatch.Frequency;
            double t = Math.Max(0, Math.Min(1, elapsedMs / 1180.0));
            Opacity = 1 - t * t * (3 - 2 * t);
            if (t >= 1) revealTimer.Stop();
        };
        Shown += (s, e) => { if (!initializing) InitializeBrowser(); };
        FormClosed += (s, e) => { revealTimer.Dispose(); browser.Dispose(); };
    }

    private void Log(string message) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.AppendAllText(logPath, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
        } catch { }
    }

    public void Prewarm() {
        if (initializing) return;
        Show();
    }

    private async void InitializeBrowser() {
        initializing = true;
        try {
            if (!File.Exists(Path.Combine(root, "index.html"))) throw new FileNotFoundException("index.html");
            string profile = Path.Combine(Path.GetDirectoryName(logPath), "SeamlessWebViewProfile");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, profile);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            browser.CoreWebView2.WebMessageReceived += OnWebMessage;
            browser.CoreWebView2.NavigationCompleted += (s, e) => Log("navigation " + e.IsSuccess + " " + e.WebErrorStatus);
            await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.AEMEATH_PREWARM=true;");
            browser.Source = new Uri(Path.Combine(root, "index.html"));
            Log("prewarm-navigation " + browser.Source.AbsoluteUri);
        } catch (Exception error) {
            Log("prewarm-error " + error);
            HideOverlay();
            if (Signal != null) Signal("error");
        }
    }

    private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs args) {
        string action;
        try { action = args.TryGetWebMessageAsString(); }
        catch { return; }
        Log("web " + action);
        if (action == "ready") {
            ready = true;
            if (pendingStart) BeginAnimation();
            else Hide();
        } else if (action == "complete" || action == "assetError") {
            HideOverlay();
            if (Signal != null) Signal(action);
        } else if (action == "reveal") {
            IsRevealing = true;
            Log("reveal bounds=" + Bounds);
            revealStarted = Stopwatch.GetTimestamp();
            revealTimer.Start();
            if (Signal != null) Signal(action);
        }
    }

    public void StartAnimation(Rectangle bounds, bool resume) {
        if (IsPlaying) return;
        revealTimer.Stop();
        IsRevealing = false;
        Opacity = 1;
        pendingStart = true;
        pendingResume = resume;
        if (bounds.Width < 300 || bounds.Height < 200) bounds = Screen.PrimaryScreen.WorkingArea;
        Bounds = bounds;
        TopMost = true;
        Show();
        BringToFront();
        SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013); // keep animation above a newly opened App window
        yielded = false;
        Log("show " + bounds + " resume=" + resume + " ready=" + ready);
        if (ready) BeginAnimation();
    }

    private async void BeginAnimation() {
        if (!pendingStart || !ready) return;
        pendingStart = false;
        IsPlaying = true;
        try {
            string script = "window.launcherUI.beginLaunch(" + (pendingResume ? "true" : "false") + ")";
            string result = await browser.ExecuteScriptAsync(script);
            Log("animation-script " + result);
            if (result != "true") throw new InvalidOperationException("Animation did not start");
            if (Signal != null) Signal("started");
        } catch (Exception error) {
            Log("animation-error " + error);
            HideOverlay();
            if (Signal != null) Signal("error");
        }
    }

    public void KeepAboveApp() {
        if (!IsActive || !Visible) return;
        TopMost = true;
        SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        if (yielded) Log("cover-resumed");
        yielded = false;
    }

    public void YieldToOtherApp() {
        if (!IsActive || !Visible || yielded) return;
        TopMost = false;
        SendToBack();
        yielded = true;
        Log("cover-yielded");
    }

    public void HideOverlay() {
        revealTimer.Stop();
        IsRevealing = false;
        pendingStart = false;
        IsPlaying = false;
        yielded = false;
        Hide();
        TopMost = false;
        Opacity = 1;
        Log("hide");
    }
}
