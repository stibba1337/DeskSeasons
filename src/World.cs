using System;
using System.Collections.Generic;
using System.Drawing;

namespace DeskSeasons
{
    static class PK { public const byte Snow = 0, Rain = 1, Leaf = 2, Petal = 3, Fluff = 4, Splash = 5; }

    class Particle
    {
        public float X, Y, VX, VY, TVY, Size, Rot, VRot, Phase, Sway, Life;
        public byte Kind, Color, Shape;
    }

    class Item { public float X, Y, Rot, Size; public byte Kind, Color, Shape; }

    // Список лежащих листьев/лепестков с номером версии: слой перерисовывается только при изменениях
    class ItemList : List<Item>
    {
        public int Ver, Removals;
        public new void Add(Item it) { base.Add(it); Ver++; }
        public new void RemoveAt(int i) { base.RemoveAt(i); Ver++; Removals++; }
        public new void Clear() { if (Count > 0) { Ver++; Removals++; } base.Clear(); }
        public new int RemoveAll(Predicate<Item> m) { int n = base.RemoveAll(m); if (n > 0) { Ver++; Removals++; } return n; }
    }

    // Кэш-картинка со всеми лежащими предметами одной кромки
    class ItemLayer
    {
        public Bitmap Bmp; public int Ver = -1, Removals = -1, Count, Pad, MaxH;
        public void Dispose() { if (Bmp != null) { Bmp.Dispose(); Bmp = null; } }
    }

    class Pile
    {
        public IntPtr H;
        public Rectangle R, Prev;
        public float[] Hh = new float[0];
        public ItemList Items = new ItemList();
        public List<int> Segs = new List<int>(); // пары x0,x1 видимых участков верхней кромки
        public ItemLayer Layer = new ItemLayer();
        public bool Decor, Seen;
        public float VX, VY;
    }

    class Mon
    {
        public Rectangle B, W;
        public Overlay O;
        public float[] Gh;
        public ItemList Items = new ItemList();
        public bool Hidden, Primary;
        public ItemLayer Layer = new ItemLayer();
        public float[] FogX = new float[4];
        public float Fill;
    }

    class Snowman
    {
        public Mon M; public float X;
        public List<float> Balls = new List<float>();
        public int Hat; public int Scarf;
        public float Cd = 3, ThrowT;   // перезарядка и анимация броска
        public int Anger;              // сколько раз ещё ответит в курсор после попадания
        public bool Complete { get { return Balls.Count >= 3; } }
    }

    // Owner: 0 — игрок, 1 — Йети, 2 — снеговик. Splat — летит «в экран» и оставляет кляксу в точке прицела.
    class Ball
    {
        public float X, Y, VX, VY, R, Age, Flight;
        public bool FromYeti, Splat;
        public int Owner;
        public Snowman From;
    }

    // Клякса от снежка на «стекле» экрана: сползает и тает
    class Splat { public float X, Y, R, Life, Drip; public int Seed; }

    class Walker
    {
        public Mon M; public float X, Y; public int Dir; public int State; public float T, Anim;
        public Snowman Target, Own; public bool Built; public int Throws, Carry;
        public bool AimCursor, Wave; public int Anger, CursorThrows; public float ThrowCd;
        public List<Snowman> Thrown = new List<Snowman>();
    }

    class Cleaner { public Mon M; public float X, Load, Anim; }

    class Butterfly
    {
        public float X, Y, VX, VY, TX, TY, Phase, SitX, SitT; public int C; public Pile Sit; public bool ToSit;
    }

    class Bolt { public Mon M; public List<PointF> Pts = new List<PointF>(); public float Life; }

