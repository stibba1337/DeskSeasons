using System;
using System.Collections.Generic;
using System.Drawing;

namespace DeskSeasons
{
    partial class World
    {
        // ---------------- смахивание курсором / варежкой ----------------
        void BrushAt(float cx, float cy, bool mitten)
        {
            float speed = (float)Math.Sqrt(CurVX * CurVX + CurVY * CurVY);
            if (Held != null) return;
            if (!mitten && speed < 60 * K) return;
            float rad = (mitten ? 24 : 9) * K;
            int made = 0;
            foreach (var p in Z)
            {
                if (!p.Decor || p.Segs.Count == 0) continue;
                if (cx < p.R.Left - rad || cx > p.R.Right + rad) continue;
                if (cy < p.R.Top - PileMax - rad || cy > p.R.Top + rad * 0.6f) continue;
                made += BrushArray(p.Hh, p.R.Left, p.R.Top, cx, cy, rad, mitten, p, made);
                made += BrushItems(p.Items, p.R.Left, p.R.Top, cx, cy, rad, p);
            }
            foreach (var m in Mons)
            {
                if (cx < m.W.Left || cx >= m.W.Right) continue;
                if (cy < m.W.Bottom - GroundMax - rad || cy > m.W.Bottom + rad) continue;
                made += BrushArray(m.Gh, m.W.Left, m.W.Bottom, cx, cy, rad, mitten, null, made);
                made += BrushItems(m.Items, m.W.Left, m.W.Bottom, cx, cy, rad, null);
            }
        }

        int BrushArray(float[] hh, float left, float baseY, float cx, float cy, float rad, bool mitten, Pile p, int made)
        {
            int c0 = Math.Max(0, (int)((cx - rad - left) / CW)), c1 = Math.Min(hh.Length - 1, (int)((cx + rad - left) / CW));
            int cnt = 0;
            for (int c = c0; c <= c1; c++)
            {
                float h = hh[c];
                if (h < 0.4f) continue;
                float x = left + c * CW;
                if (p != null && !InSegs(p, x)) continue;
                float surf = baseY - h;
                if (cy < surf - rad * 0.6f) continue;
                float take = mitten ? h : Math.Min(h, h * 0.45f + 0.4f);
                hh[c] -= take;
                // снежная пыль — только зимой; в другие сезоны высота лишь служит для укладки листьев/лепестков
                if (SnowSeason && made + cnt < 40 && R01() < take / (2.2f * K))
                {
                    cnt++;
                    AddSnowPuff(x, surf + R01() * h * 0.5f, CurVX * 0.25f + Rng(-40, 40) * K, -Rng(40, 160) * K);
                }
            }
            return cnt;
        }

