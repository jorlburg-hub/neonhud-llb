using System;
using System.Collections.Generic;

namespace NeonHud.Core
{
    public sealed partial class HudPainter
    {
        public const float BaseWidth = 1240f;
        public const float TopBand = 50f;
        public const float BaseHeight = 232f + TopBand;

        private ICanvas c;
        private HudState s;
        private HudTheme th;
        private float ox, oy, k, op;

        private readonly string[] _log;
        private readonly float[] _barStart, _barWidth;
        private readonly float _barUnits;

        private string _measuredSong;
        private float _measuredK = -1f, _songWidth;

        private static readonly string[] TierNames = { "NO LINK", "> CRUISE", "> HEATING", "> OVERDRIVE", "!! CRITICAL !!" };

        public HudPainter()
        {
            var rng = new Random(1337);
            string[] ops = { "VEL.SAMPLE", "SYNC.FRAME", "NET.RX", "BALL.VEC", "HIT.REG", "SPIN.CALC",
                             "GRID.SCAN", "BUFFER", "PARRY.WIN", "SIG.LOCK", "HEAT.SINK", "COLL.TEST" };
            _log = new string[40];
            for (int i = 0; i < _log.Length; i++)
            {
                _log[i] = string.Format("0x{0:X4}  {1,-11} {2:0.0000}  {3}",
                    rng.Next(0x1000, 0xFFFF), ops[rng.Next(ops.Length)], rng.NextDouble(),
                    rng.Next(9) == 0 ? "WARN" : "OK");
            }

            var starts = new List<float>();
            var widths = new List<float>();
            var brng = new Random(42);
            int pos = 0;
            while (pos < 90)
            {
                int w = brng.Next(1, 4);
                starts.Add(pos);
                widths.Add(w);
                pos += w + brng.Next(1, 3);
            }
            _barStart = starts.ToArray();
            _barWidth = widths.ToArray();
            _barUnits = pos;
        }

        public void Paint(ICanvas canvas, HudState state, HudTheme theme, float originX, float originY, float scale)
        {
            c = canvas; s = state; th = theme;
            k = scale; op = M.Clamp01(theme.Opacity);
            if (theme.Compact)
            {
                ox = originX; oy = originY;
                PaintCompact(s.Time);
                return;
            }
            ox = originX; oy = originY + TopBand * scale;

            float t = s.Time;
            float hitAge = t - s.SpeedChangedAt;
            float flash = M.Clamp01(1f - hitAge / 0.35f);
            bool glitch = th.Glitch && (hitAge < 0.18f || M.Hash01((uint)(t * 5f) + 911u) < 0.015f);
            int tier = Tier();
            Col tierCol = TierColour(tier);

            DrawLeft(t);
            DrawRight(t);
            DrawCore(t, tier, tierCol, flash, glitch, hitAge);
            DrawFooter(t);
            if (th.ShowRelease) DrawRelease(t);

            if (th.Scanlines)
            {
                Col sl = new Col(0f, 0f, 0f, 0.22f * op);
                float p = Math.Max(2f, 3f * k);
                if (th.ShowRelease) c.Scanlines(R(470, -46, 300, 40), p, sl);
                c.Scanlines(R(0, 40, 450, 38), p, sl);
                c.Scanlines(R(0, 114, 450, 86), p, sl);
                c.Scanlines(R(790, 114, 450, 86), p, sl);
                c.Scanlines(R(790, 40, 200, 68), p, sl);
                c.Scanlines(R(1000, 40, 240, 68), p, sl);
                c.Scanlines(R(470, 32, 300, 168), p, sl);
            }
        }

        private RectF R(float x, float y, float w, float h) { return new RectF(ox + x * k, oy + y * k, w * k, h * k); }
        private Col A(Col col) { return col.WithA(col.A * op); }
        private Col A(Col col, float a) { return col.WithA(a * op); }
        private Col Dark { get { return new Col(0.03f, 0.03f, 0.07f, op); } }

        private int Tier()
        {
            if (!s.HasSpeed) return 0;
            if (s.Speed >= th.Tier3) return 4;
            if (s.Speed >= th.Tier2) return 3;
            if (s.Speed >= th.Tier1) return 2;
            return 1;
        }

        private Col TierColour(int tier)
        {
            if (tier == 0) return th.Primary.WithA(0.5f);
            if (!th.ReactiveColours) return th.Primary;
            switch (tier)
            {
                case 1: return th.Primary;
                case 2: return th.Accent;
                case 3: return th.Secondary;
                default: return th.Warn;
            }
        }