    // Вся симуляция: частицы, накопление на окнах и внизу экрана, персонажи, техника.
    partial class World
    {
        public const float CW = 4f;
        public Settings S;
        public Season Sea;
        public SeasonSettings SS;
        public float T, K = 1f;
        public List<Mon> Mons = new List<Mon>();
        public Dictionary<IntPtr, Pile> Piles = new Dictionary<IntPtr, Pile>();
        public List<Pile> Z = new List<Pile>();
        public List<Particle> P = new List<Particle>();
        public List<Snowman> Snowmen = new List<Snowman>();
        public List<Ball> Balls = new List<Ball>();
        public List<Splat> Splats = new List<Splat>();
        public Ball Held;
        public Walker Walker;
        public Cleaner Cleaner;
        public List<Butterfly> Flies = new List<Butterfly>();
        public Bolt Bolt;
        public float CursorX, CursorY, CurVX, CurVY;
        public bool Modifier;
        readonly Random rnd = new Random();
        float spawnAcc, treeAcc, nextWalker, nextBolt, fillCheck;
        const int MaxParticles = 2200;

        public World(Settings s) { S = s; Sea = s.CurrentSeason(); SS = s.Seasons[(int)Sea]; ScheduleWalker(true); }

        float R01() { return (float)rnd.NextDouble(); }
        float Rng(float a, float b) { return a + (b - a) * (float)rnd.NextDouble(); }
        public static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        public void SetMonitors(List<Mon> mons)
        {
            Mons = mons;
            foreach (var m in Mons)
            {
                m.Gh = new float[m.W.Width / (int)CW + 2];
                for (int i = 0; i < 4; i++) m.FogX[i] = m.W.Left + R01() * m.W.Width;
            }
            Snowmen.Clear(); Walker = null; Cleaner = null; Balls.Clear(); Held = null; Flies.Clear();
        }

        public void SetSeason(Season s)
        {
            Sea = s; SS = S.Seasons[(int)s];
            P.Clear(); Piles.Clear(); Z.Clear(); Snowmen.Clear(); Balls.Clear(); Splats.Clear(); Held = null;
            Walker = null; Cleaner = null; Flies.Clear(); Bolt = null;
            foreach (var m in Mons) { Array.Clear(m.Gh, 0, m.Gh.Length); m.Items.Clear(); }
            ScheduleWalker(true);
        }

        public void ClearAll()
        {
            foreach (var p in Piles.Values) DropPile(p, 0, 120);
            foreach (var m in Mons) { Array.Clear(m.Gh, 0, m.Gh.Length); m.Items.Clear(); }
            foreach (var sm in Snowmen) BurstSnowman(sm);
            Snowmen.Clear();
        }

        void ScheduleWalker(bool first)
        {
            float[] mins = { 360, 120, 40 }, maxs = { 600, 300, 90 };
            int f = SS == null ? 1 : SS.CharacterFreq;
            nextWalker = T + (first ? Rng(15, 40) : Rng(mins[f], maxs[f]));
        }

        bool AccumSeason { get { return Sea != Season.Summer; } }
        bool SnowSeason { get { return Sea == Season.Winter; } }
        public bool CanSnowball { get { return Sea == Season.Winter && SS.Extra; } }
        public bool Fights { get { return Sea == Season.Winter && SS.Fights; } }
        float PileMax { get { return SS.PileMax * K * (Sea == Season.Winter ? 1f : 0.7f); } }
        float GroundMax { get { return SS.PileMax * K * (Sea == Season.Winter ? 2.3f : 1.4f); } }
        float Wind
        {
            get
            {
                float w = SS.Wind / 100f * 140f * K;
                float gust = (float)(Math.Sin(T * 0.31) * 0.35 + Math.Sin(T * 0.113 + 1) * 0.4);
                w = w * (0.8f + 0.4f * gust) + gust * 18f * K;
                int wt = SS.Weather;
                if (Sea == Season.Winter && wt == 2) w += (SS.Wind >= 0 ? 1 : -1) * 230f * K * (0.8f + 0.4f * gust);
                if (Sea == Season.Summer && wt == 2) w += (SS.Wind >= 0 ? 1 : -1) * 90f * K;
                if (Sea == Season.Autumn && wt == 2) w += (SS.Wind >= 0 ? 1 : -1) * 70f * K;
                return w;
            }
        }

