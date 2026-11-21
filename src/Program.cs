using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("DeskSeasons")]
[assembly: System.Reflection.AssemblyProduct("DeskSeasons — сезонные эффекты на рабочем столе")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace DeskSeasons
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--export-icon") { Art.ExportIco(args[1]); return; }
            if (args.Length >= 3 && args[0] == "--render") { TestRender(int.Parse(args[1]), args[2], args.Length > 3 ? args[3] : ""); return; }

            bool created;
            using (var mutex = new Mutex(true, "DeskSeasons_SingleInstance_v1", out created))
            {
                if (!created)
                {
                    MessageBox.Show("DeskSeasons уже запущен — значок в области уведомлений рядом с часами.", "DeskSeasons",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); }
                catch { try { Native.SetProcessDPIAware(); } catch { } }
                Native.timeBeginPeriod(1);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => Log(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);
                var app = new App();
                if (Array.IndexOf(args, "--settings") >= 0) app.ShowSettings();
                Application.Run(app);
                Native.timeEndPeriod(1);
            }
        }

        // Отладка: просчитать несколько секунд сезона без окон и сохранить кадр в PNG
        static void TestRender(int season, string path, string opts)
        {
            Art.Init(1f);
            var s = new Settings();
            s.SeasonMode = season + 1;
            s.Seasons[season].Intensity = 100;
            foreach (var o in opts.Split(','))
            {
                if (o == "noextra") s.Seasons[season].Extra = false;
                if (o == "notree") s.Seasons[season].Tree = false;
                if (o == "noground") s.Seasons[season].GroundPile = false;
                if (o == "w1") s.Seasons[season].Weather = 1;
                if (o == "w2") s.Seasons[season].Weather = 2;
            }
            var w = new World(s);
            var m = new Mon { B = new System.Drawing.Rectangle(0, 0, 1200, 700), W = new System.Drawing.Rectangle(0, 0, 1200, 660), Primary = true };
            w.SetMonitors(new System.Collections.Generic.List<Mon> { m });
            var win = new WinInfo { H = new IntPtr(1), R = new System.Drawing.Rectangle(300, 300, 600, 250), Proc = "test.exe" };
            var wins = new System.Collections.Generic.List<WinInfo> { win };
            for (int i = 0; i < 30 * 25; i++)
            {
                w.Update(1f / 30, wins, new System.Drawing.Point(-100, -100), false);
                if (i == 300) w.SummonWalker();
                if (i == 400) w.SummonCleaner(null);
            }
            using (var bmp = new System.Drawing.Bitmap(1200, 700, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(255, 20, 25, 40));
                g.FillRectangle(System.Drawing.Brushes.WhiteSmoke, win.R);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                w.Render(m, g);
                bmp.Save(path);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 60; i++) { w.Update(1f / 30, wins, new System.Drawing.Point(-100, -100), false); g.Clear(System.Drawing.Color.Transparent); w.Render(m, g); }
                File.WriteAllText(path + ".txt", (sw.Elapsed.TotalMilliseconds / 60).ToString("F2") + " ms/frame, particles=" + w.P.Count);
            }
        }

        public static void Log(Exception ex)
        {
            if (ex == null) return;
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "DeskSeasons");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"), DateTime.Now + " " + ex + Environment.NewLine);
            }
            catch { }
        }
    }
}