        private Col Heat(float f)
        {
            if (f < 0.33f) return Col.Lerp(th.Primary, th.Accent, f / 0.33f);
            if (f < 0.66f) return Col.Lerp(th.Accent, th.Secondary, (f - 0.33f) / 0.33f);
            return Col.Lerp(th.Secondary, th.Warn, (f - 0.66f) / 0.34f);
        }

        private void Brackets(RectF r, float len, float w, Col col)
        {
            c.Fill(new RectF(r.X, r.Y, len, w), col);
            c.Fill(new RectF(r.X, r.Y, w, len), col);
            c.Fill(new RectF(r.Right - len, r.Y, len, w), col);
            c.Fill(new RectF(r.Right - w, r.Y, w, len), col);
            c.Fill(new RectF(r.X, r.Bottom - w, len, w), col);
            c.Fill(new RectF(r.X, r.Bottom - len, w, len), col);
            c.Fill(new RectF(r.Right - len, r.Bottom - w, len, w), col);
            c.Fill(new RectF(r.Right - w, r.Bottom - len, w, len), col);
        }

        private void Marquee(RectF clip, float size, float t)
        {
            if (!ReferenceEquals(s.Song, _measuredSong) || Math.Abs(size - _measuredK) > 0.0001f)
            {
                _songWidth = c.Measure(s.Song, size, true);
                _measuredSong = s.Song;
                _measuredK = size;
            }
            c.PushClip(clip);
            if (_songWidth <= clip.W)
            {
                c.Text(clip, s.Song, size, A(th.Text), TextAlign.Left, true);
            }
            else
            {
                float gap = 3.2f * size;
                float age = t - s.SongChangedAt - 1.5f;
                float off = age > 0f ? (age * 2f * size) % (_songWidth + gap) : 0f;
                c.Text(new RectF(clip.X - off, clip.Y, _songWidth + 8f, clip.H), s.Song, size, A(th.Text), TextAlign.Left, true);
                c.Text(new RectF(clip.X - off + _songWidth + gap, clip.Y, _songWidth + 8f, clip.H), s.Song, size, A(th.Text), TextAlign.Left, true);
            }
            c.PopClip();
        }

        private void DrawLeft(float t)
        {
            c.Panel(R(0, 8, 250, 26), 8 * k, 0f, A(th.Accent, 0.95f), default(Col), 0f);
            c.Text(R(10, 8, 160, 26), "NOW PLAYING", 15 * k, Dark, TextAlign.Left, true);
            if (th.JapaneseAccents) c.Text(R(150, 8, 92, 26), "再生中", 13 * k, Dark, TextAlign.Right, true);
            c.Text(R(258, 8, 96, 26), "/////////", 16 * k, A(th.Primary, 0.55f), TextAlign.Left, true);
            c.Text(R(352, 8, 98, 26), s.SourceLabel, 12 * k, A(th.Primary, 0.8f), TextAlign.Right, false);

            c.Panel(R(0, 40, 450, 38), 10 * k, 10 * k, A(th.Background), A(th.Secondary, 0.9f), Math.Max(1f, 1.5f * k));
            c.Fill(R(7, 47, 4, 24), A(th.Secondary));
            Marquee(R(20, 40, 420, 38), 22 * k, t);

            c.Text(R(0, 84, 40, 24), "SIG", 12 * k, A(th.Primary, 0.6f), TextAlign.Left, true);
            c.Text(R(32, 84, 90, 24), s.LevelLabel, 12 * k, A(th.Text, 0.9f), TextAlign.Left, false);
            float lvl = M.Clamp01((s.LevelDb + 60f) / 60f);
            int lit = (int)(lvl * 20f + 0.5f);
            for (int i = 0; i < 20; i++)
            {
                Col col = i < lit ? A(Heat(i / 19f), 0.95f) : A(th.Primary, 0.08f);
                c.Fill(R(124 + i * 9, 90, 7, 12), col);
            }
            bool blink = ((int)(t * 2f)) % 2 == 0;
            c.Fill(R(318, 91, 10, 10), blink ? A(th.Warn) : A(th.Warn, 0.25f));
            c.Text(R(334, 84, 116, 24), "LIVE // FEED", 12 * k, A(th.Primary, 0.75f), TextAlign.Right, false);

            DrawSpectrum(R(0, 114, 450, 86), false);
        }