        // ---------------- главный шаг ----------------
        public void Update(float dt, List<WinInfo> wins, Point cursor, bool modifier)
        {
            T += dt;
            float ncx = cursor.X, ncy = cursor.Y;
            float ivx = (ncx - CursorX) / dt, ivy = (ncy - CursorY) / dt;
            if (Math.Abs(ivx) > 20000 || Math.Abs(ivy) > 20000) { ivx = 0; ivy = 0; }
            CurVX = CurVX * 0.55f + ivx * 0.45f; CurVY = CurVY * 0.55f + ivy * 0.45f;
            CursorX = ncx; CursorY = ncy; Modifier = modifier;

            UpdatePiles(dt, wins);
            Spawn(dt);
            UpdateParticles(dt);
            if (AccumSeason) RelaxAll();
            if (AccumSeason && (S.BrushOnHover || modifier)) BrushAt(CursorX, CursorY, modifier);
            UpdateBalls(dt);
            UpdateSnowmen(dt);
            UpdateSplats(dt);
            UpdateWalker(dt);
            UpdateCleaner(dt);
            UpdateFlies(dt);
            UpdateBolt(dt);
            foreach (var m in Mons)
                for (int i = 0; i < 4; i++)
                {
                    m.FogX[i] += (8f + i * 4f) * K * dt * (i % 2 == 0 ? 1 : -1) + Wind * 0.05f * dt;
                    float span = m.W.Width + 900 * K;
                    if (m.FogX[i] > m.W.Right + 450 * K) m.FogX[i] -= span;
                    if (m.FogX[i] < m.W.Left - 450 * K) m.FogX[i] += span;
                }
        }

        // ---------------- окна и сугробы на них ----------------
        void UpdatePiles(float dt, List<WinInfo> wins)
        {
            foreach (var p in Piles.Values) p.Seen = false;
            Z.Clear();
            foreach (var w in wins)
            {
                Pile p;
                if (!Piles.TryGetValue(w.H, out p))
                {
                    p = new Pile { H = w.H, R = w.R, Prev = w.R };
                    p.Hh = new float[Math.Max(1, w.R.Width / (int)CW + 1)];
                    Piles[w.H] = p;
                }
                p.Prev = p.R; p.R = w.R; p.Seen = true;
                p.Decor = !(w.Excluded && S.ExcludeNoDecor);
                Z.Add(p);

                // Снег держится, пока окно ведут плавно. Слетает от рывка — резкого изменения скорости:
                // старт с места, резкая остановка, тряска туда-сюда. Чем сильнее рывок, тем больше слетает.
                float vx = (p.R.X - p.Prev.X) / dt, vy = (p.R.Y - p.Prev.Y) / dt;
                float dvx = vx - p.VX, dvy = vy - p.VY;
                float jerk = (float)Math.Sqrt(dvx * dvx + dvy * dvy);
                if (p.R.Width != p.Prev.Width) Resize(p);
                if (!p.Decor && HasStuff(p)) DropPile(p, 0, 200);
                else if (jerk > 1300 * K && HasStuff(p))
                    ShakeOff(p, Math.Min(1f, 0.3f + (jerk - 1300 * K) / (2500 * K)), p.VX, p.VY);
                p.VX = vx; p.VY = vy;
            }
            // исчезнувшие окна (закрыты/свернуты) — их снег падает вниз
            var gone = new List<IntPtr>();
            foreach (var kv in Piles)
                if (!kv.Value.Seen) { DropPile(kv.Value, 0, 120); kv.Value.Layer.Dispose(); gone.Add(kv.Key); }
            foreach (var h in gone) Piles.Remove(h);

            // видимые участки верхних кромок с учётом перекрытия окнами выше
            var tmp = new List<int>();
            for (int i = 0; i < Z.Count; i++)
            {
                var p = Z[i];
                p.Segs.Clear();
                p.Segs.Add(p.R.Left); p.Segs.Add(p.R.Right);
                int y = p.R.Top - 1;
                for (int j = 0; j < i && p.Segs.Count > 0; j++)
                {
                    var o = Z[j].R;
                    if (o.Top > y || o.Bottom <= y) continue;
                    tmp.Clear();
                    for (int s = 0; s < p.Segs.Count; s += 2)
                    {
                        int a = p.Segs[s], b = p.Segs[s + 1];
                        if (o.Right <= a || o.Left >= b) { tmp.Add(a); tmp.Add(b); continue; }
                        if (o.Left > a) { tmp.Add(a); tmp.Add(o.Left); }
                        if (o.Right < b) { tmp.Add(o.Right); tmp.Add(b); }
                    }
                    p.Segs.Clear(); p.Segs.AddRange(tmp);
                }
                // кромка должна быть на каком-то мониторе
                if (p.Segs.Count > 0 && !OnAnyMonitor(p.R.Top)) p.Segs.Clear();
            }
        }

