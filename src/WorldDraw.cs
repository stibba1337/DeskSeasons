using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace DeskSeasons
{
    partial class World
    {
        static SolidBrush[] leafBr, petalBr, flagBr;
        static readonly SolidBrush white = new SolidBrush(Color.FromArgb(250, 252, 255));
        static readonly Pen rainPen = new Pen(Color.FromArgb(120, 190, 210, 235), 1.2f);
        static readonly SolidBrush splashBr = new SolidBrush(Color.FromArgb(150, 200, 220, 240));
        static readonly SolidBrush icicleBr = new SolidBrush(Color.FromArgb(190, 215, 235, 255));

        static void InitBrushes()
        {
            if (leafBr != null) return;
            leafBr = new SolidBrush[Art.LeafColors.Length];
            for (int i = 0; i < leafBr.Length; i++) leafBr[i] = new SolidBrush(Art.LeafColors[i]);
            petalBr = new SolidBrush[Art.PetalColors.Length];
            for (int i = 0; i < petalBr.Length; i++) petalBr[i] = new SolidBrush(Color.FromArgb(235, Art.PetalColors[i]));
            flagBr = new SolidBrush[Art.FlagColors.Length];
            for (int i = 0; i < flagBr.Length; i++) flagBr[i] = new SolidBrush(Art.FlagColors[i]);
        }

        public void Render(Mon m, Graphics g)
        {
            InitBrushes();
            rainPen.Width = 1.2f * K;
            var area = m.B;

            long s0 = Stopwatch.GetTimestamp();
            if (Sea == Season.Autumn && SS.Extra) DrawFog(g, m);
            if (SS.Tree && m.Primary) DrawTree(g);
            long s1 = Stopwatch.GetTimestamp();
            long sPile = 0, sGar = 0;

            // окна
            for (int i = Z.Count - 1; i >= 0; i--)
            {
                var p = Z[i];
                if (!p.Decor || p.Segs.Count == 0) continue;
                if (p.R.Right < area.Left || p.R.Left > area.Right || p.R.Top < area.Top - 60 || p.R.Top > area.Bottom + 60) continue;
                long q0 = Stopwatch.GetTimestamp();
                if (SnowSeason) DrawSnowPile(g, p.Hh, p.R.Left, p.R.Top, p);
                else DrawItems(g, p.Items, p.R.Left, p.R.Top, p, p.Layer, p.R.Width);
                if (SnowSeason && SS.Extra2) DrawIcicles(g, p);
                long q1 = Stopwatch.GetTimestamp();
                if (SS.Garland && SS.GarlandPlace != 1) DrawGarland(g, p.R.Left, p.R.Right, p.R.Top, p);
                sPile += q1 - q0; sGar += Stopwatch.GetTimestamp() - q1;
            }
            if (SS.Garland && SS.GarlandPlace != 0) DrawGarland(g, m.B.Left, m.B.Right, m.B.Top - 2 * K, null);

            long s2 = Stopwatch.GetTimestamp();
            // низ экрана
            if (SnowSeason) DrawSnowPile(g, m.Gh, m.W.Left, m.W.Bottom, null);
            else DrawItems(g, m.Items, m.W.Left, m.W.Bottom, null, m.Layer, m.W.Width);
            if (Sea == Season.Spring && SS.Extra2) DrawMeadow(g, m);

            foreach (var sm in Snowmen) if (sm.M == m) DrawSnowman(g, sm);
            if (Walker != null && Walker.M == m) DrawWalker(g, Walker);
            if (Cleaner != null && Cleaner.M == m) DrawCleaner(g, Cleaner);

            long s3 = Stopwatch.GetTimestamp();
            DrawParticles(g, area);
            long s4 = Stopwatch.GetTimestamp();

            foreach (var f in Flies) if (area.Contains((int)f.X, (int)f.Y)) DrawButterfly(g, f);
            foreach (var b in Balls) if (area.Contains((int)b.X, (int)b.Y)) Art.Ball(g, b.X, b.Y, b.R);
            if (Held != null) Art.Ball(g, Held.X, Held.Y, Held.R);
            foreach (var s in Splats) if (area.Contains((int)s.X, (int)s.Y)) DrawSplat(g, s);
            if (Modifier && AccumSeason && area.Contains((int)CursorX, (int)CursorY)) DrawMitten(g);
            if (Bolt != null && Bolt.M == m) DrawBolt(g, Bolt);
            long s5 = Stopwatch.GetTimestamp();
            Prof[0] += s1 - s0; Prof[1] += sPile; Prof[2] += sGar; Prof[3] += s3 - s2; Prof[4] += s4 - s3; Prof[5] += s5 - s4;
        }
        public static readonly long[] Prof = new long[6]; // декор, накопленное на окнах, гирлянды, низ+персонажи, частицы, прочее

        // ---------------- частицы ----------------
        void DrawParticles(Graphics g, Rectangle a)
        {
            float l = a.Left - 20, r = a.Right + 20, t = a.Top - 30, b = a.Bottom + 20;
            foreach (var q in P)
            {
                if (q.X < l || q.X > r || q.Y < t || q.Y > b) continue;
                switch (q.Kind)
                {
                    case PK.Snow:
                        {
                            int idx = (int)Math.Round((q.Size / K - 1.2f) / 0.75f);
                            if (idx < 0) idx = 0; if (idx > 7) idx = 7;
                            var img = Art.Flakes[idx];
                            Blit(g, img, q.X - img.Width / 2f, q.Y - img.Height / 2f);
                            if (q.Shape == 1)
                            {
                                // ком неровный: к основному пятну прилеплены два поменьше
                                var sm = Art.Flakes[Math.Max(0, idx - 3)];
                                float o = q.Size * 0.7f;
                                Blit(g, sm, q.X + o - sm.Width / 2f, q.Y - o * 0.4f - sm.Height / 2f);
                                Blit(g, sm, q.X - o * 0.8f - sm.Width / 2f, q.Y + o * 0.3f - sm.Height / 2f);
                            }
                            break;
                        }
                    case PK.Rain:
                        g.DrawLine(rainPen, q.X, q.Y, q.X - q.VX * 0.018f, q.Y - q.VY * 0.018f);
                        break;
                    case PK.Splash:
                        g.FillEllipse(splashBr, q.X - q.Size, q.Y - q.Size, q.Size * 2, q.Size * 2);
                        break;
                    case PK.Leaf:
                        DrawLeaf(g, q.X, q.Y, q.Rot, q.Size, q.Color, q.Shape, (float)Math.Cos(q.Phase * 2.3f));
                        break;
                    case PK.Petal:
                        DrawPetal(g, q.X, q.Y, q.Rot, q.Size, q.Color, (float)Math.Cos(q.Phase * 2.8f));
                        break;
                    case PK.Fluff:
                        DrawFluff(g, q.X, q.Y, q.Rot, q.Size);
                        break;
                }
            }
        }

        // Готовая картинка копируется по целым пикселям без масштаба — у GDI+ это быстрый путь.
        // По дробным координатам он пересчитывает каждую точку с интерполяцией, а это в разы медленнее.
        static void Blit(Graphics g, Image img, float x, float y)
        {
            var im = g.InterpolationMode; var pm = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            g.DrawImage(img, new Rectangle((int)Math.Round(x), (int)Math.Round(y), img.Width, img.Height));
            g.InterpolationMode = im; g.PixelOffsetMode = pm;
        }

        static void DrawLeaf(Graphics g, float x, float y, float rot, float s, int color, int shape, float flip)
        {
            var img = Art.LeafSprite(false, shape, color % Art.LeafColors.Length, rot, flip, s);
            Blit(g, img, x - img.Width / 2f, y - img.Height / 2f);
        }

        static void DrawPetal(Graphics g, float x, float y, float rot, float s, int color, float flip)
        {
            var img = Art.LeafSprite(true, 0, color % Art.PetalColors.Length, rot, flip, s);
            Blit(g, img, x - img.Width / 2f, y - img.Height / 2f);
        }

        void DrawFluff(Graphics g, float x, float y, float rot, float s)
        {
            using (var pen = new Pen(Color.FromArgb(170, 255, 255, 255), 0.7f * K))
            using (var br = new SolidBrush(Color.FromArgb(200, 255, 255, 250)))
            {
                for (int i = 0; i < 9; i++)
                {
                    double a = (rot + i * 40) * Math.PI / 180;
                    float ex = x + (float)Math.Cos(a) * 7 * s, ey = y + (float)Math.Sin(a) * 7 * s;
                    g.DrawLine(pen, x, y, ex, ey);
                    g.FillEllipse(br, ex - 1.1f * s, ey - 1.1f * s, 2.2f * s, 2.2f * s);
                }
                using (var c = new SolidBrush(Color.FromArgb(220, 170, 150, 110))) g.FillEllipse(c, x - 1.5f * s, y - 1.5f * s, 3 * s, 3 * s);
            }
        }

        // ---------------- накопленное ----------------
        void DrawSnowPile(Graphics g, float[] hh, float left, float top, Pile p)
        {
            int n = hh.Length;
            var segs = new List<float>();
            if (p != null) foreach (var s in p.Segs) segs.Add(s);
            else { segs.Add(left); segs.Add(left + (n - 1) * CW); }
            var pts = new List<PointF>();
            for (int s = 0; s < segs.Count; s += 2)
            {
                float x0 = segs[s], x1 = segs[s + 1];
                int c0 = Math.Max(0, (int)Math.Ceiling((x0 - left) / CW)), c1 = Math.Min(n - 1, (int)((x1 - left) / CW));
                if (c1 <= c0) continue;
                float mx = 0;
                for (int c = c0; c <= c1; c++) if (hh[c] > mx) mx = hh[c];
                if (mx < 0.6f) continue;
                pts.Clear();
                pts.Add(new PointF(x0, top + 1));
                pts.Add(new PointF(x0, top - hh[c0] * 0.6f));
                for (int c = c0; c <= c1; c++) pts.Add(new PointF(left + c * CW, top - hh[c]));
                pts.Add(new PointF(x1, top - hh[c1] * 0.6f));
                pts.Add(new PointF(x1, top + 1));
                var arr = pts.ToArray();
                using (var br = new LinearGradientBrush(new RectangleF(x0, top - mx - 1, Math.Max(1, x1 - x0), mx + 3),
                    Color.FromArgb(255, 255, 255, 255), Color.FromArgb(255, 215, 228, 245), LinearGradientMode.Vertical))
                    g.FillPolygon(br, arr);
                using (var pen = new Pen(Color.FromArgb(70, 160, 185, 215), 1f * K))
                    g.DrawLines(pen, Slice(arr, 1, arr.Length - 2));
            }
        }

        static PointF[] Slice(PointF[] a, int from, int count)
        {
            var r = new PointF[count];
            Array.Copy(a, from, r, 0, count);
            return r;
        }

        // Лежащие листья не двигаются, поэтому рисуются в отдельную картинку
        // только когда что-то изменилось, а каждый кадр копируется готовый слой.
        void DrawItems(Graphics g, ItemList items, float left, float top, Pile p, ItemLayer layer, float width)
        {
            if (items.Count == 0) return;
            int pad = (int)(24 * K);
            int maxH = (int)((p != null ? PileMax : GroundMax) + 2 * pad);
            int w = (int)width + pad * 2, h = maxH + pad;
            if (layer.Bmp == null || layer.Bmp.Width != w || layer.Bmp.Height != h)
            {
                layer.Dispose();
                layer.Bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                layer.Ver = -1; layer.Removals = -1;
            }
            layer.Pad = pad; layer.MaxH = maxH;
            if (layer.Ver != items.Ver)
            {
                // только добавились новые — дорисовываем их поверх, без полной перерисовки
                bool onlyAdds = layer.Removals == items.Removals && items.Count >= layer.Count;
                int from = onlyAdds ? layer.Count : 0;
                layer.Ver = items.Ver; layer.Removals = items.Removals; layer.Count = items.Count;
                using (var lg = Graphics.FromImage(layer.Bmp))
                {
                    if (!onlyAdds) lg.Clear(Color.Transparent);
                    for (int i = from; i < items.Count; i++)
                    {
                        var it = items[i];
                        float x = pad + it.X, y = maxH - it.Y;
                        if (it.Kind == PK.Leaf) DrawLeaf(lg, x, y, it.Rot, it.Size, it.Color, it.Shape, 0.55f);
                        else DrawPetal(lg, x, y, it.Rot, it.Size, it.Color, 0.6f);
                    }
                }
            }
            float ox = left - pad, oy = top - maxH;
            if (p == null) { Blit(g, layer.Bmp, ox, oy); return; }
            for (int s = 0; s < p.Segs.Count; s += 2)
            {
                float x0 = p.Segs[s], x1 = p.Segs[s + 1];
                if (s == 0 && x0 <= p.R.Left) x0 = ox;                         // крайние участки — с запасом,
                if (s == p.Segs.Count - 2 && x1 >= p.R.Right) x1 = ox + w;      // чтобы листья на углах не обрезались
                var src = new RectangleF(x0 - ox, 0, x1 - x0, h);
                g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.None;
                g.DrawImage(layer.Bmp, new Rectangle((int)Math.Round(x0), (int)Math.Round(oy), (int)Math.Round(x1 - x0), h), new Rectangle((int)Math.Round(src.X), 0, (int)Math.Round(x1 - x0), h), GraphicsUnit.Pixel);
            }
        }

        void DrawIcicles(Graphics g, Pile p)
        {
            float step = 7 * K;
            float max = PileMax;
            using (var hi = new Pen(Color.FromArgb(200, 255, 255, 255), 0.8f * K))
                for (int s = 0; s < p.Segs.Count; s += 2)
                {
                    int i0 = (int)Math.Ceiling((p.Segs[s] - p.R.Left) / step), i1 = (int)((p.Segs[s + 1] - p.R.Left) / step);
                    for (int i = i0; i <= i1; i++)
                    {
                        float x = p.R.Left + i * step;
                        int col = (int)((x - p.R.Left) / CW);
                        if (col < 0 || col >= p.Hh.Length || p.Hh[col] < 2.5f * K) continue;
                        float hsh = Hash(i, 7);
                        if (hsh < 0.35f) continue;
                        float len = (3 + hsh * 15) * K * Math.Min(1f, p.Hh[col] / (max * 0.6f));
                        float w = 2.6f * K;
                        g.FillPolygon(icicleBr, new[] { new PointF(x - w / 2, p.R.Top), new PointF(x + w / 2, p.R.Top), new PointF(x, p.R.Top + len) });
                        g.DrawLine(hi, x - w * 0.15f, p.R.Top + 1, x, p.R.Top + len * 0.8f);
                    }
                }
        }

        // ---------------- гирлянда ----------------
        bool BulbOn(int idx)
        {
            int mode = (int)(T / 8) % 3;
            if (Sea == Season.Autumn || Sea == Season.Spring) return Hash(idx, (int)(T * 1.2f)) > 0.2f;
            switch (mode)
            {
                case 0: return Hash(idx, (int)(T * 2.5f)) > 0.3f;
                case 1: return ((idx + (int)(T * 5)) % 4) != 0;
                default: return (idx % 2) == ((int)(T * 1.6f) % 2);
            }
        }

        void DrawGarland(Graphics g, float left, float right, float top, Pile p)
        {
            float w = right - left;
            if (w < 60 * K) return;
            var st = g.Save();
            if (p != null)
            {
                var reg = new Region(RectangleF.Empty);
                for (int s = 0; s < p.Segs.Count; s += 2)
                    reg.Union(new RectangleF(p.Segs[s], top - 30 * K, p.Segs[s + 1] - p.Segs[s], 70 * K));
                g.Clip = reg;
                reg.Dispose();
            }
            float spanW = 120 * K;
            int spans = Math.Max(1, (int)Math.Round(w / spanW));
            spanW = w / spans;
            float sag = 13 * K, y0 = top + 3 * K;
            Color wire = Sea == Season.Winter ? Color.FromArgb(220, 30, 70, 40) : Sea == Season.Spring ? Color.FromArgb(220, 70, 140, 60) :
                         Sea == Season.Summer ? Color.FromArgb(220, 240, 240, 240) : Color.FromArgb(220, 110, 70, 40);
            int per = Sea == Season.Summer ? 5 : 5;
            using (var pen = new Pen(wire, 1.5f * K))
                for (int sp = 0; sp < spans; sp++)
                {
                    float a = left + sp * spanW;
                    var pts = new PointF[9];
                    for (int i = 0; i < 9; i++) { float t = i / 8f; pts[i] = new PointF(a + t * spanW, y0 + sag * 4 * t * (1 - t)); }
                    g.DrawCurve(pen, pts);
                    for (int j = 0; j < per; j++)
                    {
                        float t = (j + 0.5f) / per;
                        float bx = a + t * spanW, by = y0 + sag * 4 * t * (1 - t);
                        int idx = sp * per + j;
                        DrawBulb(g, bx, by, idx, t);
                    }
                }
            g.Restore(st);
        }

        void DrawBulb(Graphics g, float x, float y, int idx, float t)
        {
            switch (Sea)
            {
                case Season.Winter:
                    {
                        var img = BulbOn(idx) ? Art.BulbOn[idx % 5] : Art.BulbOff[idx % 5];
                        Blit(g, img, x - img.Width / 2f, y - 4 * K);
                        break;
                    }
                case Season.Spring:
                    {
                        if (idx % 2 == 1)
                        {
                            var st = g.Save();
                            g.TranslateTransform(x, y + 3 * K); g.RotateTransform(idx % 4 == 1 ? 40 : -40); g.ScaleTransform(0.5f * K, 0.5f * K);
                            using (var br = new SolidBrush(Color.FromArgb(230, 90, 170, 70))) g.FillPath(br, Art.OvalLeaf);
                            g.Restore(st);
                            break;
                        }
                        var img = BulbOn(idx) ? Art.FlowerOn[(idx / 2) % 5] : Art.FlowerOff[(idx / 2) % 5];
                        Blit(g, img, x - img.Width / 2f, y - img.Height / 2f + 2 * K);
                        break;
                    }
                case Season.Summer:
                    {
                        float sway = (float)Math.Sin(T * 2.2 + idx * 0.7) * 2.5f * K;
                        float fw = 11 * K, fh = 15 * K;
                        g.FillPolygon(flagBr[idx % flagBr.Length], new[] { new PointF(x - fw / 2, y), new PointF(x + fw / 2, y), new PointF(x + sway, y + fh) });
                        break;
                    }
                default:
                    {
                        if (idx % 3 == 2)
                        {
                            DrawLeaf(g, x, y + 5 * K, 180 + (idx % 2 == 0 ? 25 : -25), 0.75f * K, idx % Art.LeafColors.Length, 0, 1);
                            break;
                        }
                        var img = BulbOn(idx) ? Art.AutumnOn[idx % 5] : Art.AutumnOff[idx % 5];
                        Blit(g, img, x - img.Width / 2f, y - 4 * K);
                        break;
                    }
            }
        }

        // ---------------- декорация в углу ----------------
        void DrawTree(Graphics g)
        {
            Mon m; float x, by;
            if (!TreePos(out m, out x, out by)) return;
            float k = K;
            switch (Sea)
            {
                case Season.Winter: DrawFir(g, x, by, k); break;
                case Season.Spring: DrawBlossom(g, x, by, k, true); break;
                case Season.Summer: DrawSunflowers(g, x, by, k); break;
                default: DrawBlossom(g, x, by, k, false); break;
            }
        }

        void DrawFir(Graphics g, float x, float by, float k)
        {
            using (var br = new SolidBrush(Color.FromArgb(255, 110, 70, 40))) g.FillRectangle(br, x - 9 * k, by - 26 * k, 18 * k, 26 * k);
            float[] tw = { 120, 96, 70 }, ty = { 26, 70, 108 }, th = { 62, 56, 50 };
            for (int i = 0; i < 3; i++)
            {
                float yb = by - ty[i] * k, w = tw[i] * k / 2, h = th[i] * k;
                var tri = new[] { new PointF(x - w, yb), new PointF(x + w, yb), new PointF(x, yb - h) };
                using (var br = new LinearGradientBrush(new RectangleF(x - w, yb - h, w * 2, h), Color.FromArgb(255, 40, 140, 70), Color.FromArgb(255, 20, 90, 45), LinearGradientMode.Horizontal))
                    g.FillPolygon(br, tri);
                // снег на ветках
                using (var sb = new SolidBrush(Color.FromArgb(230, 250, 252, 255)))
                    g.FillPolygon(sb, new[] { new PointF(x - w, yb), new PointF(x - w * 0.6f, yb - 5 * k), new PointF(x - w * 0.2f, yb - 2 * k), new PointF(x + w * 0.2f, yb - 5 * k), new PointF(x + w * 0.6f, yb - 2 * k), new PointF(x + w, yb), new PointF(x, yb + 1) });
            }
            // мишура
            using (var pen = new Pen(Color.FromArgb(200, 255, 215, 90), 2 * k))
            {
                g.DrawBezier(pen, x - 50 * k, by - 40 * k, x - 10 * k, by - 28 * k, x + 20 * k, by - 40 * k, x + 45 * k, by - 52 * k);
                g.DrawBezier(pen, x - 36 * k, by - 86 * k, x - 6 * k, by - 74 * k, x + 14 * k, by - 84 * k, x + 34 * k, by - 96 * k);
            }
            // огоньки
            for (int i = 0; i < 22; i++)
            {
                float hy = Hash(i, 11), hx = Hash(i, 23);
                float yy = 30 + hy * 120; // высота над землёй
                float half = 60 - (yy - 26) * 0.5f;
                if (half < 6) half = 6;
                float lx = x + (hx * 2 - 1) * half * 0.85f * k, ly = by - yy * k;
                var img = BulbOn(i * 7) ? Art.BulbOn[i % 5] : Art.BulbOff[i % 5];
                g.DrawImage(img, lx - img.Width / 2f, ly - img.Height / 2f, img.Width * 0.8f, img.Height * 0.8f);
            }
            DrawStar(g, x, by - 160 * k, 11 * k);
            // подарки
            DrawGift(g, x - 52 * k, by, 26 * k, 20 * k, Color.FromArgb(220, 50, 50), Color.FromArgb(255, 220, 80));
            DrawGift(g, x + 30 * k, by, 30 * k, 24 * k, Color.FromArgb(60, 110, 220), Color.FromArgb(240, 240, 240));
            DrawGift(g, x + 8 * k, by, 18 * k, 14 * k, Color.FromArgb(60, 170, 90), Color.FromArgb(230, 60, 60));
        }

        void DrawStar(Graphics g, float cx, float cy, float r)
        {
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float rr = i % 2 == 0 ? r : r * 0.45f;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rr, cy + (float)Math.Sin(a) * rr);
            }
            float glow = 0.6f + 0.4f * (float)Math.Sin(T * 3);
            using (var br = new SolidBrush(Color.FromArgb((int)(70 * glow), 255, 230, 120))) g.FillEllipse(br, cx - r * 2, cy - r * 2, r * 4, r * 4);
            using (var br = new SolidBrush(Color.FromArgb(255, 255, 215, 60))) g.FillPolygon(br, pts);
        }

        void DrawGift(Graphics g, float x, float by, float w, float h, Color box, Color ribbon)
        {
            using (var br = new SolidBrush(box)) g.FillRectangle(br, x, by - h, w, h);
            using (var br = new SolidBrush(ribbon))
            {
                g.FillRectangle(br, x + w / 2 - w * 0.08f, by - h, w * 0.16f, h);
                g.FillRectangle(br, x, by - h * 0.6f, w, h * 0.14f);
                g.FillEllipse(br, x + w / 2 - w * 0.32f, by - h - w * 0.18f, w * 0.3f, w * 0.2f);
                g.FillEllipse(br, x + w / 2 + w * 0.02f, by - h - w * 0.18f, w * 0.3f, w * 0.2f);
            }
        }

        void DrawBlossom(Graphics g, float x, float by, float k, bool spring)
        {
            using (var pen = new Pen(Color.FromArgb(255, 105, 70, 50), 9 * k))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawLine(pen, x, by, x - 2 * k, by - 95 * k);
                pen.Width = 5 * k;
                g.DrawLine(pen, x - 2 * k, by - 80 * k, x - 42 * k, by - 120 * k);
                g.DrawLine(pen, x - 2 * k, by - 88 * k, x + 40 * k, by - 128 * k);
                g.DrawLine(pen, x - 2 * k, by - 95 * k, x + 4 * k, by - 150 * k);
            }
            Color[] cols = spring
                ? new[] { Color.FromArgb(240, 255, 190, 210), Color.FromArgb(240, 250, 160, 190), Color.FromArgb(240, 255, 220, 232), Color.FromArgb(240, 245, 140, 175) }
                : new[] { Color.FromArgb(240, 230, 100, 30), Color.FromArgb(240, 200, 60, 30), Color.FromArgb(240, 240, 170, 40), Color.FromArgb(240, 215, 130, 40) };
            for (int i = 0; i < 26; i++)
            {
                float hx = Hash(i, 3) * 2 - 1, hy = Hash(i, 5);
                float cx = x + hx * 62 * k, cy = by - (100 + hy * 75) * k;
                float r = (14 + Hash(i, 9) * 14) * k;
                float sway = (float)Math.Sin(T * 0.8 + i) * 1.2f * k;
                using (var br = new SolidBrush(cols[i % cols.Length])) g.FillEllipse(br, cx - r + sway, cy - r, r * 2, r * 2);
            }
            if (!spring)
            {
                DrawPumpkin(g, x - 48 * k, by, 20 * k);
                DrawPumpkin(g, x + 40 * k, by, 15 * k);
            }
        }

        void DrawPumpkin(Graphics g, float x, float by, float r)
        {
            using (var br = new SolidBrush(Color.FromArgb(255, 235, 120, 30)))
            {
                g.FillEllipse(br, x - r * 1.2f, by - r * 1.5f, r * 1.5f, r * 1.5f);
                g.FillEllipse(br, x - r * 0.3f, by - r * 1.5f, r * 1.5f, r * 1.5f);
            }
            using (var br = new SolidBrush(Color.FromArgb(255, 250, 145, 45))) g.FillEllipse(br, x - r * 0.75f, by - r * 1.6f, r * 1.5f, r * 1.6f);
            using (var pen = new Pen(Color.FromArgb(255, 90, 110, 40), r * 0.22f)) g.DrawLine(pen, x, by - r * 1.55f, x + r * 0.15f, by - r * 1.95f);
        }

        void DrawSunflowers(Graphics g, float x, float by, float k)
        {
            float[] dx = { -30, 0, 34 }, hh = { 95, 140, 110 };
            for (int i = 0; i < 3; i++)
            {
                float sway = (float)Math.Sin(T * 1.1 + i * 1.7) * 4 * k;
                float fx = x + dx[i] * k + sway, fy = by - hh[i] * k;
                using (var pen = new Pen(Color.FromArgb(255, 70, 140, 50), 4 * k))
                    g.DrawBezier(pen, x + dx[i] * k, by, x + dx[i] * k, by - hh[i] * k * 0.4f, fx, fy + hh[i] * k * 0.3f, fx, fy);
                var st = g.Save();
                g.TranslateTransform(x + dx[i] * k + sway * 0.5f, by - hh[i] * k * 0.45f);
                g.RotateTransform(i % 2 == 0 ? -50 : 50); g.ScaleTransform(1.2f * k, 1.2f * k);
                using (var br = new SolidBrush(Color.FromArgb(255, 80, 160, 60))) g.FillPath(br, Art.OvalLeaf);
                g.Restore(st);
                float r = (16 + i * 3) * k;
                using (var br = new SolidBrush(Color.FromArgb(255, 255, 200, 30)))
                    for (int j = 0; j < 14; j++)
                    {
                        double a = j * Math.PI * 2 / 14 + T * 0.1;
                        float px = fx + (float)Math.Cos(a) * r * 0.85f, py = fy + (float)Math.Sin(a) * r * 0.85f;
                        g.FillEllipse(br, px - r * 0.35f, py - r * 0.35f, r * 0.7f, r * 0.7f);
                    }
                using (var br = new SolidBrush(Color.FromArgb(255, 110, 70, 30))) g.FillEllipse(br, fx - r * 0.6f, fy - r * 0.6f, r * 1.2f, r * 1.2f);
            }
        }

        void DrawMeadow(Graphics g, Mon m)
        {
            float k = K;
            float baseY = m.W.Bottom;
            using (var pen = new Pen(Color.FromArgb(230, 90, 170, 70), 1.6f * k))
            {
                int n = (int)(m.W.Width / (6 * k));
                for (int i = 0; i < n; i++)
                {
                    float x = m.W.Left + i * 6 * k;
                    float h = (5 + Hash(i, 31) * 11) * k;
                    float sway = (float)Math.Sin(T * 1.5 + x * 0.02) * 2.5f * k;
                    g.DrawLine(pen, x, baseY, x + sway + (Hash(i, 33) - 0.5f) * 4 * k, baseY - h);
                }
            }
            int fn = (int)(m.W.Width / (45 * k));
            for (int i = 0; i < fn; i++)
            {
                if (Hash(i, 41) < 0.4f) continue;
                float x = m.W.Left + (i + Hash(i, 43)) * 45 * k;
                float h = (12 + Hash(i, 47) * 14) * k;
                float sway = (float)Math.Sin(T * 1.3 + i) * 2 * k;
                using (var pen = new Pen(Color.FromArgb(255, 70, 150, 60), 1.4f * k)) g.DrawLine(pen, x, baseY, x + sway, baseY - h);
                var img = Art.FlowerOn[i % 5];
                g.DrawImage(img, x + sway - img.Width * 0.4f, baseY - h - img.Height * 0.4f, img.Width * 0.8f, img.Height * 0.8f);
            }
        }

        void DrawFog(Graphics g, Mon m)
        {
            var img = Art.Fog;
            var oldI = g.InterpolationMode; var oldP = g.PixelOffsetMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.None;
            for (int i = 0; i < 3; i++)
                g.DrawImageUnscaledAndClipped(img, new Rectangle((int)(m.FogX[i] - img.Width / 2), (int)(m.W.Bottom - img.Height * (0.6f + (i % 2) * 0.2f)), img.Width, img.Height));
            g.InterpolationMode = oldI; g.PixelOffsetMode = oldP;
        }

        // ---------------- снеговик ----------------
        void DrawSnowman(Graphics g, Snowman sm)
        {
            List<float> rr; var cs = BallCenters(sm, out rr);
            if (sm.Complete)
            {
                var mid = cs[1]; float mr = rr[1];
                using (var pen = new Pen(Color.FromArgb(255, 100, 65, 35), 2.2f * K))
                {
                    g.DrawLine(pen, mid.X - mr * 0.8f, mid.Y, mid.X - mr * 2f, mid.Y - mr * 0.9f);
                    g.DrawLine(pen, mid.X - mr * 1.6f, mid.Y - mr * 0.6f, mid.X - mr * 1.9f, mid.Y - mr * 0.2f);
                    // правая рука: замах при броске, взмахи при наведении курсора
                    float hdx = CursorX - cs[2].X, hdy = CursorY - cs[2].Y;
                    bool wave = hdx * hdx + hdy * hdy < rr[2] * rr[2] * 9;
                    float ey = sm.ThrowT > 0 ? -2.3f : wave ? -1.3f - 0.7f * (float)Math.Sin(T * 12) : -0.7f;
                    float ex = sm.ThrowT > 0 ? 1.3f : 2f;
                    g.DrawLine(pen, mid.X + mr * 0.8f, mid.Y, mid.X + mr * ex, mid.Y + mr * ey);
                }
            }
            for (int i = 0; i < cs.Count; i++) Art.Ball(g, cs[i].X, cs[i].Y, rr[i]);
            if (!sm.Complete) return;
            var hd = cs[2]; float hr = rr[2];
            var body = cs[1]; float brr = rr[1];
            using (var b = new SolidBrush(Color.FromArgb(255, 30, 30, 35)))
            {
                g.FillEllipse(b, hd.X - hr * 0.42f, hd.Y - hr * 0.3f, hr * 0.24f, hr * 0.24f);
                g.FillEllipse(b, hd.X + hr * 0.12f, hd.Y - hr * 0.3f, hr * 0.24f, hr * 0.24f);
                for (int i = 0; i < 3; i++) g.FillEllipse(b, body.X - brr * 0.1f, body.Y - brr * 0.5f + i * brr * 0.42f, brr * 0.2f, brr * 0.2f);
                for (int i = 0; i < 5; i++)
                {
                    double a = Math.PI * (0.2 + i * 0.15);
                    g.FillEllipse(b, hd.X + (float)Math.Cos(a) * hr * 0.55f - hr * 0.06f, hd.Y + (float)Math.Sin(a) * hr * 0.5f, hr * 0.12f, hr * 0.12f);
                }
            }
            using (var b = new SolidBrush(Color.FromArgb(255, 245, 130, 30)))
                g.FillPolygon(b, new[] { new PointF(hd.X, hd.Y - hr * 0.08f), new PointF(hd.X, hd.Y + hr * 0.22f), new PointF(hd.X + hr * 1.0f, hd.Y + hr * 0.12f) });
            Color[] sc = { Color.FromArgb(220, 50, 50), Color.FromArgb(50, 120, 220), Color.FromArgb(60, 170, 90), Color.FromArgb(240, 180, 40) };
            using (var b = new SolidBrush(sc[sm.Scarf % 4]))
            {
                g.FillRectangle(b, hd.X - hr * 0.9f, hd.Y + hr * 0.75f, hr * 1.8f, hr * 0.32f);
                g.FillRectangle(b, hd.X + hr * 0.3f, hd.Y + hr * 0.8f, hr * 0.3f, hr * 0.9f);
            }
            if (sm.Hat == 0)
                using (var b = new SolidBrush(Color.FromArgb(255, 35, 35, 40)))
                {
                    g.FillRectangle(b, hd.X - hr * 0.85f, hd.Y - hr * 0.95f, hr * 1.7f, hr * 0.2f);
                    g.FillRectangle(b, hd.X - hr * 0.55f, hd.Y - hr * 1.75f, hr * 1.1f, hr * 0.85f);
                }
            else if (sm.Hat == 1)
                using (var b = new SolidBrush(Color.FromArgb(255, 200, 60, 50)))
                    g.FillPolygon(b, new[] { new PointF(hd.X - hr * 0.75f, hd.Y - hr * 0.75f), new PointF(hd.X + hr * 0.75f, hd.Y - hr * 0.75f), new PointF(hd.X + hr * 0.55f, hd.Y - hr * 1.6f), new PointF(hd.X - hr * 0.55f, hd.Y - hr * 1.6f) });
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(255, 60, 90, 200))) g.FillPie(b, hd.X - hr * 0.85f, hd.Y - hr * 1.5f, hr * 1.7f, hr * 1.5f, 180, 180);
                using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, hd.X - hr * 0.25f, hd.Y - hr * 1.75f, hr * 0.5f, hr * 0.5f);
            }
        }

        // ---------------- персонажи ----------------
        void DrawWalker(Graphics g, Walker w)
        {
            float k = K;
            float y = SurfaceGround(w.M, w.X) + 2 * k;
            var st = g.Save();
            g.TranslateTransform(w.X, y);
            if (w.State == 4) g.RotateTransform(-w.Dir * 12 * (float)Math.Sin(Math.Min(1, w.T / 0.75f) * Math.PI)); // отшатнулся
            g.ScaleTransform(w.Dir * k, k);
            switch (Sea)
            {
                case Season.Winter: DrawYeti(g, w); break;
                case Season.Spring: DrawBunny(g, w); break;
                case Season.Summer: DrawCat(g, w); break;
                default: DrawHedgehog(g, w); break;
            }
            g.Restore(st);
        }

        void DrawYeti(Graphics g, Walker w)
        {
            float a = w.State == 0 ? (float)Math.Sin(w.Anim * 4) : 0;
            float bob = Math.Abs(a) * 2;
            using (var fur = new SolidBrush(Color.FromArgb(255, 238, 243, 250)))
            using (var shade = new SolidBrush(Color.FromArgb(255, 200, 212, 230)))
            {
                g.FillEllipse(shade, -14 + a * 7, -16, 13, 17);
                g.FillEllipse(fur, 2 - a * 7, -16, 13, 17);
                float by = -bob;
                g.FillEllipse(fur, -24, -76 + by, 48, 66);
                for (int i = 0; i < 12; i++)
                {
                    double an = i * Math.PI * 2 / 12;
                    g.FillEllipse(fur, (float)Math.Cos(an) * 22 - 7, -43 + by + (float)Math.Sin(an) * 31 - 7, 14, 14);
                }
                // руки
                float arm = w.State == 2 ? (w.T < 0.55f ? -2.2f : 0.6f)
                          : w.State == 3 ? (float)Math.Sin(w.T * 8) * 0.6f + 0.8f
                          : w.State == 4 ? -1.4f
                          : w.Wave ? -2.6f + (float)Math.Sin(T * 11) * 0.5f      // машет курсору
                          : a * 0.5f;
                var st = g.Save();
                g.TranslateTransform(12, -55 + by); g.RotateTransform(arm * 50);
                g.FillEllipse(shade, -6, -2, 13, 34);
                g.Restore(st);
                st = g.Save();
                g.TranslateTransform(-14, -55 + by); g.RotateTransform(-a * 25);
                g.FillEllipse(shade, -6, -2, 12, 32);
                g.Restore(st);
                using (var face = new SolidBrush(Color.FromArgb(255, 140, 170, 205))) g.FillEllipse(face, -2, -70 + by, 24, 22);
                using (var b = new SolidBrush(Color.FromArgb(255, 25, 30, 45)))
                {
                    g.FillEllipse(b, 4, -64 + by, 4.5f, 5);
                    g.FillEllipse(b, 13, -64 + by, 4.5f, 5);
                }
                using (var pen = new Pen(Color.FromArgb(255, 40, 50, 70), 1.6f)) g.DrawArc(pen, 6, -59 + by, 10, 6, 10, 160);
                if (w.State == 2 && w.T < 0.55f) Art.Ball(g, 20, -95 + by, 6);
            }
            // получил снежком: над головой кружатся звёздочки
            if (w.State == 4)
                using (var br = new SolidBrush(Color.FromArgb(255, 255, 220, 70)))
                    for (int i = 0; i < 3; i++)
                    {
                        double an = T * 7 + i * Math.PI * 2 / 3;
                        float sx = (float)Math.Cos(an) * 18, sy = -90 + (float)Math.Sin(an) * 5;
                        g.FillPolygon(br, new[] { new PointF(sx, sy - 4), new PointF(sx + 1.3f, sy - 1.3f), new PointF(sx + 4, sy), new PointF(sx + 1.3f, sy + 1.3f),
                                                  new PointF(sx, sy + 4), new PointF(sx - 1.3f, sy + 1.3f), new PointF(sx - 4, sy), new PointF(sx - 1.3f, sy - 1.3f) });
                    }
        }

        void DrawBunny(Graphics g, Walker w)
        {
            float ph = w.Anim % 1f;
            float hop = w.State == 0 && ph < 0.6f ? (float)Math.Sin(ph / 0.6f * Math.PI) * 16 : 0;
            using (var fur = new SolidBrush(Color.FromArgb(255, 225, 220, 215)))
            using (var dark = new SolidBrush(Color.FromArgb(255, 190, 182, 175)))
            using (var pink = new SolidBrush(Color.FromArgb(255, 245, 175, 185)))
            {
                float y = -hop;
                g.FillEllipse(dark, -14, -10 + y, 16, 10);
                g.FillEllipse(fur, -18, -30 + y, 32, 26);
                g.FillEllipse(Brushes.White, -23, -26 + y, 10, 10);
                g.FillEllipse(fur, 6, -40 + y, 18, 17);
                var st = g.Save();
                g.TranslateTransform(12, -37 + y); g.RotateTransform(-15 + (float)Math.Sin(T * 3) * 6);
                g.FillEllipse(fur, -4, -26, 8, 26); g.FillEllipse(pink, -2, -23, 4, 19);
                g.Restore(st);
                st = g.Save();
                g.TranslateTransform(17, -37 + y); g.RotateTransform(10);
                g.FillEllipse(dark, -4, -24, 8, 24);
                g.Restore(st);
                g.FillEllipse(Brushes.Black, 17, -35 + y, 3.5f, 3.5f);
                g.FillEllipse(pink, 22, -29 + y, 3.5f, 3);
                g.FillEllipse(fur, 2, -9 + y, 10, 9);
            }
        }

        void DrawCat(Graphics g, Walker w)
        {
            float a = w.State == 0 ? (float)Math.Sin(w.Anim * 5) : 0;
            bool sit = w.State == 1;
            using (var fur = new SolidBrush(Color.FromArgb(255, 240, 150, 60)))
            using (var dark = new SolidBrush(Color.FromArgb(255, 200, 110, 40)))
            using (var pen = new Pen(Color.FromArgb(255, 240, 150, 60), 5))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                float tail = (float)Math.Sin(T * 2.5) * 6;
                g.DrawBezier(pen, -18, -18, -32, -22, -30, -40 + tail, -22, -46 + tail);
                if (sit)
                {
                    g.FillEllipse(fur, -18, -32, 30, 32);
                    g.FillEllipse(dark, 0, -8, 9, 8);
                }
                else
                {
                    using (var lp = new Pen(Color.FromArgb(255, 210, 120, 45), 4.5f))
                    {
                        lp.StartCap = LineCap.Round; lp.EndCap = LineCap.Round;
                        g.DrawLine(lp, -12, -12, -12 + a * 5, 0); g.DrawLine(lp, -6, -12, -6 - a * 5, 0);
                        g.DrawLine(lp, 8, -12, 8 - a * 5, 0); g.DrawLine(lp, 14, -12, 14 + a * 5, 0);
                    }
                    g.FillEllipse(fur, -20, -26, 42, 17);
                    using (var sp = new Pen(Color.FromArgb(255, 200, 110, 40), 2))
                        for (int i = 0; i < 3; i++) g.DrawLine(sp, -8 + i * 8, -26, -10 + i * 8, -18);
                }
                float hx = sit ? 4 : 14, hy = sit ? -48 : -38;
                g.FillEllipse(fur, hx, hy, 19, 17);
                g.FillPolygon(fur, new[] { new PointF(hx + 1, hy + 5), new PointF(hx + 3, hy - 6), new PointF(hx + 8, hy + 2) });
                g.FillPolygon(fur, new[] { new PointF(hx + 11, hy + 2), new PointF(hx + 16, hy - 6), new PointF(hx + 18, hy + 5) });
                g.FillEllipse(Brushes.Black, hx + 9, hy + 6, 3, 4);
                g.FillEllipse(Brushes.Black, hx + 14, hy + 6, 3, 4);
                using (var pk = new SolidBrush(Color.FromArgb(255, 240, 120, 130))) g.FillEllipse(pk, hx + 15.5f, hy + 10.5f, 3, 2.5f);
            }
        }

        void DrawHedgehog(Graphics g, Walker w)
        {
            float a = w.State == 0 ? (float)Math.Sin(w.Anim * 7) : 0;
            using (var feet = new SolidBrush(Color.FromArgb(255, 90, 60, 40)))
            {
                g.FillEllipse(feet, -10 + a * 3, -6, 7, 6); g.FillEllipse(feet, 6 - a * 3, -6, 7, 6);
            }
            var spikes = new List<PointF>();
            for (int i = 0; i <= 16; i++)
            {
                double an = Math.PI + i * Math.PI / 16;
                float r = i % 2 == 0 ? 22 : 15;
                spikes.Add(new PointF((float)Math.Cos(an) * r * 1.05f - 2, -5 + (float)Math.Sin(an) * r * 0.95f));
            }
            using (var br = new SolidBrush(Color.FromArgb(255, 100, 70, 45))) g.FillPolygon(br, spikes.ToArray());
            using (var br = new SolidBrush(Color.FromArgb(255, 130, 95, 60))) g.FillEllipse(br, -18, -22, 30, 18);
            // собранные листья на спине
            for (int i = 0; i < w.Carry; i++)
                DrawLeaf(g, -12 + i * 4, -22 - (i % 2) * 3, i * 47, 0.6f, i, 0, 1);
            using (var br = new SolidBrush(Color.FromArgb(255, 225, 190, 150)))
                g.FillPolygon(br, new[] { new PointF(10, -18), new PointF(26, -8), new PointF(10, -4) });
            g.FillEllipse(Brushes.Black, 24, -10, 4, 4);
            g.FillEllipse(Brushes.Black, 14, -15, 2.6f, 2.6f);
        }

        // ---------------- техника ----------------
        void DrawCleaner(Graphics g, Cleaner c)
        {
            float k = K;
            float y = c.M.W.Bottom + 1;
            var st = g.Save();
            g.TranslateTransform(c.X, y);
            g.ScaleTransform(k, k);
            Color body = Sea == Season.Winter ? Color.FromArgb(255, 245, 190, 20) : Sea == Season.Spring ? Color.FromArgb(255, 70, 170, 70) : Color.FromArgb(255, 235, 110, 30);
            // куча впереди
            float load = c.Load / k;
            float ph = Math.Min(34, (float)Math.Sqrt(load) * 1.3f), pw = Math.Min(70, 14 + (float)Math.Sqrt(load) * 2.2f);
            if (ph > 2)
            {
                if (Sea == Season.Winter)
                    using (var br = new SolidBrush(Color.FromArgb(255, 245, 248, 255))) g.FillPie(br, 30, -ph, pw, ph * 2, 180, 180);
                else
                    for (int i = 0; i < (int)(ph * 1.5f); i++)
                    {
                        float hx = Hash(i, 61), hy = Hash(i, 67);
                        float px = 32 + hx * pw * 0.9f, py = -hy * ph * (1 - Math.Abs(hx - 0.5f) * 1.4f);
                        if (Sea == Season.Spring) DrawPetal(g, px, py, i * 37, 1f, i, 0.6f);
                        else DrawLeaf(g, px, py, i * 41, 0.8f, i, i % 3 == 0 ? 1 : 0, 0.6f);
                    }
            }
            using (var br = new SolidBrush(body))
            {
                g.FillRectangle(br, -44, -36, 66, 20);
                g.FillRectangle(br, -38, -62, 30, 27);
            }
            using (var br = new SolidBrush(Color.FromArgb(255, 170, 215, 245))) g.FillRectangle(br, -34, -58, 22, 15);
            using (var br = new SolidBrush(Color.FromArgb(255, 60, 60, 65))) g.FillRectangle(br, 4, -46, 5, 12);
            bool flash = ((int)(c.Anim * 4)) % 2 == 0;
            using (var br = new SolidBrush(flash ? Color.FromArgb(255, 255, 140, 20) : Color.FromArgb(255, 170, 80, 20))) g.FillEllipse(br, -27, -70, 9, 8);
            if (flash) using (var br = new SolidBrush(Color.FromArgb(70, 255, 160, 40))) g.FillEllipse(br, -36, -78, 27, 24);
            DrawWheel(g, -26, -17, 17, c.Anim);
            DrawWheel(g, 12, -11, 11, c.Anim);
            if (Sea == Season.Winter)
                using (var br = new SolidBrush(Color.FromArgb(255, 150, 155, 165)))
                    g.FillPolygon(br, new[] { new PointF(26, -40), new PointF(34, -42), new PointF(38, 0), new PointF(26, 0) });
            else if (Sea == Season.Spring)
                using (var br = new SolidBrush(Color.FromArgb(255, 60, 120, 60))) g.FillRectangle(br, 20, -12, 18, 10);
            else
            {
                using (var br = new SolidBrush(Color.FromArgb(255, 120, 90, 60))) g.FillEllipse(br, 22, -22, 20, 20);
                using (var pen = new Pen(Color.FromArgb(255, 200, 170, 110), 1.5f))
                    for (int i = 0; i < 6; i++)
                    {
                        double a = c.Anim * 12 + i * Math.PI / 3;
                        g.DrawLine(pen, 32, -12, 32 + (float)Math.Cos(a) * 11, -12 + (float)Math.Sin(a) * 11);
                    }
            }
            g.Restore(st);
        }

        static void DrawWheel(Graphics g, float x, float y, float r, float anim)
        {
            using (var br = new SolidBrush(Color.FromArgb(255, 40, 40, 45))) g.FillEllipse(br, x - r, y - r, r * 2, r * 2);
            using (var br = new SolidBrush(Color.FromArgb(255, 150, 150, 155))) g.FillEllipse(br, x - r * 0.45f, y - r * 0.45f, r * 0.9f, r * 0.9f);
            using (var pen = new Pen(Color.FromArgb(255, 80, 80, 85), 1.5f))
            {
                double a = anim * 8;
                g.DrawLine(pen, x, y, x + (float)Math.Cos(a) * r * 0.45f, y + (float)Math.Sin(a) * r * 0.45f);
            }
        }

        // ---------------- мелочи ----------------
        void DrawButterfly(Graphics g, Butterfly f)
        {
            float k = K;
            float flap = f.Sit != null ? 0.35f + 0.65f * Math.Abs((float)Math.Sin(f.Phase * 2)) : Math.Abs((float)Math.Sin(f.Phase * 13));
            Color[][] pal =
            {
                new[] { Color.FromArgb(240, 250, 160, 40), Color.FromArgb(240, 230, 110, 30) },
                new[] { Color.FromArgb(240, 120, 170, 255), Color.FromArgb(240, 80, 120, 230) },
                new[] { Color.FromArgb(240, 255, 255, 255), Color.FromArgb(240, 230, 230, 200) },
                new[] { Color.FromArgb(240, 250, 120, 190), Color.FromArgb(240, 220, 80, 160) },
                new[] { Color.FromArgb(240, 255, 230, 70), Color.FromArgb(240, 240, 190, 40) }
            };
            var c = pal[f.C % pal.Length];
            float w = 9 * k * Math.Max(0.12f, flap);
            using (var a = new SolidBrush(c[0]))
            using (var b = new SolidBrush(c[1]))
            {
                g.FillEllipse(a, f.X - w, f.Y - 8 * k, w, 9 * k);
                g.FillEllipse(a, f.X, f.Y - 8 * k, w, 9 * k);
                g.FillEllipse(b, f.X - w * 0.8f, f.Y - 1 * k, w * 0.8f, 6 * k);
                g.FillEllipse(b, f.X, f.Y - 1 * k, w * 0.8f, 6 * k);
            }
            using (var br = new SolidBrush(Color.FromArgb(255, 50, 40, 40))) g.FillEllipse(br, f.X - 1.2f * k, f.Y - 7 * k, 2.4f * k, 12 * k);
        }

        // Клякса: неровное пятно, капля сползает вниз, в конце всё тает
        void DrawSplat(Graphics g, Splat s)
        {
            float a = s.Life < 0.08f ? s.Life / 0.08f : s.Life > 2.3f ? Math.Max(0, 1 - (s.Life - 2.3f) / 1.2f) : 1f;
            if (a <= 0) return;
            float r = s.R * (s.Life < 0.08f ? 0.6f + 5 * s.Life : 1f);
            using (var shade = new SolidBrush(Color.FromArgb((int)(150 * a), 200, 215, 235)))
            using (var br = new SolidBrush(Color.FromArgb((int)(235 * a), 250, 252, 255)))
            {
                g.FillEllipse(shade, s.X - r, s.Y - r * 0.85f + 1.5f * K, r * 2, r * 1.8f);
                g.FillEllipse(br, s.X - r, s.Y - r * 0.9f, r * 2, r * 1.8f);
                for (int i = 0; i < 7; i++)
                {
                    double an = (Hash(s.Seed, i) * 2 * Math.PI);
                    float d = r * (0.85f + Hash(s.Seed, i + 20) * 0.5f), br2 = r * (0.18f + Hash(s.Seed, i + 40) * 0.25f);
                    g.FillEllipse(br, s.X + (float)Math.Cos(an) * d - br2, s.Y + (float)Math.Sin(an) * d * 0.9f - br2, br2 * 2, br2 * 2);
                }
                // капля и мокрый след
                float dx = (Hash(s.Seed, 99) - 0.5f) * r;
                float dw = r * 0.32f;
                g.FillRectangle(br, s.X + dx - dw * 0.35f, s.Y, dw * 0.7f, s.Drip + r * 0.5f);
                g.FillEllipse(br, s.X + dx - dw / 2, s.Y + r * 0.5f + s.Drip - dw / 2, dw, dw * 1.3f);
                using (var hi = new SolidBrush(Color.FromArgb((int)(200 * a), 255, 255, 255)))
                    g.FillEllipse(hi, s.X - r * 0.45f, s.Y - r * 0.55f, r * 0.5f, r * 0.35f);
            }
        }

        void DrawMitten(Graphics g)
        {
            var st = g.Save();
            g.TranslateTransform(CursorX + 10 * K, CursorY + 14 * K);
            g.ScaleTransform(K, K);
            using (var br = new SolidBrush(Color.FromArgb(235, 210, 40, 50))) g.FillPath(br, Art.Mitten);
            using (var br = new SolidBrush(Color.FromArgb(240, 255, 255, 255))) g.FillRectangle(br, -9, 4, 18, 6);
            using (var pen = new Pen(Color.FromArgb(160, 255, 255, 255), 1.2f)) g.DrawLine(pen, -5, -7, 5, -7);
            g.Restore(st);
        }

        void DrawBolt(Graphics g, Bolt b)
        {
            float life = b.Life;
            float flash = life < 0.08f ? 1 : life < 0.18f ? 0.2f : life < 0.26f ? 0.8f : Math.Max(0, 1 - (life - 0.26f) / 0.4f) * 0.5f;
            using (var br = new SolidBrush(Color.FromArgb((int)(34 * flash), 235, 240, 255))) g.FillRectangle(br, b.M.B);
            if (flash < 0.15f) return;
            var pts = b.Pts.ToArray();
            using (var glow = new Pen(Color.FromArgb((int)(80 * flash), 190, 210, 255), 7 * K)) g.DrawLines(glow, pts);
            using (var core = new Pen(Color.FromArgb((int)(255 * flash), 255, 255, 255), 2 * K)) g.DrawLines(core, pts);
        }
    }
}
