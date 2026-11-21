using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DeskSeasons
{
    class SettingsForm : Form
    {
        readonly App app;
        Settings S { get { return app.S; } }
        readonly List<Action> refreshers = new List<Action>();
        bool loading;
        ListBox exList;
        TabControl tabs;

        public SettingsForm(App a)
        {
            app = a;
            Text = "DeskSeasons — настройки";
            Icon = Art.MakeIcon(-1);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            Font = new Font("Segoe UI", 9f);
            Size = new Size(600, 680);
            MinimumSize = new Size(520, 500);
            StartPosition = FormStartPosition.CenterScreen;

            tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(GeneralTab());
            for (int i = 0; i < 4; i++) tabs.TabPages.Add(SeasonTab(i));
            tabs.TabPages.Add(ExcludeTab());
            Controls.Add(tabs);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 42, Padding = new Padding(6) };
            var close = new Button { Text = "Готово", Width = 100, Height = 28 };
            close.Click += delegate { Close(); };
            bottom.Controls.Add(close);
            var lbl = new Label { Text = "Изменения применяются сразу.", AutoSize = true, Padding = new Padding(0, 7, 10, 0), ForeColor = Color.Gray };
            bottom.Controls.Add(lbl);
            Controls.Add(bottom);

            tabs.SelectedIndex = 1 + (int)app.W.Sea;
            RefreshFromSettings();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            S.Save();
            base.OnFormClosing(e);
        }

        public void RefreshFromSettings()
        {
            loading = true;
            foreach (var r in refreshers) r();
            loading = false;
        }

        void Changed()
        {
            if (loading) return;
            app.ApplyAll();
        }

        // ---------- конструктор строк ----------
        static TableLayoutPanel NewTable()
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true, Padding = new Padding(12, 10, 24, 10) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
            return t;
        }

        static void Header(TableLayoutPanel t, string text)
        {
            var l = new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", 10f), Margin = new Padding(0, 12, 0, 4) };
            t.Controls.Add(l); t.SetColumnSpan(l, 3);
        }

        static void Note(TableLayoutPanel t, string text)
        {
            var l = new Label { Text = text, AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(500, 0), Margin = new Padding(0, 2, 0, 6) };
            t.Controls.Add(l); t.SetColumnSpan(l, 3);
        }

        void Slider(TableLayoutPanel t, string label, int min, int max, Func<int> get, Action<int> set, Func<int, string> fmt)
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 9, 0, 0) });
            var tb = new TrackBar { Minimum = min, Maximum = max, TickStyle = TickStyle.None, Dock = DockStyle.Fill, AutoSize = false, Height = 30 };
            var val = new Label { AutoSize = true, Margin = new Padding(0, 9, 0, 0) };
            tb.ValueChanged += delegate { val.Text = fmt(tb.Value); if (!loading) { set(tb.Value); Changed(); } };
            refreshers.Add(delegate { tb.Value = Math.Max(min, Math.Min(max, get())); val.Text = fmt(tb.Value); });
            t.Controls.Add(tb); t.Controls.Add(val);
        }

        CheckBox Check(TableLayoutPanel t, string text, Func<bool> get, Action<bool> set)
        {
            var cb = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0, 5, 0, 2) };
            cb.CheckedChanged += delegate { if (!loading) { set(cb.Checked); Changed(); } };
            refreshers.Add(delegate { cb.Checked = get(); });
            t.Controls.Add(cb); t.SetColumnSpan(cb, 3);
            return cb;
        }

        void Combo(TableLayoutPanel t, string label, string[] items, Func<int> get, Action<int> set)
        {
            t.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 7, 0, 0) });
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            c.Items.AddRange(items);
            c.SelectedIndexChanged += delegate { if (!loading && c.SelectedIndex >= 0) { set(c.SelectedIndex); Changed(); } };
            refreshers.Add(delegate { int v = get(); c.SelectedIndex = v >= 0 && v < items.Length ? v : 0; });
            t.Controls.Add(c); t.SetColumnSpan(c, 2);
        }

        static string Pct(int v) { return v + "%"; }

        // ---------- вкладки ----------
        TabPage GeneralTab()
        {
            var page = new TabPage("Общие");
            var t = NewTable();
            Header(t, "Сезон");
            Combo(t, "Какой сезон показывать", new[] { "Авто — по текущей дате", "Зима", "Весна", "Лето", "Осень" }, () => S.SeasonMode, v => S.SeasonMode = v);
            Note(t, "Авто: декабрь–февраль зима, март–май весна, июнь–август лето, сентябрь–ноябрь осень.");
            Check(t, "Показывать эффекты (снять — программа «спит»)", () => S.Visible, v => S.Visible = v);

            Header(t, "Экран");
            Combo(t, "Мониторы", new[] { "Все мониторы", "Только основной" }, () => S.Monitors, v => S.Monitors = v);
            Combo(t, "Плавность (кадров в секунду)", new[] { "20 — экономно", "30 — обычно", "45", "60 — максимально плавно" },
                () => S.Fps <= 20 ? 0 : S.Fps <= 30 ? 1 : S.Fps <= 45 ? 2 : 3, v => S.Fps = new[] { 20, 30, 45, 60 }[v]);
            Check(t, "Не показывать эффекты при демонстрации экрана и на скриншотах", () => S.HideFromCapture, v => S.HideFromCapture = v);
            Check(t, "Скрывать эффекты, когда открыто полноэкранное приложение (игры, видео)", () => S.HideOnFullscreen, v => S.HideOnFullscreen = v);

            Header(t, "Мышь");
            Check(t, "Сметать снег и листья, проводя курсором", () => S.BrushOnHover, v => S.BrushOnHover = v);
            Combo(t, "Клавиша для варежки и снежков", new[] { "Левый Alt", "Левый Ctrl", "Левый Shift" }, () => S.Modifier, v => S.Modifier = v);
            Note(t, "Зажмите клавишу и водите мышью — варежка сметает снег. Зимой: зажмите клавишу, нажмите на сугроб и ведите — " +
                    "катится снежок; отпустите в движении — бросок. Положите снежки друг на друга внизу экрана — получится снеговик.");

            Header(t, "Запуск");
            Check(t, "Запускать вместе с Windows", () => S.Autostart, v => S.Autostart = v);
            Check(t, "Портативный режим: хранить настройки рядом с программой", () => S.Portable, v => S.Portable = v);
            var path = new Label { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(500, 0) };
            refreshers.Add(delegate { path.Text = "Файл настроек: " + S.FilePath; });
            t.Controls.Add(path); t.SetColumnSpan(path, 3);
            Note(t, "Горячие клавиши: Ctrl+Alt+S — показать/скрыть, Ctrl+Alt+W — сменить погоду, Ctrl+Alt+D — следующий сезон.");

            var reset = new Button { Text = "Сбросить все настройки", AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
            reset.Click += delegate
            {
                if (MessageBox.Show(this, "Вернуть все настройки по умолчанию?", "DeskSeasons", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                var d = new Settings();
                S.SeasonMode = d.SeasonMode; S.Fps = d.Fps; S.Monitors = d.Monitors; S.HideFromCapture = d.HideFromCapture;
                S.HideOnFullscreen = d.HideOnFullscreen; S.BrushOnHover = d.BrushOnHover; S.Modifier = d.Modifier;
                S.ExcludeNoDecor = d.ExcludeNoDecor; S.ExcludeHideAll = d.ExcludeHideAll; S.Visible = true;
                for (int i = 0; i < 4; i++) S.Seasons[i] = d.Seasons[i];
                app.ApplyAll(); RefreshFromSettings();
            };
            t.Controls.Add(reset); t.SetColumnSpan(reset, 3);
            page.Controls.Add(t);
            return page;
        }

        TabPage SeasonTab(int si)
        {
            var page = new TabPage(Settings.SeasonNames[si]);
            var t = NewTable();
            Func<SeasonSettings> ss = () => S.Seasons[si];
            bool summer = si == 2;

            var now = new Button { Text = "Включить «" + Settings.SeasonNames[si] + "» сейчас", AutoSize = true };
            now.Click += delegate { S.SeasonMode = si + 1; app.ApplyAll(); RefreshFromSettings(); };
            t.Controls.Add(now); t.SetColumnSpan(now, 3);

            Header(t, "Погода");
            Combo(t, "Что падает с неба", Settings.WeatherNames[si], () => ss().Weather, v => ss().Weather = v);
            Slider(t, "Интенсивность", 0, 100, () => ss().Intensity, v => ss().Intensity = v, Pct);
            Slider(t, "Ветер (← влево / вправо →)", -100, 100, () => ss().Wind, v => ss().Wind = v, v => v == 0 ? "нет" : v.ToString());
            Slider(t, "Размер", 50, 200, () => ss().Size, v => ss().Size = v, Pct);
            Slider(t, "Скорость падения", 50, 200, () => ss().Speed, v => ss().Speed = v, Pct);

            Header(t, "Накопление");
            if (summer) Note(t, "Летом ничего не копится: дождь разбивается брызгами о края окон, пух улетает.");
            else
            {
                Check(t, Settings.PileNames[si], () => ss().WindowPiles, v => ss().WindowPiles = v);
                Slider(t, "Высота на окнах", 4, 40, () => ss().PileMax, v => ss().PileMax = v, v => v + " px");
                Check(t, Settings.GroundNames[si], () => ss().GroundPile, v => ss().GroundPile = v);
                Check(t, Settings.CleanerNames[si] + " приезжает, когда внизу много накопилось", () => ss().Cleaner, v => ss().Cleaner = v);
                Note(t, "Если резко дёрнуть окно — всё накопленное слетит с него.");
            }

            Header(t, "Украшения");
            Check(t, Settings.GarlandNames[si], () => ss().Garland, v => ss().Garland = v);
            Combo(t, "Где висит гирлянда", new[] { "На окнах", "Вдоль верха экрана", "Везде" }, () => ss().GarlandPlace, v => ss().GarlandPlace = v);
            Check(t, Settings.TreeNames[si] + " в углу экрана", () => ss().Tree, v => ss().Tree = v);
            Combo(t, "Угол", new[] { "Справа внизу", "Слева внизу" }, () => ss().TreeSide, v => ss().TreeSide = v);
            Check(t, Settings.ExtraNames[si], () => ss().Extra, v => ss().Extra = v);
            if (Settings.Extra2Names[si].Length > 0)
                Check(t, Settings.Extra2Names[si], () => ss().Extra2, v => ss().Extra2 = v);

            Header(t, "Персонаж: " + Settings.CharacterNames[si]);
            Check(t, Settings.CharacterNames[si] + " иногда гуляет по низу экрана", () => ss().Character, v => ss().Character = v);
            Combo(t, "Как часто", new[] { "Редко", "Иногда", "Часто" }, () => ss().CharacterFreq, v => ss().CharacterFreq = v);
            if (si == 0)
            {
                Check(t, "Йети и снеговики кидаются снежками (друг в друга и в курсор)", () => ss().Fights, v => ss().Fights = v);
                Note(t, "Йети лепит своих снеговиков и кидает снежки в чужих. Попадите в него своим снежком — он рассердится и ответит. " +
                        "Снеговик, в которого попали легонько, тоже ответит, а сильным броском его можно разбить. Наведите курсор — помашут рукой.");
            }
            if (si == 3) Note(t, "Ёжик собирает опавшие листья на спину.");

            var row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 10, 0, 0) };
            var call = new Button { Text = "Позвать: " + Settings.CharacterNames[si], AutoSize = true };
            call.Click += delegate { if ((int)app.W.Sea == si) app.W.SummonWalker(); };
            row.Controls.Add(call);
            if (!summer)
            {
                var cl = new Button { Text = "Вызвать: " + Settings.CleanerNames[si], AutoSize = true };
                cl.Click += delegate { if ((int)app.W.Sea == si) app.W.SummonCleaner(null); };
                row.Controls.Add(cl);
            }
            if (si == 0)
            {
                var sm = new Button { Text = "Слепить снеговика", AutoSize = true };
                sm.Click += delegate { if ((int)app.W.Sea == si) app.W.BuildSnowman(); };
                row.Controls.Add(sm);
            }
            t.Controls.Add(row); t.SetColumnSpan(row, 3);
            Note(t, "Кнопки работают, когда этот сезон сейчас включён.");
            page.Controls.Add(t);
            return page;
        }

        TabPage ExcludeTab()
        {
            var page = new TabPage("Исключения");
            var t = NewTable();
            Header(t, "Приложения-исключения");
            Note(t, "На окнах этих программ не будет гирлянд, сугробов, листьев и сосулек — снег просто пролетает мимо. " +
                    "Укажите имя процесса, например chrome.exe.");
            exList = new ListBox { Height = 200, Dock = DockStyle.Fill, IntegralHeight = false };
            refreshers.Add(delegate { exList.Items.Clear(); foreach (var e in S.Exclude) exList.Items.Add(e); });
            t.Controls.Add(exList); t.SetColumnSpan(exList, 3);

            var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var running = new Button { Text = "Добавить из запущенных…", AutoSize = true };
            running.Click += delegate { PickRunning(); };
            var browse = new Button { Text = "Выбрать .exe…", AutoSize = true };
            browse.Click += delegate
            {
                using (var d = new OpenFileDialog { Filter = "Программы (*.exe)|*.exe", Multiselect = true })
                    if (d.ShowDialog(this) == DialogResult.OK) foreach (var f in d.FileNames) AddExclude(Path.GetFileName(f));
            };
            var remove = new Button { Text = "Удалить", AutoSize = true };
            remove.Click += delegate
            {
                if (exList.SelectedItem == null) return;
                S.Exclude.RemoveAll(x => string.Equals(x, (string)exList.SelectedItem, StringComparison.OrdinalIgnoreCase));
                AfterExcludeChange();
            };
            row.Controls.Add(running); row.Controls.Add(browse); row.Controls.Add(remove);
            t.Controls.Add(row); t.SetColumnSpan(row, 3);

            var manual = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var tb = new TextBox { Width = 220 };
            var add = new Button { Text = "Добавить", AutoSize = true };
            add.Click += delegate { AddExclude(tb.Text); tb.Clear(); };
            tb.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { AddExclude(tb.Text); tb.Clear(); e.SuppressKeyPress = true; } };
            manual.Controls.Add(new Label { Text = "Имя процесса:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            manual.Controls.Add(tb); manual.Controls.Add(add);
            t.Controls.Add(manual); t.SetColumnSpan(manual, 3);

            Header(t, "Поведение");
            Check(t, "Не украшать окна приложений из списка", () => S.ExcludeNoDecor, v => S.ExcludeNoDecor = v);
            Check(t, "Полностью скрывать эффекты, пока приложение из списка активно", () => S.ExcludeHideAll, v => S.ExcludeHideAll = v);
            page.Controls.Add(t);
            return page;
        }

        void AddExclude(string name)
        {
            name = (name ?? "").Trim().ToLowerInvariant();
            if (name.Length == 0) return;
            if (!name.EndsWith(".exe")) name += ".exe";
            if (!S.IsExcluded(name)) S.Exclude.Add(name);
            AfterExcludeChange();
        }

        void AfterExcludeChange()
        {
            S.Save();
            app.ApplyAll();
            RefreshFromSettings();
        }

        void PickRunning()
        {
            var items = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero || string.IsNullOrEmpty(p.MainWindowTitle)) continue;
                    string n = p.ProcessName.ToLowerInvariant() + ".exe";
                    if (n == "deskseasons.exe" || !seen.Add(n)) continue;
                    items.Add(new KeyValuePair<string, string>(n, n + "  —  " + p.MainWindowTitle));
                }
                catch { }
                finally { p.Dispose(); }
            }
            items = items.OrderBy(x => x.Key).ToList();
            using (var f = new Form { Text = "Запущенные приложения", Size = new Size(520, 460), StartPosition = FormStartPosition.CenterParent, Font = Font, MinimizeBox = false, MaximizeBox = false })
            {
                var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
                foreach (var it in items) list.Items.Add(it.Value, S.IsExcluded(it.Key));
                var ok = new Button { Text = "Добавить отмеченные", Dock = DockStyle.Bottom, Height = 32, DialogResult = DialogResult.OK };
                f.Controls.Add(list); f.Controls.Add(ok);
                f.AcceptButton = ok;
                if (f.ShowDialog(this) != DialogResult.OK) return;
                for (int i = 0; i < items.Count; i++)
                {
                    bool on = list.GetItemChecked(i);
                    if (on && !S.IsExcluded(items[i].Key)) S.Exclude.Add(items[i].Key);
                    if (!on) S.Exclude.RemoveAll(x => string.Equals(x, items[i].Key, StringComparison.OrdinalIgnoreCase));
                }
                AfterExcludeChange();
            }
        }
    }
}