        bool OnAnyMonitor(int y)
        {
            foreach (var m in Mons) if (y > m.B.Top + 2 && y < m.B.Bottom) return true;
            return false;
        }

        static bool HasStuff(Pile p)
        {
            if (p.Items.Count > 0) return true;
            for (int i = 0; i < p.Hh.Length; i++) if (p.Hh[i] > 0.5f) return true;
            return false;
        }

        void Resize(Pile p)
        {
            var n = new float[Math.Max(1, p.R.Width / (int)CW + 1)];
            Array.Copy(p.Hh, n, Math.Min(n.Length, p.Hh.Length));
            p.Hh = n;
            p.Items.RemoveAll(it => it.X > p.R.Width);
        }

        public static bool InSegs(Pile p, float x)
        {
            for (int s = 0; s < p.Segs.Count; s += 2)
                if (x >= p.Segs[s] && x < p.Segs[s + 1]) return true;
            return false;
        }

        float Limit(int col, int n, float max)
        {
            int e = Math.Min(col, n - 1 - col);
            return max * Math.Min(1f, (e + 0.5f) / 4f);
        }

        void DropPile(Pile p, float vx, int cap)
        {
            int made = 0;
            bool vis = p.Segs.Count > 0;
            if (vis && SnowSeason)
                for (int c = 0; c < p.Hh.Length && made < cap; c += 2)
                {
                    float h = p.Hh[c];
                    float x = p.R.Left + c * CW;
                    if (h > 1.5f * K && InSegs(p, x))
                    {
                        int cnt = Math.Min(3, (int)(h / (3 * K)) + 1);
                        for (int k = 0; k < cnt; k++, made++)
                            AddSnowPuff(x + Rng(-2, 2), p.R.Top - R01() * h, vx * 0.35f + Rng(-30, 30) * K, -Rng(0, 60) * K);
                    }
                }
            if (vis)
                foreach (var it in p.Items)
                    if (made++ < cap) ItemToParticle(it, p.R.Left + it.X, p.R.Top - it.Y, vx * 0.35f, -Rng(0, 60) * K);
            Array.Clear(p.Hh, 0, p.Hh.Length);
            p.Items.Clear();
        }

