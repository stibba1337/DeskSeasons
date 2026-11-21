using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DeskSeasons
{
    public enum Season { Winter = 0, Spring = 1, Summer = 2, Autumn = 3 }

    public class SeasonSettings
    {
        public int Weather;
        public int Intensity = 50;
        public int Wind;
        public int Size = 100;
        public int Speed = 100;
        public bool WindowPiles = true;
        public int PileMax = 16;
        public bool GroundPile = true;
        public bool Garland = true;
        public int GarlandPlace;      // 0 окна, 1 верх экрана, 2 везде
        public bool Tree = false;
        public int TreeSide;          // 0 справа, 1 слева
        public bool Character = true;
        public int CharacterFreq = 1; // 0 редко, 1 иногда, 2 часто
        public bool Cleaner = true;
        public bool Extra = true;
        public bool Extra2 = true;
    }

    public class Settings
    {
        public int SeasonMode;        // 0 авто, 1..4 = зима..осень
        public bool Visible = true;
        public int Fps = 30;
        public int Monitors;          // 0 все, 1 основной
        public bool HideFromCapture = true;
        public bool HideOnFullscreen = true;
        public bool BrushOnHover = true;
        public int Modifier;          // 0 левый Alt, 1 левый Ctrl, 2 левый Shift
        public bool Autostart;
        public bool Portable;
        public List<string> Exclude = new List<string>();
        public bool ExcludeNoDecor = true;
        public bool ExcludeHideAll;
        public SeasonSettings[] Seasons = new SeasonSettings[4];

        public static readonly string[] SeasonNames = { "Зима", "Весна", "Лето", "Осень" };
        public static readonly string[][] WeatherNames =
        {
            new[] { "Снегопад", "Лёгкий снег", "Метель" },
            new[] { "Цветение сакуры", "Весенний дождь", "Лепестки и дождь" },
            new[] { "Солнечно (пух одуванчиков)", "Летний дождь", "Гроза" },
            new[] { "Листопад", "Осенний дождь", "Листья и дождь" }
        };
        public static readonly string[] PileNames = { "Сугробы на окнах", "Лепестки на окнах", "(летом не копится)", "Листья на окнах" };
        public static readonly string[] GroundNames = { "Снег внизу экрана", "Лепестки внизу экрана", "(летом не копится)", "Листва внизу экрана" };
        public static readonly string[] GarlandNames = { "Новогодняя гирлянда", "Цветочная гирлянда", "Флажки", "Тёплая гирлянда с листьями" };
        public static readonly string[] TreeNames = { "Ёлка с подарками", "Цветущая сакура", "Подсолнухи", "Клён и тыквы" };
        public static readonly string[] CharacterNames = { "Йети", "Зайчик", "Котик", "Ёжик" };
        public static readonly string[] CleanerNames = { "Снегоуборщик", "Газонокосилка", "", "Листоуборщик" };
        public static readonly string[] ExtraNames = { "Снежки и снеговики (клавиша + мышь)", "Бабочки", "Бабочки", "Туман внизу экрана" };
        public static readonly string[] Extra2Names = { "Сосульки на окнах", "Цветы и трава внизу", "Вспышки молний в грозу", "" };

        public Settings()
        {
            for (int i = 0; i < 4; i++) Seasons[i] = new SeasonSettings();
            Seasons[2].WindowPiles = false; Seasons[2].GroundPile = false; Seasons[2].Cleaner = false;
            Seasons[2].Intensity = 35;
            Seasons[3].Intensity = 40;
            Seasons[3].Extra = false; // туман по умолчанию выключен
            Seasons[1].Intensity = 45;
        }

        public Season CurrentSeason()
        {
            if (SeasonMode >= 1 && SeasonMode <= 4) return (Season)(SeasonMode - 1);
            return SeasonByDate(DateTime.Now);
        }

        public static Season SeasonByDate(DateTime d)
        {
            int m = d.Month;
            if (m == 12 || m <= 2) return Season.Winter;
            if (m <= 5) return Season.Spring;
            if (m <= 8) return Season.Summer;
            return Season.Autumn;
        }

        public bool IsExcluded(string proc)
        {
            if (string.IsNullOrEmpty(proc)) return false;
            for (int i = 0; i < Exclude.Count; i++)
                if (string.Equals(Exclude[i], proc, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ---------- хранение ----------
        public static string PortablePath
        {
            get { return Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "DeskSeasons.ini"); }
        }
        public static string AppDataPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskSeasons", "settings.ini"); }
        }
        public string FilePath { get { return Portable ? PortablePath : AppDataPath; } }

        public static Settings Load()
        {
            var s = new Settings();
            string path = File.Exists(PortablePath) ? PortablePath : AppDataPath;
            s.Portable = path == PortablePath;
            if (!File.Exists(path)) return s;
            try
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                s.SeasonMode = I(d, "SeasonMode", s.SeasonMode, 0, 4);
                s.Visible = B(d, "Visible", s.Visible);
                s.Fps = I(d, "Fps", s.Fps, 10, 60);
                s.Monitors = I(d, "Monitors", s.Monitors, 0, 1);
                s.HideFromCapture = B(d, "HideFromCapture", s.HideFromCapture);
                s.HideOnFullscreen = B(d, "HideOnFullscreen", s.HideOnFullscreen);
                s.BrushOnHover = B(d, "BrushOnHover", s.BrushOnHover);
                s.Modifier = I(d, "Modifier", s.Modifier, 0, 2);
                s.Autostart = B(d, "Autostart", s.Autostart);
                s.ExcludeNoDecor = B(d, "ExcludeNoDecor", s.ExcludeNoDecor);
                s.ExcludeHideAll = B(d, "ExcludeHideAll", s.ExcludeHideAll);
                string ex;
                if (d.TryGetValue("Exclude", out ex))
                    foreach (var p in ex.Split('|'))
                        if (p.Trim().Length > 0) s.Exclude.Add(p.Trim());
                for (int i = 0; i < 4; i++)
                {
                    var ss = s.Seasons[i];
                    string k = "S" + i + ".";
                    ss.Weather = I(d, k + "Weather", ss.Weather, 0, 2);
                    ss.Intensity = I(d, k + "Intensity", ss.Intensity, 0, 100);
                    ss.Wind = I(d, k + "Wind", ss.Wind, -100, 100);
                    ss.Size = I(d, k + "Size", ss.Size, 50, 200);
                    ss.Speed = I(d, k + "Speed", ss.Speed, 50, 200);
                    ss.WindowPiles = B(d, k + "WindowPiles", ss.WindowPiles);
                    ss.PileMax = I(d, k + "PileMax", ss.PileMax, 4, 40);
                    ss.GroundPile = B(d, k + "GroundPile", ss.GroundPile);
                    ss.Garland = B(d, k + "Garland", ss.Garland);
                    ss.GarlandPlace = I(d, k + "GarlandPlace", ss.GarlandPlace, 0, 2);
                    ss.Tree = B(d, k + "Tree", ss.Tree);
                    ss.TreeSide = I(d, k + "TreeSide", ss.TreeSide, 0, 1);
                    ss.Character = B(d, k + "Character", ss.Character);
                    ss.CharacterFreq = I(d, k + "CharacterFreq", ss.CharacterFreq, 0, 2);
                    ss.Cleaner = B(d, k + "Cleaner", ss.Cleaner);
                    ss.Extra = B(d, k + "Extra", ss.Extra);
                    ss.Extra2 = B(d, k + "Extra2", ss.Extra2);
                }
                // Файлы первой версии: туман и деревья тогда были включены по умолчанию — выключаем один раз
                if (I(d, "Version", 1, 1, 100) < 2)
                {
                    for (int i = 0; i < 4; i++) s.Seasons[i].Tree = false;
                    s.Seasons[3].Extra = false;
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("; Настройки DeskSeasons");
                W(sb, "Version", 2);
                W(sb, "SeasonMode", SeasonMode); W(sb, "Visible", Visible); W(sb, "Fps", Fps); W(sb, "Monitors", Monitors);
                W(sb, "HideFromCapture", HideFromCapture); W(sb, "HideOnFullscreen", HideOnFullscreen);
                W(sb, "BrushOnHover", BrushOnHover); W(sb, "Modifier", Modifier); W(sb, "Autostart", Autostart);
                W(sb, "ExcludeNoDecor", ExcludeNoDecor); W(sb, "ExcludeHideAll", ExcludeHideAll);
                sb.AppendLine("Exclude=" + string.Join("|", Exclude.ToArray()));
                for (int i = 0; i < 4; i++)
                {
                    var ss = Seasons[i]; string k = "S" + i + ".";
                    W(sb, k + "Weather", ss.Weather); W(sb, k + "Intensity", ss.Intensity); W(sb, k + "Wind", ss.Wind);
                    W(sb, k + "Size", ss.Size); W(sb, k + "Speed", ss.Speed); W(sb, k + "WindowPiles", ss.WindowPiles);
                    W(sb, k + "PileMax", ss.PileMax); W(sb, k + "GroundPile", ss.GroundPile); W(sb, k + "Garland", ss.Garland);
                    W(sb, k + "GarlandPlace", ss.GarlandPlace); W(sb, k + "Tree", ss.Tree); W(sb, k + "TreeSide", ss.TreeSide);
                    W(sb, k + "Character", ss.Character); W(sb, k + "CharacterFreq", ss.CharacterFreq);
                    W(sb, k + "Cleaner", ss.Cleaner); W(sb, k + "Extra", ss.Extra); W(sb, k + "Extra2", ss.Extra2);
                }
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                // при смене режима удаляем «чужой» файл, чтобы при запуске читался правильный
                string other = Portable ? AppDataPath : PortablePath;
                if (!Portable && File.Exists(other)) File.Delete(other);
            }
            catch { }
        }

        static void W(StringBuilder sb, string k, int v) { sb.AppendLine(k + "=" + v.ToString(CultureInfo.InvariantCulture)); }
        static void W(StringBuilder sb, string k, bool v) { sb.AppendLine(k + "=" + (v ? "1" : "0")); }
        static int I(Dictionary<string, string> d, string k, int def, int min, int max)
        {
            string v; int r;
            if (d.TryGetValue(k, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r))
                return Math.Max(min, Math.Min(max, r));
            return def;
        }
        static bool B(Dictionary<string, string> d, string k, bool def)
        {
            string v;
            if (d.TryGetValue(k, out v)) return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
            return def;
        }
    }
}