        private void DrawRight(float t)
        {
            c.Panel(R(990, 8, 250, 26), 8 * k, 0f, A(th.Accent, 0.95f), default(Col), 0f);
            if (th.JapaneseAccents) c.Text(R(1000, 8, 90, 26), "タイマー", 13 * k, Dark, TextAlign.Left, true);
            c.Text(R(1080, 8, 150, 26), "TIMER", 15 * k, Dark, TextAlign.Right, true);
            c.Text(R(886, 8, 100, 26), "\\\\\\\\\\\\\\\\\\", 16 * k, A(th.Primary, 0.55f), TextAlign.Right, true);
            c.Text(R(790, 8, 100, 26), s.ClockLabel, 12 * k, A(th.Primary, 0.8f), TextAlign.Left, false);

            c.Panel(R(790, 40, 200, 68), 8 * k, 8 * k, A(th.Background, th.Background.A * 0.8f), A(th.Primary, 0.35f), Math.Max(1f, 1.2f * k));
            c.Fill(R(797, 48, 3, 52), A(th.Primary, 0.9f));
            c.Text(R(808, 42, 178, 21), s.FpsLabel, 12 * k, A(th.Text, 0.9f), TextAlign.Left, false);
            c.Text(R(808, 63, 178, 21), s.SceneLabel, 12 * k, A(th.Text, 0.9f), TextAlign.Left, false);
            c.Text(R(808, 84, 178, 21), s.ModeLabel, 12 * k, A(th.Primary, 0.75f), TextAlign.Left, false);

            bool warn = s.HasTimer && th.TimerCountsDown && s.TimerSeconds <= th.TimerWarnSeconds;
            bool blink = warn && ((int)(t * 3f)) % 2 == 0;
            Col digits = !s.HasTimer ? A(th.Primary, 0.35f) : (warn ? A(th.Warn) : A(th.Text));
            Col edge = warn ? (blink ? A(th.Warn) : A(th.Warn, 0.4f)) : A(th.Primary, 0.9f);
            RectF tp = R(1000, 40, 240, 68);
            c.Panel(tp, 12 * k, 12 * k, A(th.Background), edge, Math.Max(1f, 1.5f * k));

            float dh = 44 * k, dw = 25 * k, st = 5.5f * k, sp = 6 * k;
            float w = SevenSeg.Measure(s.TimerText, dw, st, sp);
            float x = tp.X + (tp.W - w) * 0.5f;
            float y = tp.Y + (tp.H - dh) * 0.5f;
            if (th.Chromatic && s.HasTimer)
            {
                SevenSeg.Draw(c, s.TimerText, x - 1.2f * k, y, dw, dh, st, sp, A(th.Secondary, 0.45f), default(Col));
                SevenSeg.Draw(c, s.TimerText, x + 1.2f * k, y, dw, dh, st, sp, A(th.Primary, 0.45f), default(Col));
            }
            SevenSeg.Draw(c, s.TimerText, x, y, dw, dh, st, sp, digits, A(th.Primary, 0.06f));

            DrawSpectrum(R(790, 114, 450, 86), true);
        }

        private void DrawSpectrum(RectF panel, bool rightSide)
        {
            c.Panel(panel, 10 * k, 10 * k, A(th.Background, th.Background.A * 0.75f), A(th.Primary, 0.45f), Math.Max(1f, 1.2f * k));
            c.Text(new RectF(panel.X + 10 * k, panel.Y + 2 * k, 120 * k, 14 * k), rightSide ? "FREQ.R" : "FREQ.L", 10 * k, A(th.Primary, 0.5f), TextAlign.Left, true);
            if (th.JapaneseAccents)
                c.Text(new RectF(panel.Right - 130 * k, panel.Y + 2 * k, 120 * k, 14 * k), "周波数", 10 * k, A(th.Primary, 0.5f), TextAlign.Right, true);

            if (!th.Spectrum)
            {
                c.Text(panel, "SPECTRUM OFFLINE", 12 * k, A(th.Primary, 0.4f), TextAlign.Center, true);
                return;
            }

            int n = HudState.Bands;
            const int segs = 10;
            float bx = panel.X + 10 * k, by = panel.Y + 18 * k;
            float bw = panel.W - 20 * k, bh = panel.H - 26 * k;
            float gapX = 4 * k, gapY = Math.Max(1f, 2 * k);
            float barW = (bw - gapX * (n - 1)) / n;
            float segH = (bh - gapY * (segs - 1)) / segs;

            for (int i = 0; i < n; i++)
            {
                int band = rightSide ? n - 1 - i : i;
                float v = M.Clamp01(s.Spectrum[band]);
                float pk = M.Clamp01(s.Peaks[band]);
                float x = bx + i * (barW + gapX);

                c.Fill(new RectF(x, by, barW, bh), A(th.Primary, 0.05f));
                int lit = (int)Math.Ceiling(v * segs - 0.05f);
                for (int j = 0; j < lit && j < segs; j++)
                {
                    float y = by + bh - (j + 1) * segH - j * gapY;
                    Col col = j >= segs - 1 ? th.Warn : Col.Lerp(th.Secondary, th.Accent, j / (float)(segs - 1));
                    c.Fill(new RectF(x, y, barW, segH), A(col, 0.92f));
                }
                if (pk > 0.02f)
                {
                    float py = by + bh * (1f - pk);
                    c.Fill(new RectF(x, py - 1.5f * k, barW, Math.Max(1f, 2.5f * k)), A(th.Primary, 0.95f));
                }
            }
        }

