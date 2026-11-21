using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DeskSeasons
{
    // Заготовки графики: спрайты, контуры листьев, иконки.
    static class Art
    {
        public static Bitmap[] Flakes;           // мягкие снежинки разного радиуса
        public static Bitmap[] BulbOn, BulbOff;  // лампочки гирлянды
        public static Bitmap[] FlowerOn, FlowerOff;
        public static Bitmap Fog;
        public static GraphicsPath MapleLeaf, OvalLeaf, Petal, Mitten;
        public static float K = 1f;

        public static readonly Color[] WinterBulbs = { Color.FromArgb(255, 60, 60), Color.FromArgb(255, 210, 60), Color.FromArgb(70, 230, 90), Color.FromArgb(70, 150, 255), Color.FromArgb(240, 90, 230) };
        public static readonly Color[] AutumnBulbs = { Color.FromArgb(255, 190, 80), Color.FromArgb(255, 150, 50), Color.FromArgb(255, 215, 120), Color.FromArgb(255, 120, 40), Color.FromArgb(255, 200, 100) };
        public static readonly Color[] FlowerColors = { Color.FromArgb(255, 170, 200), Color.FromArgb(255, 255, 255), Color.FromArgb(255, 225, 90), Color.FromArgb(200, 160, 255), Color.FromArgb(255, 130, 170) };
        public static readonly Color[] LeafColors = { Color.FromArgb(230, 90, 30), Color.FromArgb(200, 50, 30), Color.FromArgb(240, 170, 40), Color.FromArgb(170, 100, 40), Color.FromArgb(250, 200, 60), Color.FromArgb(190, 70, 50) };
        public static readonly Color[] PetalColors = { Color.FromArgb(255, 190, 210), Color.FromArgb(255, 215, 228), Color.FromArgb(250, 170, 195), Color.FromArgb(255, 235, 240) };
        public static readonly Color[] FlagColors = { Color.FromArgb(240, 70, 70), Color.FromArgb(255, 190, 40), Color.FromArgb(60, 190, 90), Color.FromArgb(60, 140, 240), Color.FromArgb(180, 90, 220) };

        public static void Init(float k)
        {
            K = k;
            Flakes = new Bitmap[8];
            for (int i = 0; i < 8; i++) Flakes[i] = SoftDot((1.2f + i * 0.75f) * k, Color.White);
            BulbOn = new Bitmap[5]; BulbOff = new Bitmap[5];
            FlowerOn = new Bitmap[5]; FlowerOff = new Bitmap[5];
            for (int i = 0; i < 5; i++)
            {
                BulbOn[i] = MakeBulb(WinterBulbs[i], true); BulbOff[i] = MakeBulb(WinterBulbs[i], false);
                FlowerOn[i] = MakeFlower(FlowerColors[i], true); FlowerOff[i] = MakeFlower(FlowerColors[i], false);
            }
            AutumnOn = new Bitmap[5]; AutumnOff = new Bitmap[5];
            for (int i = 0; i < 5; i++) { AutumnOn[i] = MakeBulb(AutumnBulbs[i], true); AutumnOff[i] = MakeBulb(AutumnBulbs[i], false); }
            Fog = MakeFog();
            MapleLeaf = MakeMaple(); OvalLeaf = MakeOvalLeaf(); Petal = MakePetal(); Mitten = MakeMitten();
        }
        public static Bitmap[] AutumnOn, AutumnOff;

        static Bitmap SoftDot(float r, Color c)
        {
            int s = (int)Math.Ceiling(r * 2 + 4);
            var b = new Bitmap(s, s, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cx = s / 2f;
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(cx - r - 1.5f, cx - r - 1.5f, (r + 1.5f) * 2, (r + 1.5f) * 2);
                    using (var br = new PathGradientBrush(p))
                    {
                        br.CenterColor = Color.FromArgb(255, c);
                        br.SurroundColors = new[] { Color.FromArgb(0, c) };
                        var blend = new Blend();
                        blend.Factors = new float[] { 0f, 0.6f, 1f, 1f };
                        blend.Positions = new float[] { 0f, 0.3f, 0.6f, 1f };
                        br.Blend = blend;
                        g.FillPath(br, p);
                    }
                }
            }
            return b;
        }

        static Bitmap MakeBulb(Color c, bool on)
        {
            float k = K;
            int w = (int)(22 * k), h = (int)(26 * k);
            var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cx = w / 2f, cy = 14 * k;
                if (on)
                {
                    using (var p = new GraphicsPath())
                    {
                        p.AddEllipse(cx - 10 * k, cy - 10 * k, 20 * k, 20 * k);
                        using (var br = new PathGradientBrush(p))
                        {
                            br.CenterColor = Color.FromArgb(150, c);
                            br.SurroundColors = new[] { Color.FromArgb(0, c) };
                            g.FillPath(br, p);
                        }
                    }
                }
                Color body = on ? Blend(c, Color.White, 0.25f) : Blend(c, Color.Black, 0.55f);
                using (var br = new SolidBrush(Color.FromArgb(on ? 255 : 220, body)))
                    g.FillEllipse(br, cx - 3.2f * k, cy - 4.5f * k, 6.4f * k, 9.5f * k);
                if (on)
                    using (var br = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                        g.FillEllipse(br, cx - 1.6f * k, cy - 3f * k, 2f * k, 3.5f * k);
                using (var br = new SolidBrush(Color.FromArgb(255, 60, 70, 60)))
                    g.FillRectangle(br, cx - 2.2f * k, cy - 7.5f * k, 4.4f * k, 3.5f * k);
            }
            return b;
        }

        static Bitmap MakeFlower(Color c, bool on)
        {
            float k = K;
            int s = (int)(20 * k);
            var b = new Bitmap(s, s, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cx = s / 2f;
                if (on)
                    using (var br = new SolidBrush(Color.FromArgb(60, c))) g.FillEllipse(br, 0, 0, s, s);
                Color pc = on ? c : Blend(c, Color.Gray, 0.35f);
                using (var br = new SolidBrush(pc))
                    for (int i = 0; i < 5; i++)
                    {
                        double a = i * Math.PI * 2 / 5 - Math.PI / 2;
                        float px = cx + (float)Math.Cos(a) * 4f * k, py = cx + (float)Math.Sin(a) * 4f * k;
                        g.FillEllipse(br, px - 3.3f * k, py - 3.3f * k, 6.6f * k, 6.6f * k);
                    }
                using (var br = new SolidBrush(Color.FromArgb(255, 240, 190, 40)))
                    g.FillEllipse(br, cx - 2.2f * k, cx - 2.2f * k, 4.4f * k, 4.4f * k);
            }
            return b;
        }

        // Туман готовится сразу в итоговом размере: растягивать полупрозрачную картинку каждый кадр очень дорого
        static Bitmap MakeFog()
        {
            int w = (int)(700 * K), h = (int)(200 * K);
            var b = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b))
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(0, 0, w, h);
                using (var br = new PathGradientBrush(p))
                {
                    br.CenterColor = Color.FromArgb(70, 235, 238, 242);
                    br.SurroundColors = new[] { Color.FromArgb(0, 235, 238, 242) };
                    g.FillPath(br, p);
                }
            }
            return b;
        }

        // Кленовый лист, нормирован примерно на радиус 10
        static GraphicsPath MakeMaple()
        {
            var pts = new List<PointF>();
            // строим симметрично: правая половина + зеркальная левая
            var half = new List<PointF>
            {
                new PointF(0, -10), new PointF(1.6f, -5.5f), new PointF(4.5f, -7.5f), new PointF(3.8f, -3f),
                new PointF(9.5f, -4.5f), new PointF(7.5f, -1f), new PointF(9f, 1.5f), new PointF(4.5f, 2.5f),
                new PointF(5.5f, 5.5f), new PointF(1.2f, 4f), new PointF(0.6f, 6f)
            };
            foreach (var p in half) pts.Add(p);
            for (int i = half.Count - 1; i >= 1; i--) pts.Add(new PointF(-half[i].X, half[i].Y));
            var gp = new GraphicsPath();
            gp.AddPolygon(pts.ToArray());
            return gp;
        }

        static GraphicsPath MakeOvalLeaf()
        {
            var gp = new GraphicsPath();
            gp.AddBezier(0, -10, 7, -6, 7, 5, 0, 9);
            gp.AddBezier(0, 9, -7, 5, -7, -6, 0, -10);
            gp.CloseFigure();
            return gp;
        }

        static GraphicsPath MakePetal()
        {
            var gp = new GraphicsPath();
            gp.AddBezier(0, 5, -6, 1, -5, -5, -1.2f, -4.2f);
            gp.AddLine(-1.2f, -4.2f, 0, -2.6f);
            gp.AddLine(0, -2.6f, 1.2f, -4.2f);
            gp.AddBezier(1.2f, -4.2f, 5, -5, 6, 1, 0, 5);
            gp.CloseFigure();
            return gp;
        }

        static GraphicsPath MakeMitten()
        {
            var gp = new GraphicsPath();
            gp.AddArc(-8, -14, 16, 18, 180, 180);
            gp.AddLine(8, -5, 8, 6);
            gp.AddLine(8, 6, -8, 6);
            gp.AddLine(-8, 6, -8, -5);
            gp.CloseFigure();
            gp.AddEllipse(-14, -6, 9, 7);
            return gp;
        }

        // ---------- готовые картинки листьев и лепестков ----------
        // Повернуть и сгладить фигуру дорого, поэтому каждое сочетание
        // (форма, цвет, угол, «переворот», размер) рисуется один раз и дальше копируется.
        static readonly Dictionary<long, Bitmap> spriteCache = new Dictionary<long, Bitmap>();
        const int Angles = 16;

        public static Bitmap LeafSprite(bool petal, int shape, int color, float rot, float flip, float scale)
        {
            int ai = ((int)Math.Round(rot / (360f / Angles)) % Angles + Angles) % Angles;
            int fi = Math.Max(1, Math.Min(4, (int)Math.Round(Math.Abs(flip) * 4)));
            int si = Math.Max(1, Math.Min(40, (int)Math.Round(scale * 5)));
            long key = ((((((petal ? 1L : 0L) * 4 + shape) * 16 + color) * Angles + ai) * 8 + fi) * 64) + si;
            Bitmap b;
            if (spriteCache.TryGetValue(key, out b)) return b;

            float s = si / 5f, fy = fi / 4f;
            int size = (int)Math.Ceiling((petal ? 12 : 22) * s) + 4;
            b = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TranslateTransform(size / 2f, size / 2f);
                g.RotateTransform(ai * (360f / Angles));
                if (petal)
                {
                    g.ScaleTransform(s, s * fy);
                    using (var br = new SolidBrush(Color.FromArgb(235, PetalColors[color % PetalColors.Length]))) g.FillPath(br, Petal);
                }
                else
                {
                    g.ScaleTransform(s * 0.9f, s * 0.9f * fy);
                    using (var br = new SolidBrush(LeafColors[color % LeafColors.Length])) g.FillPath(br, shape == 0 ? MapleLeaf : OvalLeaf);
                    using (var pen = new Pen(Color.FromArgb(120, 90, 40, 20), 0.6f)) g.DrawLine(pen, 0, -6, 0, 9);
                }
            }
            if (spriteCache.Count > 6000) { foreach (var v in spriteCache.Values) v.Dispose(); spriteCache.Clear(); }
            spriteCache[key] = b;
            return b;
        }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static void Ball(Graphics g, float x, float y, float r)
        {
            using (var br = new SolidBrush(Color.FromArgb(255, 205, 220, 238))) g.FillEllipse(br, x - r, y - r, r * 2, r * 2);
            using (var br = new SolidBrush(Color.FromArgb(255, 250, 252, 255))) g.FillEllipse(br, x - r * 0.95f, y - r, r * 1.75f, r * 1.75f);
            using (var br = new SolidBrush(Color.FromArgb(160, 255, 255, 255))) g.FillEllipse(br, x - r * 0.5f, y - r * 0.7f, r * 0.6f, r * 0.45f);
        }

        // ---------- иконки ----------
        public static Bitmap SeasonGlyph(int season, int size)
        {
            var b = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float s = size / 32f;
                g.ScaleTransform(s, s);
                Color[] bg = { Color.FromArgb(40, 120, 220), Color.FromArgb(230, 100, 160), Color.FromArgb(245, 170, 20), Color.FromArgb(220, 90, 30) };
                if (season < 0)
                {
                    // иконка программы: четыре сезона по четвертям
                    for (int i = 0; i < 4; i++)
                        using (var br = new SolidBrush(bg[i])) g.FillPie(br, 1, 1, 30, 30, -90 + i * 90, 90);
                    DrawFlake(g, 16, 16, 10.5f, 2.4f);
                }
                else
                {
                    using (var p = Rounded(1, 1, 30, 30, 8))
                    using (var br = new SolidBrush(bg[season])) g.FillPath(br, p);
                    if (season == 0) DrawFlake(g, 16, 16, 11, 2.2f);
                    else if (season == 1)
                    {
                        using (var br = new SolidBrush(Color.White))
                            for (int i = 0; i < 5; i++)
                            {
                                double a = i * Math.PI * 2 / 5 - Math.PI / 2;
                                g.FillEllipse(br, 16 + (float)Math.Cos(a) * 6.5f - 5, 16 + (float)Math.Sin(a) * 6.5f - 5, 10, 10);
                            }
                        using (var br = new SolidBrush(Color.FromArgb(255, 220, 60))) g.FillEllipse(br, 12.5f, 12.5f, 7, 7);
                    }
                    else if (season == 2)
                    {
                        using (var pen = new Pen(Color.White, 2.4f))
                            for (int i = 0; i < 8; i++)
                            {
                                double a = i * Math.PI / 4;
                                g.DrawLine(pen, 16 + (float)Math.Cos(a) * 8.5f, 16 + (float)Math.Sin(a) * 8.5f,
                                    16 + (float)Math.Cos(a) * 13f, 16 + (float)Math.Sin(a) * 13f);
                            }
                        using (var br = new SolidBrush(Color.White)) g.FillEllipse(br, 9.5f, 9.5f, 13, 13);
                    }
                    else
                    {
                        var st = g.Save();
                        g.TranslateTransform(16, 17); g.ScaleTransform(1.3f, 1.3f);
                        using (var br = new SolidBrush(Color.White)) g.FillPath(br, MapleLeaf ?? MakeMaple());
                        g.Restore(st);
                    }
                }
            }
            return b;
        }

        static void DrawFlake(Graphics g, float cx, float cy, float r, float w)
        {
            using (var pen = new Pen(Color.White, w))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3 - Math.PI / 2;
                    float ex = cx + (float)Math.Cos(a) * r, ey = cy + (float)Math.Sin(a) * r;
                    g.DrawLine(pen, cx, cy, ex, ey);
                    float mx = cx + (float)Math.Cos(a) * r * 0.58f, my = cy + (float)Math.Sin(a) * r * 0.58f;
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        double b2 = a + sgn * 0.75;
                        g.DrawLine(pen, mx, my, mx + (float)Math.Cos(b2) * r * 0.35f, my + (float)Math.Sin(b2) * r * 0.35f);
                    }
                }
            }
        }

        public static GraphicsPath Rounded(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            p.AddArc(x, y, r * 2, r * 2, 180, 90);
            p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Icon MakeIcon(int season)
        {
            using (var b = SeasonGlyph(season, 32))
            {
                IntPtr h = b.GetHicon();
                var tmp = Icon.FromHandle(h);
                var ic = (Icon)tmp.Clone();
                tmp.Dispose();
                Native.DestroyIcon(h);
                return ic;
            }
        }

        // Пишет .ico (PNG внутри) — используется при сборке для иконки exe
        public static void ExportIco(string path)
        {
            int[] sizes = { 256, 64, 48, 32, 16 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (var b = SeasonGlyph(-1, s))
                using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }
}
