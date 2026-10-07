using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal static class SeamlessDemo {
    [STAThread] private static void Main(string[] args) {
        if (args.Length < 2) throw new ArgumentException("Usage: SeamlessDemo.exe <animation root> <log file>");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var overlay = new SeamlessOverlay(args[0], args[1]);
        var timer = new Timer { Interval = 2000 };
        var exit = new Timer { Interval = 18000 };
        overlay.Signal += action => {
            if (action == "complete" || action == "assetError" || action == "error") {
                exit.Stop();
                Application.ExitThread();
            }
        };
        timer.Tick += (s, e) => {
            timer.Stop();
            overlay.StartAnimation(new Rectangle(200, 120, 1280, 720), args.Length > 2 && args[2] == "--resume");
        };
        exit.Tick += (s, e) => { exit.Stop(); Application.ExitThread(); };
        overlay.Prewarm();
        timer.Start(); exit.Start();
        Application.Run();
        timer.Dispose(); exit.Dispose(); overlay.Close(); overlay.Dispose();
    }
}