        private void DrawCore(float t, int tier, Col tierCol, float flash, bool glitch, float hitAge)
        {
            RectF core = R(470, 8, 300, 192);
            Col edge = Col.Lerp(tierCol, Col.White, flash * 0.8f);
            c.Panel(core, 14 * k, 14 * k, A(th.Background, Math.Min(1f, th.Background.A + 0.08f)), A(edge), Math.Max(1f, 2f * k));

            RectF hdr = R(470, 8, 300, 24);
            c.Panel(hdr, 14 * k, 0f, A(tierCol, 0.92f), default(Col), 0f);
            if (tier == 4 && ((int)(t * 4f)) % 2 == 0)
                c.Stripes(R(640, 8, 130, 24), 14 * k, new Col(0f, 0f, 0f, 0.45f * op));
            c.Text(R(486, 8, 150, 24), tier == 4 ? "!! CRITICAL" : "VELOCITY", 14 * k, Dark, TextAlign.Left, true);
            if (th.JapaneseAccents)
                c.Text(R(590, 8, 50, 24), tier == 4 ? "警告" : "速度", 12 * k, Dark, TextAlign.Left, true);
            c.Text(R(650, 8, 110, 24), "BALL.SPD", 12 * k, Dark, TextAlign.Right, true);

            if (th.DataLog)
            {
                RectF logR = R(476, 34, 288, 104);
                c.PushClip(logR);
                float lh = 13 * k;
                float scroll = t * 16f * k;
                int first = (int)(scroll / lh);
                float frac = scroll - first * lh;
                for (int i = 0; i < 10; i++)
                {
                    string line = _log[(first + i) % _log.Length];
                    c.Text(new RectF(logR.X + 2 * k, logR.Y + i * lh - frac, logR.W, lh), line, 10 * k, A(th.Primary, 0.16f), TextAlign.Left, false);
                }
                c.PopClip();
            }

            string sp = s.SpeedText;
            float dh = 96 * k, dw = 54 * k, st = 12 * k, gap = 12 * k;
            float w = SevenSeg.Measure(sp, dw, st, gap);
            float maxW = 270 * k;
            if (w > maxW)
            {
                float f = maxW / w;
                dh *= f; dw *= f; st *= f; gap *= f; w = maxW;
            }
            float x = core.X + (core.W - w) * 0.5f;
            float y = oy + 38 * k + (96 * k - dh) * 0.5f;
            Col main = s.HasSpeed ? A(Col.Lerp(tierCol, Col.White, 0.35f + flash * 0.4f)) : A(th.Primary, 0.35f);
            float ca = th.Chromatic && s.HasSpeed ? (1.5f + 4f * flash + (glitch ? 5f : 0f)) * k : 0f;

            if (glitch)
            {
                const int slices = 4;
                float sh = dh / slices;
                uint seed = (uint)(t * 30f);
                for (int i = 0; i < slices; i++)
                {
                    float jx = (M.Hash01(seed * 7u + (uint)i) - 0.5f) * 18f * k;
                    c.PushClip(new RectF(core.X, y + i * sh, core.W, sh + 0.5f));
                    DigitsPass(sp, x + jx, y, dw, dh, st, gap, ca, main);
                    c.PopClip();
                }
            }
            else
            {
                DigitsPass(sp, x, y, dw, dh, st, gap, ca, main);
            }

            c.Text(R(482, 140, 160, 16), TierNames[tier], 12 * k, A(tierCol), TextAlign.Left, true);
            c.Text(R(620, 140, 138, 16), s.PeakLabel, 12 * k, A(th.Text, 0.75f), TextAlign.Right, false);

            const int segs = 24;
            float frac2 = s.HasSpeed && th.SpeedBarMax > 0f
                ? (float)(Math.Log(1.0 + s.Speed) / Math.Log(1.0 + th.SpeedBarMax)) : 0f;
            int lit = (int)(M.Clamp01(frac2) * segs + 0.5f);
            float sbX = 482, sbW = 276, sgap = 2f;
            float segW = (sbW - sgap * (segs - 1)) / segs;
            for (int i = 0; i < segs; i++)
            {
                Col col = i < lit ? A(Heat(i / (float)(segs - 1)), 0.95f) : A(th.Primary, 0.08f);
                c.Fill(R(sbX + i * (segW + sgap), 160, segW, 12), col);
            }

            bool recent = hitAge < 1.5f;
            c.Text(R(482, 176, 150, 20), recent ? s.DeltaLabel : "STABLE", 11 * k, recent ? A(th.Accent) : A(th.Primary, 0.5f), TextAlign.Left, true);
            c.Text(R(620, 176, 138, 20), tier == 4 ? "SYS//OVERLOAD" : "SYS//NOMINAL", 11 * k, tier == 4 ? A(th.Warn) : A(th.Primary, 0.5f), TextAlign.Right, false);

            Brackets(core.Inset(-5 * k), 14 * k, Math.Max(1f, 2f * k), A(tierCol, 0.85f));
        }