        // Стряхивание после рывка. Снег берётся там, где он лежал в прошлом кадре (p.Prev и прошлые видимые участки),
        // и сохраняет прежнюю скорость окна — инерция: окно уехало, а снег остался и ссыпается комьями.
        void ShakeOff(Pile p, float frac, float vx, float vy)
        {
            if (p.Segs.Count == 0) { Array.Clear(p.Hh, 0, p.Hh.Length); p.Items.Clear(); return; }
            var r = p.Prev;
            int made = 0;
            if (SnowSeason)
                for (int c = 0; c < p.Hh.Length; c++)
                {
                    float h = p.Hh[c];
                    if (h < 0.5f) continue;
                    float take = Math.Min(h, h * frac * Rng(0.75f, 1.25f));
                    p.Hh[c] -= take;
                    float x = r.Left + c * CW;
                    if (!InSegs(p, x)) continue;   // видимые участки ещё прошлого кадра — в тех же координатах
                    // крупный ком примерно на каждые 3 px снятого снега + немного пыли
                    if (made < 260 && R01() < take / (2.2f * K))
                    {
                        made++;
                        var q = NewParticle(PK.Snow, x + Rng(-2, 2), r.Top - R01() * h);
                        if (q != null)
                        {
                            q.Size = Rng(2.6f, 5.6f) * K;
                            q.Shape = 1;                            // ком: падает с ускорением, а не парит как снежинка
                            q.TVY = Rng(500, 700) * K;
                            q.VX = vx * Rng(0.7f, 1f) + Rng(-25, 25) * K;
                            q.VY = vy * 0.8f - Rng(0, 70) * K;
                        }
                    }
                    if (made < 260 && R01() < take / (5f * K))
                    {
                        made++;
                        AddSnowPuff(x, r.Top - R01() * h, vx * 0.5f + Rng(-50, 50) * K, -Rng(20, 110) * K);
                    }
                }
            else
            {
                for (int c = 0; c < p.Hh.Length; c++) p.Hh[c] *= 1 - frac;
                for (int i = p.Items.Count - 1; i >= 0; i--)
                {
                    if (R01() > frac) continue;
                    var it = p.Items[i];
                    p.Items.RemoveAt(i);
                    if (made++ < 200 && InSegs(p, r.Left + it.X))
                        ItemToParticle(it, r.Left + it.X, r.Top - it.Y, vx * 0.8f, vy * 0.8f - Rng(20, 90) * K);
                }
            }
        }

        void ItemToParticle(Item it, float x, float y, float vx, float vy)
        {
            var q = NewParticle(it.Kind, x, y);
            if (q == null) return;
            q.Size = it.Size; q.Rot = it.Rot; q.Color = it.Color; q.Shape = it.Shape;
            q.VX = vx + Rng(-20, 20) * K; q.VY = vy;
        }

        // ---------------- рождение частиц ----------------
        void Spawn(float dt)
        {
            float f = SS.Intensity / 50f;
            int w = SS.Weather;
            float main = 0, rain = 0; byte mk = PK.Snow;
            switch (Sea)
            {
                case Season.Winter: mk = PK.Snow; main = w == 0 ? 38 : w == 1 ? 11 : 120; break;
                case Season.Spring: mk = PK.Petal; main = w == 0 ? 14 : w == 1 ? 0 : 9; rain = w == 1 ? 150 : w == 2 ? 75 : 0; break;
                case Season.Summer: mk = PK.Fluff; main = w == 0 ? 3 : 0; rain = w == 1 ? 160 : w == 2 ? 280 : 0; break;
                case Season.Autumn: mk = PK.Leaf; main = w == 0 ? 7 : w == 1 ? 0 : 6; rain = w == 1 ? 150 : w == 2 ? 85 : 0; break;
            }
            float wind = Wind;
            foreach (var m in Mons)
            {
                if (m.Hidden) continue;
                float widthK = m.B.Width / 1000f;
                if (main > 0) spawnAcc += main * f * widthK * dt * SideFactor(m, mk, wind);
                while (spawnAcc >= 1) { spawnAcc -= 1; SpawnTop(m, mk, wind); }
                float rr = rain > 0 ? rain * f * widthK * dt * SideFactor(m, PK.Rain, wind) : 0;
                while (rr >= 1 || (rr > 0 && R01() < rr)) { rr -= 1; SpawnTop(m, PK.Rain, wind); }
            }
            // дерево роняет лепестки / листья
            if (SS.Tree && (Sea == Season.Spring || Sea == Season.Autumn))
            {
                treeAcc += dt * (Sea == Season.Spring ? 2.2f : 1.3f);
                while (treeAcc >= 1)
                {
                    treeAcc -= 1;
                    RectangleF crown;
                    Mon tm;
                    if (TreeCrown(out crown, out tm))
                    {
                        var q = NewParticle(Sea == Season.Spring ? PK.Petal : PK.Leaf, crown.X + R01() * crown.Width, crown.Y + R01() * crown.Height);
                        if (q != null) { q.VY = 10 * K; }
                    }
                }
            }
        }

