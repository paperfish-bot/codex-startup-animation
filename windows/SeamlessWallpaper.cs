using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

// Click-through video above the official window. Keep alpha low so its text stays legible.
internal sealed class SeamlessWallpaper : Form {
    private const double FinalOpacity = .28;
    private readonly string root;
    private readonly string logPath;
    private readonly Image fallback;
    private readonly WebView2 browser;
    private readonly Timer revealTimer;
    private bool initializing;
    private bool videoReady;
    private bool wantedVisible;
    private long revealStarted;
    private Rectangle lastBounds;
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);

    public SeamlessWallpaper(string animationRoot, string agentLogPath) {
        root = Path.GetFullPath(animationRoot);
        logPath = agentLogPath;
        fallback = LoadTonedFrame(Path.Combine(root, "assets", "artwork.jpg"));
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(-30000, -30000, 1000, 660);
        BackColor = Color.FromArgb(217, 230, 239);
        Opacity = FinalOpacity;
        browser = new WebView2 { Dock = DockStyle.Fill, Visible = true, DefaultBackgroundColor = BackColor };
        Controls.Add(browser);
        Shown += (s, e) => { if (!initializing) InitializeBrowser(); };
        revealTimer = new Timer { Interval = 16 };
        revealTimer.Tick += (s, e) => {
            double elapsedMs = (Stopwatch.GetTimestamp() - revealStarted) * 1000.0 / Stopwatch.Frequency;
            double t = Math.Max(0, Math.Min(1, elapsedMs / 1180.0));
            double eased = t * t * (3 - 2 * t);
            Opacity = 1 - (1 - FinalOpacity) * eased;
            if (t >= 1) revealTimer.Stop();
        };
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    private void Log(string message) {
        try { File.AppendAllText(logPath, DateTime.UtcNow.ToString("o") + " wallpaper " + message + Environment.NewLine); }
        catch { }
    }

    private static Bitmap LoadTonedFrame(string path) {
        using (Image source = Image.FromFile(path)) {
            var toned = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            const float brightness = .55f, saturation = 1.45f, contrast = 1.35f;
            float gain = brightness * contrast, offset = brightness * (1 - contrast) * .5f;
            float neutral = 1 - saturation;
            float red = neutral * .2126f, green = neutral * .7152f, blue = neutral * .0722f;
            var matrix = new ColorMatrix(new[] {
                new[] { gain * (red + saturation), gain * red, gain * red, 0f, 0f },
                new[] { gain * green, gain * (green + saturation), gain * green, 0f, 0f },
                new[] { gain * blue, gain * blue, gain * (blue + saturation), 0f, 0f },
                new[] { 0f, 0f, 0f, 1f, 0f },
                new[] { offset, offset, offset, 0f, 1f }
            });
            using (var attributes = new ImageAttributes())
            using (var graphics = Graphics.FromImage(toned)) {
                attributes.SetColorMatrix(matrix);
                graphics.DrawImage(source, new Rectangle(0, 0, toned.Width, toned.Height),
                    0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
            }
            return toned;
        }
    }

    public void Prewarm() { if (!initializing) Show(); }

    private async void InitializeBrowser() {
        initializing = true;
        try {
            string page = Path.Combine(root, "wallpaper.html");
            if (!File.Exists(page)) throw new FileNotFoundException("wallpaper.html");
            string profile = Path.Combine(Path.GetDirectoryName(logPath), "SeamlessWallpaperProfile");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, profile);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            browser.CoreWebView2.WebMessageReceived += (s, e) => {
                string action;
                try { action = e.TryGetWebMessageAsString(); } catch { return; }
                Log(action);
                if (action == "video-ready") {
                    videoReady = true;
                    browser.Visible = true;
                    Invalidate();
                    if (wantedVisible) Play(); else Hide();
                }
                else if (action == "video-error") { videoReady = false; browser.Visible = false; Invalidate(); }
            };
            browser.CoreWebView2.NavigationCompleted += (s, e) => Log("navigation " + e.IsSuccess + " " + e.WebErrorStatus);
            browser.Source = new Uri(page);
        } catch (Exception error) {
            Log("init-error " + error.Message);
            browser.Visible = false;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
        get {
            CreateParams p = base.CreateParams;
            p.ExStyle |= 0x20 | 0x80 | 0x08000000; // transparent hit test, tool window, no activation
            return p;
        }
    }

    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; } // HTTRANSPARENT
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        if (videoReady) return;
        double scale = Math.Max((double)ClientSize.Width / fallback.Width, (double)ClientSize.Height / fallback.Height);
        int width = (int)Math.Ceiling(ClientSize.Width / scale);
        int height = (int)Math.Ceiling(ClientSize.Height / scale);
        Rectangle source = new Rectangle((fallback.Width - width) / 2, (fallback.Height - height) / 2, width, height);
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(fallback, ClientRectangle, source, GraphicsUnit.Pixel);
    }

    private void Play() {
        if (videoReady && browser.CoreWebView2 != null) {
            Log("play");
            browser.ExecuteScriptAsync("window.wallpaperVideo && window.wallpaperVideo.play()");
        }
    }

    private void Pause() {
        if (videoReady && browser.CoreWebView2 != null)
            browser.ExecuteScriptAsync("window.wallpaperVideo && window.wallpaperVideo.pause()");
    }

    public void ShowFor(Rectangle bounds, IntPtr appWindow) {
        if (bounds.Width < 300 || bounds.Height < 200 || appWindow == IntPtr.Zero) return;
        wantedVisible = true;
        bool changed = lastBounds != bounds, becameVisible = !Visible;
        if (changed) { Bounds = bounds; lastBounds = bounds; Invalidate(); }
        if (becameVisible) { Show(); ShowWindow(Handle, 4); Refresh(); Play(); }
        if (Visible && GetWindow(Handle, 2) != appWindow) {
            IntPtr previous = GetWindow(appWindow, 3);
            bool appTopmost = (GetWindowLong(appWindow, -20) & 0x8) != 0;
            bool previousTopmost = previous != IntPtr.Zero && (GetWindowLong(previous, -20) & 0x8) != 0;
            IntPtr after = previous == IntPtr.Zero || (previousTopmost && !appTopmost)
                ? new IntPtr(appTopmost ? -1 : 0) : previous;
            SetWindowPos(Handle, after, 0, 0, 0, 0, 0x0013);
        }
    }

    public void BeginReveal() {
        revealTimer.Stop();
        Opacity = 1;
        if (videoReady && browser.CoreWebView2 != null)
            browser.ExecuteScriptAsync("window.wallpaperReveal && window.wallpaperReveal()");
        revealStarted = Stopwatch.GetTimestamp();
        revealTimer.Start();
    }
    public void EndReveal() {
        revealTimer.Stop();
        Opacity = FinalOpacity;
        if (videoReady && browser.CoreWebView2 != null)
            browser.ExecuteScriptAsync("window.wallpaperSettle && window.wallpaperSettle()");
    }
    public void HideForApp() {
        wantedVisible = false;
        if (Visible || revealTimer.Enabled) EndReveal();
        // Keep the offscreen prewarm surface alive until the first video frame decodes.
        if (Visible && (videoReady || Bounds.X > -10000)) { Pause(); Hide(); }
    }
    protected override void Dispose(bool disposing) {
        if (disposing) { revealTimer.Dispose(); browser.Dispose(); fallback.Dispose(); }
        base.Dispose(disposing);
    }
}