        private void DigitsPass(string sp, float x, float y, float dw, float dh, float st, float gap, float ca, Col main)
        {
            if (ca > 0f)
            {
                SevenSeg.Draw(c, sp, x - ca, y, dw, dh, st, gap, A(th.Secondary, 0.55f), default(Col));
                SevenSeg.Draw(c, sp, x + ca, y, dw, dh, st, gap, A(th.Primary, 0.55f), default(Col));
            }
            SevenSeg.Draw(c, sp, x, y, dw, dh, st, gap, main, A(th.Primary, 0.07f));
        }

        private const int ReleaseSegs = 18;

        private void DrawRelease(float t)
        {
            bool active = s.ReleaseActive;
            float p = s.ReleaseProgress;
            bool window = active && p >= th.ReleaseWindow;
            float burst = M.Clamp01(1f - (t - s.ReleaseEndedAt) / 0.3f);
            float pulse = 0.5f + 0.5f * (float)Math.Sin(t * 28f);
            Col hot = s.ReleaseReduced ? th.Reduced : th.Secondary;

            ReleaseTrack(0f, 456f, -46f, 34f, ReleaseSegs, 4f, active, p, window, pulse, burst, hot, false);
            ReleaseTrack(784f, 456f, -46f, 34f, ReleaseSegs, 4f, active, p, window, pulse, burst, hot, true);

            RectF box = R(470, -46, 300, 40);
            Col edge = window ? Col.Lerp(th.Hit, Col.White, pulse * 0.6f)
                     : active ? hot
                     : th.Primary.WithA(0.45f);
            Col fill = burst > 0f ? Col.Lerp(th.Background, th.Accent, burst * 0.9f) : th.Background;
            c.Panel(box, 10 * k, 10 * k, A(fill), A(edge), Math.Max(1f, (window ? 2.2f : 1.5f) * k));

            string label;
            Col labelCol;
            if (burst > 0.35f) { label = "RELEASE!"; labelCol = Dark; }
            else if (window) { label = "RELEASE"; labelCol = A(Col.Lerp(th.Hit, Col.White, pulse * 0.5f)); }
            else if (active) { label = s.ReleaseReduced ? "REDUCED" : "HITSTUN"; labelCol = A(hot); }
            else { label = s.HasRelease ? "STANDBY" : "NO LINK"; labelCol = A(th.Primary, 0.55f); }
            c.Text(R(486, -46, 150, 40), label, 16 * k, labelCol, TextAlign.Left, true);
            if (th.JapaneseAccents)
                c.Text(R(600, -46, 60, 40), "リリース", 10 * k, burst > 0.35f ? Dark : A(th.Primary, 0.5f), TextAlign.Left, true);

            float dh = 24 * k, dw = 14 * k, st = 3.4f * k, sp = 4 * k;
            string ro = s.ReleaseReadout;
            float w = SevenSeg.Measure(ro, dw, st, sp);
            float right = ox + 738 * k;
            float y = oy + (-46 + (40 - 24) * 0.5f) * k;
            Col digits = burst > 0.35f ? Dark
                       : window ? A(Col.Lerp(th.Hit, Col.White, pulse * 0.5f))
                       : active ? A(th.Text)
                       : A(th.Primary, 0.35f);
            SevenSeg.Draw(c, ro, right - w, y, dw, dh, st, sp, digits, burst > 0.35f ? default(Col) : A(th.Primary, 0.06f));
            if (s.ReleaseUnit.Length > 0)
                c.Text(R(742, -46, 22, 40), s.ReleaseUnit, 13 * k, digits, TextAlign.Left, true);

            Brackets(box.Inset(-4 * k), 10 * k, Math.Max(1f, 1.5f * k), A(window ? th.Hit : th.Primary, window ? 0.9f : 0.5f));
        }

