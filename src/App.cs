using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeskSeasons
{
    class HotkeyWindow : NativeWindow
    {
        public event Action<int> Pressed;
        public HotkeyWindow() { CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    class App : ApplicationContext
    {
        public Settings S;
        public World W;
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly HashSet<IntPtr> own = new HashSet<IntPtr>();
        readonly HotkeyWindow hk = new HotkeyWindow();
        Native.HookProc hookProc;
        IntPtr hook;
        bool grabbing;
        double last;
        float topTimer, seasonTimer;
        SettingsForm settingsForm;
        Icon trayIcon;
        Season iconSeason = (Season)(-1);
        int monitorsMode = -1;

        public App()
        {
            S = Settings.Load();
            float k = Math.Max(1f, Math.Min(2.5f, Screen.PrimaryScreen.Bounds.Height / 1080f));
            Art.Init(k);
            W = new World(S);
            W.K = k;
            BuildMonitors();

            tray.Text = "DeskSeasons";
            tray.Visible = true;
            tray.ContextMenuStrip = new ContextMenuStrip();
            tray.ContextMenuStrip.Opening += delegate { BuildMenu(); };
            tray.MouseDoubleClick += delegate (object o, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowSettings(); };
            BuildMenu();
            UpdateIcon();

            hk.Pressed += OnHotkey;
            Native.RegisterHotKey(hk.Handle, 1, 0x1 | 0x2 | 0x4000, (uint)Keys.S); // Ctrl+Alt+S показать/скрыть
            Native.RegisterHotKey(hk.Handle, 2, 0x1 | 0x2 | 0x4000, (uint)Keys.W); // Ctrl+Alt+W погода
            Native.RegisterHotKey(hk.Handle, 3, 0x1 | 0x2 | 0x4000, (uint)Keys.D); // Ctrl+Alt+D сезон

            hookProc = HookCallback;

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            ApplyAutostart();

            timer.Tick += Tick;
            ApplyAll();
            if (!S.Visible)
                tray.ShowBalloonTip(3000, "DeskSeasons", "Эффекты скрыты. Ctrl+Alt+S — показать.", ToolTipIcon.Info);
        }

        // ---------------- мониторы ----------------
        void BuildMonitors()
        {
            foreach (var m in W.Mons) if (m.O != null) { own.Remove(m.O.Handle); m.O.Close(); m.O.Dispose(); }
            var list = new List<Mon>();
            foreach (var sc in Screen.AllScreens)
            {
                if (S.Monitors == 1 && !sc.Primary) continue;
                var m = new Mon { B = sc.Bounds, W = sc.WorkingArea, Primary = sc.Primary };
                m.O = new Overlay(sc.Bounds);
                m.O.SetExcludeFromCapture(S.HideFromCapture);
                m.O.Show();
                own.Add(m.O.Handle);
                list.Add(m);
            }
            W.SetMonitors(list);
            monitorsMode = S.Monitors;
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            BuildMonitors();
        }

        // ---------------- главный цикл ----------------
        void Tick(object sender, EventArgs e)
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.05, Math.Max(0.001, now - last));
            last = now;

            seasonTimer += dt;
            if (seasonTimer > 30) { seasonTimer = 0; var cs = S.CurrentSeason(); if (cs != W.Sea) { W.SetSeason(cs); W.SS = S.Seasons[(int)cs]; UpdateIcon(); UpdateHook(); } }

            IntPtr fg = Native.GetForegroundWindow();
            bool hideAll = S.ExcludeHideAll && fg != IntPtr.Zero && S.IsExcluded(WindowTracker.ProcessName(fg));
            foreach (var m in W.Mons)
            {
                bool hidden = hideAll || (S.HideOnFullscreen && !own.Contains(fg) && WindowTracker.IsFullscreen(fg, m.B));
                if (hidden != m.Hidden)
                {
                    m.Hidden = hidden;
                    if (hidden) m.O.Hide(); else m.O.Show();
                }
            }

            var wins = WindowTracker.Enumerate(own, S);
            Native.POINT cp; Native.GetCursorPos(out cp);
            W.Update(dt, wins, new Point(cp.X, cp.Y), ModifierDown());

            foreach (var m in W.Mons)
            {
                if (m.Hidden) continue;
                m.O.BeginFrame();
                try { W.Render(m, m.O.G); } catch (Exception ex) { Program.Log(ex); }
                m.O.Present();
            }

            topTimer += dt;
            if (topTimer > 2) { topTimer = 0; foreach (var m in W.Mons) if (!m.Hidden) m.O.KeepOnTop(); }
        }

        bool ModifierDown()
        {
            int vk = S.Modifier == 1 ? Native.VK_LCONTROL : S.Modifier == 2 ? Native.VK_LSHIFT : Native.VK_LMENU;
            return Native.KeyDown(vk);
        }

        IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && S.Visible)
                {
                    int msg = wParam.ToInt32();
                    if (msg == Native.WM_LBUTTONDOWN && ModifierDown() && W.CanSnowball)
                    {
                        var info = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                        W.CursorX = info.pt.X; W.CursorY = info.pt.Y;
                        if (W.TryGrab(info.pt.X, info.pt.Y)) { grabbing = true; return new IntPtr(1); }
                    }
                    else if (msg == Native.WM_LBUTTONUP && grabbing)
                    {
                        grabbing = false;
                        W.ReleaseHeld();
                        return new IntPtr(1);
                    }
                }
            }
            catch (Exception ex) { Program.Log(ex); }
            return Native.CallNextHookEx(hook, nCode, wParam, lParam);
        }

        void OnHotkey(int id)
        {
            if (id == 1) ToggleVisible();
            else if (id == 2) { var ss = W.SS; ss.Weather = (ss.Weather + 1) % 3; Changed(); ShowTip(Settings.WeatherNames[(int)W.Sea][ss.Weather]); }
            else if (id == 3)
            {
                int cur = (int)W.Sea;
                S.SeasonMode = (cur + 1) % 4 + 1;
                Changed();
                ShowTip(Settings.SeasonNames[(int)W.Sea]);
            }
        }

        void ShowTip(string text) { tray.ShowBalloonTip(1500, "DeskSeasons", text, ToolTipIcon.None); }

        // ---------------- применение настроек ----------------
        public void Changed()
        {
            ApplyAll();
            S.Save();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshFromSettings();
        }

        public void ApplyAll()
        {
            if (S.Monitors != monitorsMode) BuildMonitors();
            var cs = S.CurrentSeason();
            if (cs != W.Sea) W.SetSeason(cs);
            W.SS = S.Seasons[(int)W.Sea];
            foreach (var m in W.Mons) m.O.SetExcludeFromCapture(S.HideFromCapture);
            timer.Interval = Math.Max(10, 1000 / Math.Max(10, S.Fps));
            if (S.Visible)
            {
                foreach (var m in W.Mons) if (!m.Hidden) m.O.Show();
                last = clock.Elapsed.TotalSeconds;
                timer.Start();
            }
            else
            {
                timer.Stop();             // программа «спит» и не тратит ресурсы
                foreach (var m in W.Mons) m.O.Hide();
            }
            ApplyAutostart();
            UpdateIcon();
            UpdateHook();
        }

        // Перехват мыши нужен только для снежков. Он работает в том же потоке, что и отрисовка,
        // поэтому без надобности не ставим — иначе при нагрузке начинает тормозить курсор во всей системе.
        void UpdateHook()
        {
            bool want = S.Visible && W.CanSnowball;
            if (want && hook == IntPtr.Zero)
                hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, hookProc, Native.GetModuleHandle(null), 0);
            else if (!want && hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
                if (grabbing) { grabbing = false; W.ReleaseHeld(); }
            }
        }

        void ToggleVisible() { S.Visible = !S.Visible; Changed(); }

        void UpdateIcon()
        {
            if (iconSeason == W.Sea && trayIcon != null) { tray.Text = TrayText(); return; }
            iconSeason = W.Sea;
            var old = trayIcon;
            trayIcon = Art.MakeIcon((int)W.Sea);
            tray.Icon = trayIcon;
            if (old != null) old.Dispose();
            tray.Text = TrayText();
        }

        string TrayText()
        {
            string t = "DeskSeasons — " + Settings.SeasonNames[(int)W.Sea] + (S.Visible ? "" : " (скрыто)");
            return t.Length > 63 ? t.Substring(0, 63) : t;
        }

        void ApplyAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (S.Autostart) key.SetValue("DeskSeasons", "\"" + Application.ExecutablePath + "\"");
                    else if (key.GetValue("DeskSeasons") != null) key.DeleteValue("DeskSeasons", false);
                }
            }
            catch { }
        }

        // ---------------- меню в трее ----------------
        void BuildMenu()
        {
            var menu = tray.ContextMenuStrip;
            menu.Items.Clear();
            var ss = W.SS; int si = (int)W.Sea;

            var head = new ToolStripMenuItem("DeskSeasons — " + Settings.SeasonNames[si]) { Enabled = false };
            menu.Items.Add(head);
            menu.Items.Add(Item(S.Visible ? "Скрыть эффекты\tCtrl+Alt+S" : "Показать эффекты\tCtrl+Alt+S", ToggleVisible));

            var season = new ToolStripMenuItem("Сезон");
            season.DropDownItems.Add(Check("Авто (по дате: " + Settings.SeasonNames[(int)Settings.SeasonByDate(DateTime.Now)] + ")", S.SeasonMode == 0, delegate { S.SeasonMode = 0; Changed(); }));
            for (int i = 0; i < 4; i++)
            {
                int ii = i;
                season.DropDownItems.Add(Check(Settings.SeasonNames[i], S.SeasonMode == i + 1, delegate { S.SeasonMode = ii + 1; Changed(); }));
            }
            menu.Items.Add(season);

            var weather = new ToolStripMenuItem("Погода");
            for (int i = 0; i < 3; i++)
            {
                int ii = i;
                weather.DropDownItems.Add(Check(Settings.WeatherNames[si][i], ss.Weather == i, delegate { ss.Weather = ii; Changed(); }));
            }
            menu.Items.Add(weather);

            var inten = new ToolStripMenuItem("Интенсивность");
            int[] lv = { 15, 35, 50, 75, 100 }; string[] ln = { "Чуть-чуть", "Слабо", "Средне", "Сильно", "Максимум" };
            for (int i = 0; i < lv.Length; i++)
            {
                int v = lv[i];
                inten.DropDownItems.Add(Check(ln[i], Math.Abs(ss.Intensity - v) < 8, delegate { ss.Intensity = v; Changed(); }));
            }
            menu.Items.Add(inten);

            var wind = new ToolStripMenuItem("Ветер");
            int[] wv = { -70, -30, 0, 30, 70 }; string[] wn = { "Сильный влево", "Влево", "Штиль", "Вправо", "Сильный вправо" };
            for (int i = 0; i < wv.Length; i++)
            {
                int v = wv[i];
                wind.DropDownItems.Add(Check(wn[i], Math.Abs(ss.Wind - v) < 15, delegate { ss.Wind = v; Changed(); }));
            }
            menu.Items.Add(wind);
            menu.Items.Add(new ToolStripSeparator());

            if (si != 2) menu.Items.Add(Check(Settings.PileNames[si], ss.WindowPiles, delegate { ss.WindowPiles = !ss.WindowPiles; Changed(); }));
            if (si != 2) menu.Items.Add(Check(Settings.GroundNames[si], ss.GroundPile, delegate { ss.GroundPile = !ss.GroundPile; Changed(); }));
            menu.Items.Add(Check(Settings.GarlandNames[si], ss.Garland, delegate { ss.Garland = !ss.Garland; Changed(); }));
            menu.Items.Add(Check(Settings.TreeNames[si], ss.Tree, delegate { ss.Tree = !ss.Tree; Changed(); }));
            menu.Items.Add(Check(Settings.CharacterNames[si] + " гуляет по экрану", ss.Character, delegate { ss.Character = !ss.Character; Changed(); }));
            if (si != 2) menu.Items.Add(Check(Settings.CleanerNames[si], ss.Cleaner, delegate { ss.Cleaner = !ss.Cleaner; Changed(); }));
            menu.Items.Add(Check(Settings.ExtraNames[si], ss.Extra, delegate { ss.Extra = !ss.Extra; Changed(); }));
            if (Settings.Extra2Names[si].Length > 0)
                menu.Items.Add(Check(Settings.Extra2Names[si], ss.Extra2, delegate { ss.Extra2 = !ss.Extra2; Changed(); }));
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add(Item("Позвать: " + Settings.CharacterNames[si], delegate { W.SummonWalker(); }));
            if (si != 2) menu.Items.Add(Item("Вызвать: " + Settings.CleanerNames[si], delegate { W.SummonCleaner(null); }));
            if (si == 0) menu.Items.Add(Item("Слепить снеговика", delegate { W.BuildSnowman(); }));
            menu.Items.Add(Item("Стряхнуть всё с окон и экрана", delegate { W.ClearAll(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Item("Настройки…", ShowSettings));
            menu.Items.Add(Item("Выход", ExitApp));
        }

        static ToolStripMenuItem Item(string text, Action a)
        {
            var it = new ToolStripMenuItem(text);
            it.Click += delegate { a(); };
            return it;
        }

        static ToolStripMenuItem Check(string text, bool on, Action a)
        {
            var it = Item(text, a);
            it.Checked = on;
            return it;
        }

        public void ShowSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(this);
            }
            settingsForm.Show();
            settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }

        public void ExitApp()
        {
            S.Save();
            timer.Stop();
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            for (int i = 1; i <= 3; i++) Native.UnregisterHotKey(hk.Handle, i);
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            tray.Visible = false;
            tray.Dispose();
            foreach (var m in W.Mons) if (m.O != null) m.O.Close();
            hk.DestroyHandle();
            ExitThread();
        }
    }
}
