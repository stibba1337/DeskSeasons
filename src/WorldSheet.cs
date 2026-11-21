using System.Drawing;

namespace DeskSeasons
{
    // Отладка: лист со всеми персонажами, техникой и декорациями в крупном масштабе (--sheet)
    partial class World
    {
        public void DrawSheet(Graphics g, float zoom)
        {
            var fake = new Mon { B = new Rectangle(0, 0, 4000, 4000), W = new Rectangle(0, 0, 4000, 4000), Primary = true };
            fake.Gh = new float[1100];
            var font = new Font("Segoe UI", 9);
            var label = Brushes.Yellow;

            // ряд 1: Йети в разных состояниях
            Sea = Season.Winter; SS = S.Seasons[0];
            string[] yn = { "идёт", "бросок", "лепит", "получил", "машет" };
            for (int i = 0; i < 5; i++)
            {
                var w = new Walker { M = fake, Dir = 1, Anim = 0.3f };
                if (i == 1) { w.State = 2; w.T = 0.3f; }
                if (i == 2) { w.State = 3; w.T = 0.5f; }
                if (i == 3) { w.State = 4; w.T = 0.35f; }
                if (i == 4) w.Wave = true;
                Place(g, zoom, 70 + i * 130, 130, delegate { DrawYeti(g, w); });
            }
            // ряд 2: остальные персонажи
            Season[] ss = { Season.Spring, Season.Summer, Season.Summer, Season.Autumn };
            for (int i = 0; i < 4; i++)
            {
                Sea = ss[i]; SS = S.Seasons[(int)Sea];
                var w = new Walker { M = fake, Dir = 1, Anim = 0.25f, State = i == 2 ? 1 : 0, Carry = 5 };
                Place(g, zoom, 70 + i * 130, 260, delegate
                {
                    if (Sea == Season.Spring) DrawBunny(g, w);
                    else if (Sea == Season.Summer) DrawCat(g, w);
                    else DrawHedgehog(g, w);
                });
            }
            // ряд 3: техника
            Season[] cs = { Season.Winter, Season.Spring, Season.Autumn };
            for (int i = 0; i < 3; i++)
            {
                Sea = cs[i]; SS = S.Seasons[(int)Sea];
                var c = new Cleaner { M = fake, X = 0, Load = 300, Anim = 0.1f };
                Place(g, zoom, 90 + i * 200, 390, delegate
                {
                    var m2 = new Mon { B = fake.B, W = new Rectangle(0, 0, 10, 0) };
                    c.M = m2; DrawCleaner(g, c);
                });
            }
            // ряд 4: деревья, снеговик
            for (int i = 0; i < 4; i++)
            {
                Sea = (Season)i; SS = S.Seasons[i];
                Place(g, zoom, 80 + i * 150, 620, delegate
                {
                    if (i == 0) DrawFir(g, 0, 0, 1);
                    else if (i == 1) DrawBlossom(g, 0, 0, 1, true);
                    else if (i == 2) DrawSunflowers(g, 0, 0, 1);
                    else DrawBlossom(g, 0, 0, 1, false);
                });
            }
            Sea = Season.Winter; SS = S.Seasons[0];
            var sm = new Snowman { M = fake, X = 0 };
            sm.Balls.Add(18); sm.Balls.Add(13); sm.Balls.Add(9.5f);
            for (int h = 0; h < 3; h++)
            {
                var s2 = new Snowman { M = fake, X = 0, Hat = h, Scarf = h };
                s2.Balls.AddRange(sm.Balls);
                Place(g, zoom, 690 + h * 60, 620, delegate
                {
                    fake.W = new Rectangle(-2000, -4000, 4000, 4000); // поверхность земли в точке y=0
                    DrawSnowman(g, s2);
                });
            }
        }

        delegate void Act();

        static void Place(Graphics g, float zoom, float x, float y, Act draw)
        {
            var st = g.Save();
            g.ScaleTransform(zoom, zoom);
            g.TranslateTransform(x, y);
            using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255))) g.DrawLine(pen, -60, 0, 60, 0);
            draw();
            g.Restore(st);
        }
    }
}