        private void ReleaseTrack(float x, float w, float top, float h, int n, float gap,
                                  bool active, float p, bool window, float pulse, float burst, Col hot, bool mirror)
        {
            float segW = (w - gap * (n - 1)) / n;
            int lit = active ? (int)Math.Ceiling(p * n - 0.001f) : 0;
            int windowAt = (int)Math.Ceiling(th.ReleaseWindow * n - 0.001f);

            for (int i = 0; i < n; i++)
            {
                float frac = i / (float)(n - 1);
                float sh = h * (0.32f + 0.68f * frac);
                float sx = mirror ? x + w - (i + 1) * segW - i * gap : x + i * (segW + gap);
                Col col;
                float a;
                if (i < lit)
                {
                    col = window ? Col.Lerp(th.Hit, Col.White, pulse * 0.45f) : Col.Lerp(th.Primary, hot, frac);
                    a = 0.95f;
                }
                else if (burst > 0f)
                {
                    col = th.Accent;
                    a = 0.15f + 0.75f * burst;
                }
                else
                {
                    col = i >= windowAt ? th.Hit : th.Primary;
                    a = i >= windowAt ? 0.12f : 0.07f;
                }
                c.Fill(R(sx, top + h - sh, segW, sh), A(col, a));
            }

            c.Fill(R(x, top + h + 2, w, 1.5f), A(th.Primary, 0.35f));
            float mx = mirror ? x + w - windowAt * (segW + gap) + gap * 0.5f : x + windowAt * (segW + gap) - gap * 0.5f;
            c.Fill(R(mx - 0.75f, top - 2, 1.5f, h + 6), A(th.Hit, active ? 0.8f : 0.35f));
        }

        private void DrawFooter(float t)
        {
            RectF tr = R(474, 204, 180, 26);
            float size = 18 * k;
            if (th.Chromatic)
            {
                c.Text(tr.Shift(-1.5f * k, 0f), th.Title, size, A(th.Secondary, 0.6f), TextAlign.Left, true);
                c.Text(tr.Shift(1.5f * k, 0f), th.Title, size, A(th.Primary, 0.6f), TextAlign.Left, true);
            }
            float flicker = th.Glitch && M.Hash01((uint)(t * 12f) + 77u) < 0.04f ? 0.45f : 1f;
            c.Text(tr, th.Title, size, A(th.Text, flicker), TextAlign.Left, true);

            RectF bc = R(660, 207, 110, 20);
            float unit = bc.W / _barUnits;
            Col bcol = A(th.Text, 0.85f);
            for (int i = 0; i < _barStart.Length; i++)
                c.Fill(new RectF(bc.X + _barStart[i] * unit, bc.Y, _barWidth[i] * unit, bc.H), bcol);

            Rail(R(0, 206, 450, 8));
            Rail(R(790, 206, 450, 8));
            c.Text(R(0, 214, 300, 16), "NEURAL LINK // STABLE", 10 * k, A(th.Primary, 0.45f), TextAlign.Left, false);
            c.Text(R(940, 214, 300, 16), "NEONHUD v1.5 // LLB", 10 * k, A(th.Primary, 0.45f), TextAlign.Right, false);
        }

        private void Rail(RectF r)
        {
            Col col = A(th.Primary, 0.35f);
            c.Fill(new RectF(r.X, r.Y + r.H * 0.5f, r.W, Math.Max(1f, 1.5f * k)), col);
            for (float x = r.X; x <= r.Right; x += 30 * k)
                c.Fill(new RectF(x, r.Y, Math.Max(1f, 1.5f * k), r.H), col);
        }
    }
}