        // Средняя скорость падения и сноса частицы данного вида — для расчёта, откуда она должна прийти
        void Flow(byte kind, float wind, out float vy, out float vx)
        {
            float vf = SS.Speed / 100f;
            switch (kind)
            {
                case PK.Rain: vy = 985 * vf * K; vx = wind * 2.2f; break;
                case PK.Leaf: vy = 62 * vf * K; vx = wind; break;
                case PK.Petal: vy = 43 * vf * K; vx = wind; break;
                case PK.Fluff: vy = 12 * K; vx = wind * 1.4f; break;
                default: vy = 40 * vf * K * (SS.Weather == 2 ? 1.6f : 1f); vx = wind; break;
            }
        }

        // Во сколько раз больше частиц нужно монитору, чтобы с ветром часть их пришла сбоку
        float SideFactor(Mon m, byte kind, float wind)
        {
            float vy, vx; Flow(kind, wind, out vy, out vx);
            return 1 + m.B.Height * Math.Abs(vx) / (m.B.Width * vy);
        }

        // Частица появляется над монитором или — при ветре — у его наветренного края.
        // Поток через верх ~ ширина × скорость падения, через бок ~ высота × скорость ветра.
        // Сбоку частица появляется, только если там нет соседнего монитора: сосед и так её «надует».
        void SpawnTop(Mon m, byte kind, float wind)
        {
            float vy, vx; Flow(kind, wind, out vy, out vx);
            float top = m.B.Width * vy, side = m.B.Height * Math.Abs(vx);
            if (side > 1 && R01() < side / (top + side))
            {
                float y = m.B.Top + R01() * m.B.Height;
                float x = vx > 0 ? m.B.Left - 10 * K : m.B.Right + 10 * K;
                if (MonAt(x, y) == null) NewParticle(kind, x, y);
                return;
            }
            NewParticle(kind, m.B.Left + R01() * m.B.Width, m.B.Top - 12 * K);
        }

        Particle NewParticle(byte kind, float x, float y)
        {
            if (P.Count >= MaxParticles && kind != PK.Splash) return null;
            float sf = SS.Size / 100f, vf = SS.Speed / 100f;
            var q = new Particle { Kind = kind, X = x, Y = y, Phase = R01() * 100f, Life = 0 };
            switch (kind)
            {
                case PK.Snow:
                    q.Size = (1.0f + R01() * R01() * 4.2f) * K * sf;
                    q.TVY = (22f + q.Size / K * 9f) * vf * K * (SS.Weather == 2 ? 1.6f : 1f);
                    q.Sway = Rng(8, 26) * K;
                    break;
                case PK.Rain:
                    q.Size = Rng(0.8f, 1.4f) * K;
                    q.TVY = Rng(820, 1150) * vf * K;
                    break;
                case PK.Leaf:
                    q.Size = Rng(0.85f, 1.45f) * K * sf;
                    q.TVY = Rng(45, 80) * vf * K;
                    q.Sway = Rng(25, 55) * K;
                    q.VRot = Rng(-120, 120);
                    q.Rot = R01() * 360;
                    q.Color = (byte)rnd.Next(Art.LeafColors.Length);
                    q.Shape = (byte)(R01() < 0.7 ? 0 : 1);
                    break;
                case PK.Petal:
                    q.Size = Rng(0.8f, 1.3f) * K * sf;
                    q.TVY = Rng(32, 55) * vf * K;
                    q.Sway = Rng(20, 45) * K;
                    q.VRot = Rng(-160, 160);
                    q.Rot = R01() * 360;
                    q.Color = (byte)rnd.Next(Art.PetalColors.Length);
                    break;
                case PK.Fluff:
                    q.Size = Rng(0.8f, 1.3f) * K * sf;
                    q.TVY = Rng(6, 18) * vf * K;
                    q.Sway = Rng(15, 30) * K;
                    q.Rot = R01() * 360; q.VRot = Rng(-20, 20);
                    break;
                case PK.Splash:
                    q.Size = Rng(0.8f, 1.5f) * K;
                    q.TVY = 2000 * K;
                    break;
            }
            q.VY = q.TVY;
            q.VX = Wind;
            P.Add(q);
            return q;
        }