        int BrushItems(ItemList items, float left, float baseY, float cx, float cy, float rad, Pile p)
        {
            int cnt = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                float x = left + it.X, y = baseY - it.Y;
                float dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy > rad * rad * 1.6f) continue;
                if (p != null && !InSegs(p, x)) continue;
                items.RemoveAt(i);
                ItemToParticle(it, x, y, CurVX * 0.3f, -Rng(60, 180) * K);
                cnt++;
            }
            if (cnt > 0)
            {
                // пересчёт высоты после смахивания
                float[] hh = p != null ? p.Hh : null;
                if (hh == null) foreach (var m in Mons) if (m.Items == items) hh = m.Gh;
                if (hh != null)
                {
                    int c0 = Math.Max(0, (int)((cx - rad - left) / CW)), c1 = Math.Min(hh.Length - 1, (int)((cx + rad - left) / CW));
                    for (int c = c0; c <= c1; c++) hh[c] *= 0.3f;
                }
            }
            return cnt;
        }

        // ---------------- снежки и снеговики ----------------
        float SurfaceGround(Mon m, float x)
        {
            int col = (int)((x - m.W.Left) / CW);
            if (col < 0) col = 0; if (col >= m.Gh.Length) col = m.Gh.Length - 1;
            return m.W.Bottom - m.Gh[col];
        }

        public Mon MonAt(float x, float y)
        {
            foreach (var m in Mons) if (m.B.Contains((int)x, (int)y)) return m;
            return null;
        }

        public List<PointF> BallCenters(Snowman sm, out List<float> radii)
        {
            var res = new List<PointF>();
            radii = sm.Balls;
            float y = SurfaceGround(sm.M, sm.X) + 2 * K;
            foreach (var r in sm.Balls)
            {
                float cy = y - r;
                res.Add(new PointF(sm.X, cy));
                y = cy - r * 0.72f;
            }
            return res;
        }

        // Нажатие с клавишей-модификатором: можно ли взять снег под курсором
        public bool TryGrab(int x, int y)
        {
            if (!CanSnowball || Held != null) return false;
            // взять верхний шар снеговика
            foreach (var sm in Snowmen)
            {
                List<float> rr; var cs = BallCenters(sm, out rr);
                for (int i = cs.Count - 1; i >= 0; i--)
                {
                    float dx = cs[i].X - x, dy = cs[i].Y - y;
                    if (dx * dx + dy * dy <= rr[i] * rr[i] * 1.2f)
                    {
                        Held = new Ball { X = x, Y = y, R = sm.Balls[sm.Balls.Count - 1] };
                        sm.Balls.RemoveAt(sm.Balls.Count - 1);
                        if (sm.Balls.Count == 0) Snowmen.Remove(sm);
                        return true;
                    }
                }
            }
            float tol = 9 * K;
            foreach (var p in Z)
            {
                if (!p.Decor || x < p.R.Left || x >= p.R.Right || !InSegs(p, x)) continue;
                int col = (int)((x - p.R.Left) / CW);
                if (col < 0 || col >= p.Hh.Length || p.Hh[col] < 2.5f * K) continue;
                if (y >= p.R.Top - p.Hh[col] - tol && y <= p.R.Top + tol)
                { Held = new Ball { X = x, Y = y, R = 5 * K }; return true; }
            }
            foreach (var m in Mons)
            {
                if (x < m.W.Left || x >= m.W.Right) continue;
                int col = (int)((x - m.W.Left) / CW);
                if (col < 0 || col >= m.Gh.Length || m.Gh[col] < 2.5f * K) continue;
                if (y >= m.W.Bottom - m.Gh[col] - tol && y <= m.W.Bottom + tol)
                { Held = new Ball { X = x, Y = y, R = 5 * K }; return true; }
            }
            return false;
        }

        public void ReleaseHeld()
        {
            if (Held == null) return;
            var b = Held; Held = null;
            float max = 2600 * K;
            b.VX = Math.Max(-max, Math.Min(max, CurVX * 0.9f));
            b.VY = Math.Max(-max, Math.Min(max, CurVY * 0.9f));
            Balls.Add(b);
        }

        void GrowHeld(float dt)
        {
            var b = Held;
            b.X = CursorX; b.Y = CursorY;
            float taken = 0;
            foreach (var p in Z)
                if (p.Decor && p.Segs.Count > 0) taken += Take(p.Hh, p.R.Left, p.R.Top, b, dt, p);
            foreach (var m in Mons) taken += Take(m.Gh, m.W.Left, m.W.Bottom, b, dt, null);
            if (taken > 0)
            {
                float area = (float)Math.PI * b.R * b.R + taken * CW * 1.6f;
                b.R = Math.Min(34 * K, (float)Math.Sqrt(area / Math.PI));
            }
        }

        float Take(float[] hh, float left, float baseY, Ball b, float dt, Pile p)
        {
            if (b.X < left - b.R || b.X > left + hh.Length * CW + b.R) return 0;
            int c0 = Math.Max(0, (int)((b.X - b.R - left) / CW)), c1 = Math.Min(hh.Length - 1, (int)((b.X + b.R - left) / CW));
            float sum = 0;
            for (int c = c0; c <= c1; c++)
            {
                float surf = baseY - hh[c];
                if (hh[c] < 0.3f || b.Y + b.R < surf - 2 || b.Y - b.R > baseY + 2) continue;
                if (p != null && !InSegs(p, left + c * CW)) continue;
                float t = Math.Min(hh[c], 45 * K * dt);
                hh[c] -= t; sum += t;
            }
            return sum;
        }

        void UpdateBalls(float dt)
        {
            if (Held != null) GrowHeld(dt);
            float g = 1500 * K;
            for (int i = Balls.Count - 1; i >= 0; i--)
            {
                var b = Balls[i];
                float py = b.Y;
                b.VY += g * dt; b.X += b.VX * dt; b.Y += b.VY * dt;
                b.Age += dt;
                float speed = (float)Math.Sqrt(b.VX * b.VX + b.VY * b.VY);
                bool done = false;

                // снежок, брошенный «в экран», долетел до точки прицела
                if (b.Splat && b.Age >= b.Flight) { AddSplat(b.X, b.Y, b.R); Balls.RemoveAt(i); continue; }

                // попадание в Йети (своими снежками он в себя не попадает)
                if (b.Owner != 1 && Walker != null && Sea == Season.Winter && b.Age > 0.05f)
                {
                    float wy = SurfaceGround(Walker.M, Walker.X) - 42 * K;
                    float dx = Walker.X - b.X, dy = wy - b.Y, rs = 30 * K + b.R;
                    if (dx * dx + dy * dy < rs * rs)
                    {
                        Burst(b.X, b.Y, b.R * 0.7f);
                        HitWalker(Walker, b);
                        Balls.RemoveAt(i); continue;
                    }
                }

                // попадание в снеговика
                foreach (var sm in Snowmen)
                {
                    if (sm == b.From) continue;
                    List<float> rr; var cs = BallCenters(sm, out rr);
                    for (int j = 0; j < cs.Count && !done; j++)
                    {
                        float dx = cs[j].X - b.X, dy = cs[j].Y - b.Y, rs = rr[j] + b.R;
                        if (dx * dx + dy * dy > rs * rs) continue;
                        if (b.Owner == 1 || (b.Owner == 0 && speed > 420 * K))
                        { BurstSnowman(sm); Snowmen.Remove(sm); Burst(b.X, b.Y, b.R); }
                        else if (b.Owner == 0 && j == cs.Count - 1 && !sm.Complete && b.VY > 0)
                        { AddBall(sm, b.R); }
                        else
                        {
                            Burst(b.X, b.Y, b.R);
                            // лёгкое попадание игрока — снеговик обижается и отвечает в курсор
                            if (b.Owner == 0 && sm.Complete) { sm.Anger = 2; sm.Cd = 0.5f; }
                        }
                        done = true;
                    }
                    if (done) break;
                }
                // кромки окон
                if (!done && b.VY > 0)
                    foreach (var p in Z)
                    {
                        if (!p.Decor || b.X < p.R.Left || b.X >= p.R.Right) continue;
                        int col = (int)((b.X - p.R.Left) / CW);
                        if (col < 0 || col >= p.Hh.Length) continue;
                        float surf = p.R.Top - p.Hh[col];
                        if (py + b.R <= surf + 1 && b.Y + b.R >= surf && InSegs(p, b.X))
                        {
                            Burst(b.X, surf - b.R, b.R);
                            done = true; break;
                        }
                    }
                // земля
                if (!done && b.VY > 0)
                {
                    var m = MonAt(b.X, Math.Min(b.Y, py));
                    if (m != null && b.X >= m.W.Left && b.X < m.W.Right)
                    {
                        float surf = SurfaceGround(m, b.X);
                        if (py + b.R <= surf + 2 && b.Y + b.R >= surf)
                        {
                            if (b.Owner == 0 && speed < 650 * K && b.R >= 8 * K) LandBall(m, b);
                            else Burst(b.X, surf - b.R, b.R);
                            done = true;
                        }
                    }
                }
                if (!done && (b.Y > 20000 || MonAt(b.X, b.Y) == null && b.Y > -5000 && !AnyMonX(b.X))) done = true;
                if (done) Balls.RemoveAt(i);
            }
        }

        // Бросок по параболе: снежок окажется в точке (tx, ty) через время t
        Ball Throw(float x0, float y0, float tx, float ty, float t, int owner, bool splat, Snowman from)
        {
            float g = 1500 * K;
            var b = new Ball { X = x0, Y = y0, R = 6 * K, Owner = owner, FromYeti = owner == 1, Splat = splat, Flight = t, From = from };
            b.VX = (tx - x0) / t;
            b.VY = (ty - y0 - 0.5f * g * t * t) / t;
            Balls.Add(b);
            return b;
        }

        void AddSplat(float x, float y, float r)
        {
            if (Splats.Count > 24) Splats.RemoveAt(0);
            Splats.Add(new Splat { X = x, Y = y, R = Math.Max(r, 6 * K) * 2.8f, Seed = rnd.Next(1000) });
            for (int i = 0; i < 6; i++) AddSnowPuff(x, y, Rng(-120, 120) * K, -Rng(20, 140) * K);
        }

        void UpdateSplats(float dt)
        {
            for (int i = Splats.Count - 1; i >= 0; i--)
            {
                var s = Splats[i];
                s.Life += dt;
                s.Drip = Math.Min(s.R * 2.5f, s.Drip + dt * 14 * K);
                if (s.Life > 3.5f) Splats.RemoveAt(i);
            }
        }

        // Йети получил снежком: пошатнулся, рассердился и отвечает
        void HitWalker(Walker w, Ball b)
        {
            w.State = 4; w.T = 0;
            w.Dir = b.VX > 0 ? -1 : 1;              // поворачивается туда, откуда прилетело
            if (b.Owner == 2 && b.From != null && Snowmen.Contains(b.From))
            { w.Target = b.From; w.Thrown.Remove(b.From); w.Anger = Math.Max(w.Anger, 1); }
            else w.Anger = 3;
            w.ThrowCd = 0.9f;
        }

        // Снеговики кидаются в Йети, а иногда и в курсор
        void UpdateSnowmen(float dt)
        {
            if (!Fights) return;
            foreach (var sm in Snowmen)
            {
                if (!sm.Complete) continue;
                sm.ThrowT -= dt;
                sm.Cd -= dt;
                if (sm.Cd > 0) continue;
                List<float> rr; var cs = BallCenters(sm, out rr);
                var mid = cs[1]; float mr = rr[1];
                float hx = mid.X + mr * 1.6f, hy = mid.Y - mr * 2f;
                bool thrown = false;
                if (Walker != null && Walker.M == sm.M && Math.Abs(Walker.X - sm.X) < 700 * K && Math.Abs(Walker.X - sm.X) > 60 * K && R01() < 0.6f)
                {
                    Throw(hx, hy, Walker.X, SurfaceGround(Walker.M, Walker.X) - 45 * K, 0.75f, 2, false, sm);
                    thrown = true;
                }
                else if (sm.M.B.Contains((int)CursorX, (int)CursorY))
                {
                    float dx = CursorX - sm.X, dy = CursorY - cs[2].Y;
                    bool near = dx * dx + dy * dy < 450 * K * 450 * K;
                    if (sm.Anger > 0 || (near && R01() < 0.3f))
                    {
                        Throw(hx, hy, CursorX, CursorY, 0.8f, 2, true, sm);
                        if (sm.Anger > 0) sm.Anger--;
                        thrown = true;
                    }
                }
                if (thrown) sm.ThrowT = 0.35f;
                sm.Cd = sm.Anger > 0 ? 1.1f : Rng(3, 7);
            }
        }

        bool AnyMonX(float x)
        {
            foreach (var m in Mons) if (x >= m.B.Left - 200 && x <= m.B.Right + 200) return true;
            return false;
        }

        void LandBall(Mon m, Ball b)
        {
            foreach (var sm in Snowmen)
            {
                if (sm.M != m || sm.Complete) continue;
                float top = sm.Balls[sm.Balls.Count - 1];
                if (Math.Abs(sm.X - b.X) < top * 1.1f) { AddBall(sm, b.R); return; }
            }
            if (Snowmen.Count >= 8) { Burst(b.X, b.Y, b.R); return; }
            var n = new Snowman { M = m, X = b.X };
            n.Balls.Add(b.R);
            Snowmen.Add(n);
        }

        void AddBall(Snowman sm, float r)
        {
            float top = sm.Balls.Count > 0 ? sm.Balls[sm.Balls.Count - 1] : r;
            sm.Balls.Add(Math.Max(5 * K, Math.Min(r, top * 0.85f)));
            if (sm.Complete) { sm.Hat = rnd.Next(3); sm.Scarf = rnd.Next(4); }
        }

        void Burst(float x, float y, float r)
        {
            int n = Math.Min(50, (int)(r / K * 1.6f) + 4);
            for (int i = 0; i < n; i++)
            {
                double a = -Math.PI * R01();
                float sp = Rng(60, 260) * K;
                AddSnowPuff(x + Rng(-r, r) * 0.5f, y + Rng(-r, r) * 0.5f, (float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp);
            }
        }

        void BurstSnowman(Snowman sm)
        {
            List<float> rr; var cs = BallCenters(sm, out rr);
            for (int i = 0; i < cs.Count; i++) Burst(cs[i].X, cs[i].Y, rr[i]);
        }

        public void BuildSnowman()
        {
            if (Sea != Season.Winter) return;
            var m = PickMon();
            if (m == null) return;
            float x = m.W.Left + Rng(0.15f, 0.7f) * m.W.Width;
            var sm = new Snowman { M = m, X = x };
            sm.Balls.Add(Rng(16, 20) * K); sm.Balls.Add(sm.Balls[0] * 0.74f); sm.Balls.Add(sm.Balls[0] * 0.52f);
            sm.Hat = rnd.Next(3); sm.Scarf = rnd.Next(4);
            Snowmen.Add(sm);
            if (Snowmen.Count > 8) Snowmen.RemoveAt(0);
        }

        Mon PickMon()
        {
            Mon best = null;
            foreach (var m in Mons) if (!m.Hidden && (best == null || m.Primary)) best = m;
            return best;
        }

        // ---------------- персонаж ----------------
        public void SummonWalker()
        {
            var m = PickMon();
            if (m == null) return;
            var w = new Walker { M = m, Dir = R01() < 0.5 ? 1 : -1 };
            w.X = w.Dir > 0 ? m.W.Left - 70 * K : m.W.Right + 70 * K;
            Walker = w;
        }

        float WalkSpeed { get { return (Sea == Season.Winter ? 55 : Sea == Season.Spring ? 95 : Sea == Season.Summer ? 60 : 32) * K; } }

        void UpdateWalker(float dt)
        {
            if (Walker == null)
            {
                if (SS.Character && T > nextWalker) { SummonWalker(); ScheduleWalker(false); }
                return;
            }
            var w = Walker;
            w.T += dt;
            var m = w.M;
            if (!Mons.Contains(m)) { Walker = null; return; }
            if (w.State == 0)
            {
                // курсор рядом — персонаж останавливается, Йети машет рукой
                float cy = SurfaceGround(m, w.X) - 40 * K;
                float hdx = CursorX - w.X, hdy = CursorY - cy;
                w.Wave = hdx * hdx + hdy * hdy < 75 * K * 75 * K;

                if (Sea == Season.Winter && Fights && w.Anger > 0)
                {
                    // перестрелка: стоит на месте и отвечает, пока не «остынет»
                    w.ThrowCd -= dt;
                    if (w.ThrowCd <= 0)
                    {
                        bool atSnowman = w.Target != null && Snowmen.Contains(w.Target);
                        w.AimCursor = !atSnowman;
                        w.State = 2; w.T = 0; w.Throws = 0;
                        w.Anger--; w.ThrowCd = 1.3f;
                    }
                    return;
                }
                if (w.Wave) return;

                bool hop = Sea == Season.Spring;
                float sp = WalkSpeed;
                if (hop) { float ph = (w.Anim % 1f); sp *= ph < 0.6f ? 1.4f : 0.05f; }
                w.X += w.Dir * sp * dt;
                w.Anim += dt * (hop ? 1.6f : 2.2f);

                if (Sea == Season.Winter) YetiThink(w, dt);
                else if (Sea == Season.Autumn) HedgehogPick(w);
                else if (R01() < dt * 0.06f && InsideW(w, 100)) { w.State = 1; w.T = 0; }

                if ((w.Dir > 0 && w.X > m.W.Right + 90 * K) || (w.Dir < 0 && w.X < m.W.Left - 90 * K))
                    Walker = null;
            }
            else if (w.State == 1) // пауза
            {
                if (w.T > 2.5f) { w.State = 0; w.T = 0; }
            }
            else if (w.State == 2) // бросок снежка
            {
                bool hasTarget = w.Target != null && Snowmen.Contains(w.Target);
                // разворачивается к цели ещё во время замаха
                float aimX = w.AimCursor ? CursorX : hasTarget ? w.Target.X : w.X + w.Dir;
                if (w.T < 0.5f && Math.Abs(aimX - w.X) > 5) w.Dir = aimX > w.X ? 1 : -1;
                if (w.Throws == 0 && w.T > 0.55f)
                {
                    float x0 = w.X + w.Dir * 14 * K, y0 = SurfaceGround(m, w.X) - 95 * K;
                    if (w.AimCursor)
                        Throw(x0, y0, CursorX, CursorY, 0.8f, 1, true, null);
                    else if (hasTarget)
                    {
                        List<float> rr; var cs = BallCenters(w.Target, out rr);
                        var c = cs[cs.Count - 1];
                        Throw(x0, y0, c.X, c.Y, 0.75f, 1, false, null);
                    }
                    w.Throws = 1;
                }
                if (w.T > 1.2f)
                {
                    w.State = 0; w.T = 0; w.Throws = 0; w.AimCursor = false;
                    if (w.Anger <= 0) w.Target = null;
                }
            }
            else if (w.State == 4) // получил снежком — пошатывается
            {
                if (w.T > 0.75f) { w.State = 0; w.T = 0; }
            }
            else if (w.State == 3) // лепит снеговика
            {
                var sm = w.Own;
                if (sm == null || !Snowmen.Contains(sm)) { w.State = 0; return; }
                int need = w.T > 3.2f ? 3 : w.T > 2.1f ? 2 : w.T > 1f ? 1 : 0;
                while (sm.Balls.Count < need)
                {
                    float r0 = 17 * K;
                    float r = sm.Balls.Count == 0 ? r0 : sm.Balls.Count == 1 ? r0 * 0.74f : r0 * 0.52f;
                    sm.Balls.Add(r);
                    if (sm.Complete) { sm.Hat = rnd.Next(3); sm.Scarf = rnd.Next(4); }
                }
                if (w.T > 4.2f) { w.State = 0; w.T = 0; }
            }
        }

        bool InsideW(Walker w, float margin)
        {
            return w.X > w.M.W.Left + margin * K && w.X < w.M.W.Right - margin * K;
        }

        void YetiThink(Walker w, float dt)
        {
            // иногда сам начинает кидаться в курсор, если тот неподалёку
            if (Fights && w.CursorThrows < 3 && w.M.B.Contains((int)CursorX, (int)CursorY) &&
                Math.Abs(CursorX - w.X) < 520 * K && Math.Abs(CursorX - w.X) > 60 * K && R01() < dt * 0.3f)
            {
                w.State = 2; w.T = 0; w.Throws = 0; w.AimCursor = true; w.CursorThrows++;
                return;
            }
            foreach (var sm in Snowmen)
            {
                if (sm.M != w.M || sm == w.Own || w.Thrown.Contains(sm) || sm.Balls.Count == 0) continue;
                float d = (sm.X - w.X) * w.Dir;
                if (d > 140 * K && d < 420 * K) { w.State = 2; w.T = 0; w.Target = sm; w.Thrown.Add(sm); return; }
            }
            if (!w.Built && InsideW(w, 160) && R01() < 0.004f && Snowmen.Count < 7)
            {
                foreach (var sm in Snowmen) if (sm.M == w.M && Math.Abs(sm.X - w.X) < 150 * K) return;
                var own = new Snowman { M = w.M, X = w.X + w.Dir * 46 * K };
                Snowmen.Add(own);
                w.Own = own; w.Built = true; w.State = 3; w.T = 0;
            }
        }

        void HedgehogPick(Walker w)
        {
            if (w.Carry >= 7) return;
            var m = w.M;
            for (int i = m.Items.Count - 1; i >= 0 && w.Carry < 7; i--)
            {
                float x = m.W.Left + m.Items[i].X;
                if (Math.Abs(x - w.X) < 8 * K && R01() < 0.5f)
                {
                    m.Items.RemoveAt(i); w.Carry++;
                    int col = (int)((x - m.W.Left) / CW);
                    if (col >= 0 && col < m.Gh.Length) m.Gh[col] *= 0.5f;
                }
            }
        }

        // ---------------- уборочная техника ----------------
        public bool HasCleaner { get { return Sea != Season.Summer; } }

        public void SummonCleaner(Mon m)
        {
            if (!HasCleaner || Cleaner != null) return;
            if (m == null) m = PickMon();
            if (m == null) return;
            Cleaner = new Cleaner { M = m, X = m.W.Left - 70 * K };
        }

        void UpdateCleaner(float dt)
        {
            fillCheck += dt;
            if (Cleaner == null && SS.Cleaner && HasCleaner && fillCheck > 2)
            {
                fillCheck = 0;
                foreach (var m in Mons)
                {
                    if (m.Hidden) continue;
                    float sum = 0; foreach (var h in m.Gh) sum += h;
                    m.Fill = sum / m.Gh.Length / GroundMax;
                    if (m.Fill > 0.55f || m.Items.Count > 600) { SummonCleaner(m); break; }
                }
            }
            if (Cleaner == null) return;
            var c = Cleaner;
            if (!Mons.Contains(c.M)) { Cleaner = null; return; }
            c.X += 150 * K * dt; c.Anim += dt;
            float front = c.X + 38 * K;
            var mm = c.M;
            int c1 = Math.Min(mm.Gh.Length - 1, (int)((front - mm.W.Left) / CW));
            for (int col = Math.Max(0, c1 - 60); col <= c1; col++) { c.Load += mm.Gh[col]; mm.Gh[col] = 0; }
            for (int i = mm.Items.Count - 1; i >= 0; i--)
                if (mm.W.Left + mm.Items[i].X < front) { mm.Items.RemoveAt(i); c.Load += 1.5f * K; }
            if (c.X > mm.W.Right + 200 * K) Cleaner = null;
        }

        // ---------------- бабочки ----------------
        void UpdateFlies(float dt)
        {
            bool want = SS.Extra && (Sea == Season.Spring || Sea == Season.Summer);
            int target = want ? (Sea == Season.Summer ? 5 : 4) : 0;
            while (Flies.Count > target) Flies.RemoveAt(Flies.Count - 1);
            while (Flies.Count < target)
            {
                var m = PickMon(); if (m == null) break;
                var f = new Butterfly { X = m.W.Left + R01() * m.W.Width, Y = m.W.Top + R01() * m.W.Height, C = rnd.Next(5), Phase = R01() * 10 };
                f.TX = f.X; f.TY = f.Y;
                Flies.Add(f);
            }
            foreach (var f in Flies)
            {
                f.Phase += dt;
                if (f.Sit != null)
                {
                    if (!Piles.ContainsKey(f.Sit.H) || !f.Sit.Seen || f.Sit.R != f.Sit.Prev || (f.SitT -= dt) <= 0)
                    { f.Sit = null; f.VY = -60 * K; NewTarget(f); }
                    else { f.X = f.Sit.R.Left + f.SitX; f.Y = f.Sit.R.Top - 3 * K; }
                    continue;
                }
                float dx = f.TX - f.X, dy = f.TY - f.Y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy) + 0.01f;
                f.VX += (dx / d * 160 * K + (float)Math.Sin(f.Phase * 3.1) * 90 * K) * dt;
                f.VY += (dy / d * 160 * K + (float)Math.Cos(f.Phase * 4.3) * 110 * K) * dt;
                float sp = (float)Math.Sqrt(f.VX * f.VX + f.VY * f.VY), max = 95 * K;
                if (sp > max) { f.VX *= max / sp; f.VY *= max / sp; }
                f.X += f.VX * dt; f.Y += f.VY * dt;
                if (d < 14 * K)
                {
                    if (f.ToSit && f.Sit == null)
                    {
                        foreach (var p in Z)
                            if (p.Decor && Math.Abs(p.R.Top - f.Y) < 20 * K && f.X >= p.R.Left && f.X < p.R.Right && InSegs(p, f.X))
                            { f.Sit = p; f.SitX = f.X - p.R.Left; f.SitT = Rng(3, 8); break; }
                    }
                    NewTarget(f);
                }
            }
        }

        void NewTarget(Butterfly f)
        {
            f.ToSit = false;
            if (R01() < 0.35f && Z.Count > 0)
            {
                var p = Z[rnd.Next(Z.Count)];
                if (p.Decor && p.Segs.Count > 0)
                {
                    int s = rnd.Next(p.Segs.Count / 2) * 2;
                    f.TX = Rng(p.Segs[s], p.Segs[s + 1]); f.TY = p.R.Top - 3 * K; f.ToSit = true; return;
                }
            }
            var m = MonAt(f.X, f.Y) ?? PickMon();
            if (m == null) return;
            f.TX = Math.Max(m.W.Left + 20, Math.Min(m.W.Right - 20, f.X + Rng(-500, 500) * K));
            f.TY = Math.Max(m.W.Top + 20, Math.Min(m.W.Bottom - 30 * K, f.Y + Rng(-300, 300) * K));
        }

        // ---------------- молнии ----------------
        void UpdateBolt(float dt)
        {
            bool storm = Sea == Season.Summer && SS.Weather == 2 && SS.Extra2;
            if (Bolt != null) { Bolt.Life += dt; if (Bolt.Life > 0.7f) Bolt = null; }
            if (!storm) { nextBolt = T + Rng(3, 8); return; }
            if (Bolt == null && T > nextBolt)
            {
                nextBolt = T + Rng(6, 16);
                var m = PickMon(); if (m == null) return;
                var b = new Bolt { M = m };
                float x = m.B.Left + Rng(0.1f, 0.9f) * m.B.Width, y = m.B.Top;
                float endY = m.B.Top + m.B.Height * Rng(0.35f, 0.65f);
                b.Pts.Add(new PointF(x, y));
                while (y < endY) { y += Rng(18, 45) * K; x += Rng(-30, 30) * K; b.Pts.Add(new PointF(x, y)); }
                Bolt = b;
            }
        }

        // ---------------- декорация в углу ----------------
        public bool TreePos(out Mon m, out float x, out float baseY)
        {
            m = null; x = 0; baseY = 0;
            foreach (var mm in Mons) if (mm.Primary && !mm.Hidden) m = mm;
            if (m == null) return false;
            x = SS.TreeSide == 0 ? m.W.Right - 130 * K : m.W.Left + 130 * K;
            baseY = m.W.Bottom;
            return true;
        }

        bool TreeCrown(out RectangleF crown, out Mon m)
        {
            float x, by;
            crown = RectangleF.Empty;
            if (!TreePos(out m, out x, out by)) return false;
            crown = new RectangleF(x - 70 * K, by - 175 * K, 140 * K, 90 * K);
            return true;
        }
    }
}
