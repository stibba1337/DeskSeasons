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
        public static readonly bool Profile = Environment.GetEnvironmentVariable("DESKSEASONS_PROFILE") == "1";
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--export-icon") { Art.ExportIco(args[1]); return; }
            if (args.Length == 2 && (args[0] == "--sheet" || args[0] == "--yeti"))
            {
                Art.Init(1f);
                var w = new World(new Settings());
                using (var bmp = new System.Drawing.Bitmap(args[0] == "--yeti" ? 1460 : 1700, args[0] == "--yeti" ? 1320 : 1400))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.Clear(System.Drawing.Color.FromArgb(255, 30, 36, 52));
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    if (args[0] == "--yeti") w.DrawYetiStrip(g, 2f); else w.DrawSheet(g, 2f);
                    bmp.Save(args[1]);
                }
                return;
            }
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
                if (Array.IndexOf(args, "--settings-general") >= 0) app.ShowSettings(0);
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
                if (o == "tree") s.Seasons[season].Tree = true;
                if (o == "noground") s.Seasons[season].GroundPile = false;
                if (o == "w1") s.Seasons[season].Weather = 1;
                if (o == "w2") s.Seasons[season].Weather = 2;
                if (o == "windR") s.Seasons[season].Wind = 100;
                if (o == "windL") s.Seasons[season].Wind = -100;
            }
            var w = new World(s);
            var m = new Mon { B = new System.Drawing.Rectangle(0, 0, 1200, 700), W = new System.Drawing.Rectangle(0, 0, 1200, 660), Primary = true };
            w.SetMonitors(new System.Collections.Generic.List<Mon> { m });
            var win = new WinInfo { H = new IntPtr(1), R = new System.Drawing.Rectangle(300, 300, 600, 250), Proc = "test.exe" };
            var wins = new System.Collections.Generic.List<WinInfo> { win };
            if (opts.Contains("jerk") || opts.Contains("fight")) { s.Seasons[season].Intensity = 35; TestSequence(w, m, win, wins, path, opts.Contains("jerk")); return; }
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
                int[] thirds = new int[3];
                foreach (var q in w.P) if (q.X >= 0 && q.X < 1200 && q.Y >= 0 && q.Y < 700) thirds[(int)(q.X / 400)]++;
                File.WriteAllText(path + ".txt", (sw.Elapsed.TotalMilliseconds / 60).ToString("F2") + " ms/frame, particles=" + w.P.Count +
                    ", left/mid/right=" + thirds[0] + "/" + thirds[1] + "/" + thirds[2]);
            }
        }

        // Отладка: раскадровка 3x2 — рывок окна или перестрелка снежками
        static void TestSequence(World w, Mon m, WinInfo win, System.Collections.Generic.List<WinInfo> wins, string path, bool jerk)
        {
            var cursor = new System.Drawing.Point(-100, -100);
            for (int i = 0; i < 600; i++) w.Update(1f / 30, wins, cursor, false);
            int[] shots = jerk ? new[] { 0, 2, 5, 10, 18, 40 } : new[] { 112, 120, 128, 140, 165, 200 };
            if (!jerk)
            {
                w.BuildSnowman();
                var sm = w.Snowmen[w.Snowmen.Count - 1]; sm.X = 250; sm.Cd = 1.2f;
                w.SummonWalker(); w.Walker.Dir = 1; w.Walker.X = 620;
                cursor = new System.Drawing.Point(1000, 200);
            }
            using (var sheet = new System.Drawing.Bitmap(1800, 700))
            using (var sg = System.Drawing.Graphics.FromImage(sheet))
            using (var bmp = new System.Drawing.Bitmap(1200, 700, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                int shot = 0;
                for (int f = 0; f <= shots[shots.Length - 1]; f++)
                {
                    if (jerk && f < 4) win.R = new System.Drawing.Rectangle(win.R.X + 70, win.R.Y, win.R.Width, win.R.Height); // резкий рывок вправо
                    if (!jerk && f == 30)
                    {
                        // игрок попадает снежком в Йети
                        float tx = w.Walker.X + w.Walker.Dir * 27 * (w.Walker.Wave ? 0 : 1);
                        var b = new Ball { X = 1000, Y = 200, R = 9, VX = (tx - 1000) / 0.5f, VY = 0 };
                        b.VY = (615 - 200 - 0.5f * 1500 * 0.25f) / 0.5f;
                        w.Balls.Add(b);
                    }
                    w.Update(1f / 30, wins, cursor, false);
                    if (f != shots[shot]) continue;
                    g.Clear(System.Drawing.Color.FromArgb(255, 20, 25, 40));
                    g.FillRectangle(System.Drawing.Brushes.WhiteSmoke, win.R);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    w.Render(m, g);
                    if (!jerk) g.FillEllipse(System.Drawing.Brushes.Red, cursor.X - 4, cursor.Y - 4, 8, 8);
                    g.DrawString("кадр +" + f, new System.Drawing.Font("Segoe UI", 22), System.Drawing.Brushes.Yellow, 10, 10);
                    if (jerk) sg.DrawImage(bmp, (shot % 3) * 600, (shot / 3) * 350, 600, 350); else sg.DrawImage(bmp, new System.Drawing.Rectangle((shot % 3) * 600, (shot / 3) * 350, 600, 350), new System.Drawing.Rectangle(180, 120, 960, 560), System.Drawing.GraphicsUnit.Pixel);
                    shot++;
                }
                sheet.Save(path);
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