        void AddSnowPuff(float x, float y, float vx, float vy)
        {
            var q = NewParticle(PK.Snow, x, y);
            if (q == null) return;
            q.VX = vx; q.VY = vy; q.Size = Math.Max(q.Size, 1.4f * K);
        }

        // ---------------- движение частиц ----------------
        void UpdateParticles(float dt)
        {
            float wind = Wind;
            float vb = 0, vt = float.MaxValue;
            foreach (var m in Mons) { vb = Math.Max(vb, m.B.Bottom); vt = Math.Min(vt, m.B.Top); }
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var m in Mons) { minX = Math.Min(minX, m.B.Left); maxX = Math.Max(maxX, m.B.Right); }
            float g = 1400 * K;
            for (int i = P.Count - 1; i >= 0; i--)
            {
                var q = P[i];
                q.Life += dt; q.Phase += dt;
                float prevY = q.Y;
                float targetVX = wind;
                switch (q.Kind)
                {
                    case PK.Snow: targetVX += (float)Math.Sin(q.Phase * 0.9f) * q.Sway; break;
                    case PK.Leaf: targetVX += (float)Math.Sin(q.Phase * 1.3f) * q.Sway; q.Rot += q.VRot * dt; break;
                    case PK.Petal: targetVX += (float)Math.Sin(q.Phase * 1.7f) * q.Sway; q.Rot += q.VRot * dt; break;
                    case PK.Fluff:
                        targetVX = wind * 1.4f + (float)Math.Sin(q.Phase * 0.5f) * q.Sway;
                        q.TVY = (float)Math.Sin(q.Phase * 0.37f) * 14f * K + 9f * K;
                        q.Rot += q.VRot * dt; break;
                    case PK.Rain: targetVX = wind * 2.2f; break;
                }
                if (q.Kind == PK.Splash)
                {
                    q.VY += g * dt;
                    if (q.Life > 0.45f) { P.RemoveAt(i); continue; }
                }
                else
                {
                    if (q.Kind == PK.Snow && q.Shape == 1)
                    {
                        // снежный ком: обычная сила тяжести, горизонтальная скорость гаснет медленно
                        q.VY = Math.Min(q.TVY, q.VY + g * dt);
                        q.VX += (targetVX - q.VX) * Math.Min(1f, dt * 0.8f);
                    }
                    else
                    {
                        float a = Math.Min(1f, dt * (q.Kind == PK.Rain ? 8f : 2.2f));
                        q.VY += (q.TVY - q.VY) * a;
                        q.VX += (targetVX - q.VX) * a;
                    }
                }
                q.X += q.VX * dt; q.Y += q.VY * dt;

                if (q.Y > vb + 20 || q.X < minX - 600 * K || q.X > maxX + 600 * K || (q.Kind == PK.Fluff && q.Life > 60) || q.Y < vt - 400 * K)
                { P.RemoveAt(i); continue; }
                if (q.Kind == PK.Splash || q.VY <= 0) continue;
                if (TryLand(q, prevY)) P.RemoveAt(i);
            }
        }

        bool TryLand(Particle q, float prevY)
        {
            bool accum = AccumSeason && q.Kind != PK.Rain && q.Kind != PK.Fluff;
            // окна, сверху вниз по Z (стряхнутый ком первые полсекунды летит мимо окон — иначе снова ляжет на то же окно)
            bool freshClump = q.Kind == PK.Snow && q.Shape == 1 && q.Life < 0.5f;
            for (int i = 0; i < Z.Count && !freshClump; i++)
            {
                var p = Z[i];
                if (!p.Decor || p.Segs.Count == 0) continue;
                if (q.X < p.R.Left || q.X >= p.R.Right) continue;
                int col = (int)((q.X - p.R.Left) / CW);
                if (col < 0 || col >= p.Hh.Length) continue;
                float surf = p.R.Top - p.Hh[col];
                if (prevY > surf + 1 || q.Y < surf) continue;
                if (!InSegs(p, q.X)) continue;
                if (q.Kind == PK.Rain) { Splash(q.X, surf); return true; }
                if (!accum || !SS.WindowPiles) continue;
                if (Deposit(p.Hh, col, PileMax, true, q, p.Items, p.R.Left)) return true;
            }
            // низ экрана (над панелью задач)
            foreach (var m in Mons)
            {
                if (q.X < m.W.Left || q.X >= m.W.Right) continue;
                if (q.Y < m.W.Top) continue;
                int col = (int)((q.X - m.W.Left) / CW);
                if (col < 0 || col >= m.Gh.Length) continue;
                float surf = m.W.Bottom - m.Gh[col];
                if (prevY > surf + 1 || q.Y < surf) continue;
                if (q.Kind == PK.Rain) { Splash(q.X, surf); return true; }
                if (q.Kind == PK.Fluff) return true;
                if (!accum || !SS.GroundPile) return true;
                if (Deposit(m.Gh, col, GroundMax, false, q, m.Items, m.W.Left)) return true;
            }
            return false;
        }

        bool Deposit(float[] hh, int col, float max, bool taper, Particle q, ItemList items, float originX)
        {
            int n = hh.Length;
            float lim = taper ? Limit(col, n, max) : max;
            if (hh[col] >= lim) return false;
            if (q.Kind == PK.Snow)
            {
                float amt = q.Size * 0.5f;
                hh[col] = Math.Min(lim, hh[col] + amt);
                if (col > 0) hh[col - 1] = Math.Min(taper ? Limit(col - 1, n, max) : max, hh[col - 1] + amt * 0.5f);
                if (col < n - 1) hh[col + 1] = Math.Min(taper ? Limit(col + 1, n, max) : max, hh[col + 1] + amt * 0.5f);
                return true;
            }
            if (items.Count > (taper ? 300 : 700)) return false;
            var it = new Item { Kind = q.Kind, Color = q.Color, Shape = q.Shape, Size = q.Size, Rot = q.Rot, Y = hh[col] + 2.5f * q.Size };
            it.X = q.X - originX;
            items.Add(it);
            float add = q.Size * (q.Kind == PK.Leaf ? 1.6f : 0.9f);
            hh[col] = Math.Min(lim, hh[col] + add);
            if (col > 0) hh[col - 1] = Math.Min(lim, hh[col - 1] + add * 0.6f);
            if (col < n - 1) hh[col + 1] = Math.Min(lim, hh[col + 1] + add * 0.6f);
            return true;
        }

        void Splash(float x, float y)
        {
            if (R01() > 0.45f) return;
            for (int k = 0; k < 2; k++)
            {
                var s = NewParticle(PK.Splash, x, y - 1);
                if (s == null) return;
                s.VX = Rng(-70, 70) * K; s.VY = -Rng(90, 200) * K;
            }
        }

        void RelaxAll()
        {
            if (!SnowSeason) return;
            float th = 1.4f * K;
            foreach (var p in Z) Relax(p.Hh, th);
            foreach (var m in Mons) Relax(m.Gh, th);
        }

        static void Relax(float[] h, float th)
        {
            for (int c = 0; c < h.Length - 1; c++)
            {
                float d = h[c] - h[c + 1];
                if (d > th || d < -th) { float t = d * 0.2f; h[c] -= t; h[c + 1] += t; }
            }
        }
    }
}
