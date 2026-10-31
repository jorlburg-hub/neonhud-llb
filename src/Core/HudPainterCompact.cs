using System;

namespace NeonHud.Core
{
    public sealed partial class HudPainter
    {
        public const float CompactWidth = 640f;
        public const float CompactHeight = 136f;

        public static void Size(HudTheme theme, out float w, out float h)
        {
            if (theme.Compact) { w = CompactWidth; h = CompactHeight; }
            else { w = BaseWidth; h = BaseHeight; }
        }

        private void PaintCompact(float t)
        {
            float hitAge = t - s.SpeedChangedAt;
            float flash = M.Clamp01(1f - hitAge / 0.35f);
            bool glitch = th.Glitch && (hitAge < 0.18f || M.Hash01((uint)(t * 5f) + 911u) < 0.015f);
            int tier = Tier();
            Col tierCol = TierColour(tier);

            if (th.ShowRelease) CompactRelease(t);
            CompactLeft(t);
            CompactCore(t, tier, tierCol, flash, glitch);
            CompactRight(t);
        }

        private void CompactRelease(float t)
        {
            bool active = s.ReleaseActive;
            float p = s.ReleaseProgress;
            bool window = active && p >= th.ReleaseWindow;
            float burst = M.Clamp01(1f - (t - s.ReleaseEndedAt) / 0.3f);
            float pulse = 0.5f + 0.5f * (float)Math.Sin(t * 28f);
            Col hot = s.ReleaseReduced ? th.Reduced : th.Secondary;

            ReleaseTrack(0f, 212f, 0f, 22f, 12, 3f, active, p, window, pulse, burst, hot, false);
            ReleaseTrack(428f, 212f, 0f, 22f, 12, 3f, active, p, window, pulse, burst, hot, true);

            RectF box = R(220, 0, 200, 24);
            Col edge = window ? Col.Lerp(th.Hit, Col.White, pulse * 0.6f) : active ? hot : th.Primary.WithA(0.45f);
            Col fill = burst > 0f ? Col.Lerp(th.Background, th.Accent, burst * 0.9f) : th.Background;
            c.Panel(box, 6 * k, 6 * k, A(fill), A(edge), Math.Max(1f, (window ? 1.8f : 1.2f) * k));

            string label; Col labelCol;
            if (burst > 0.35f) { label = "RELEASE!"; labelCol = Dark; }
            else if (window) { label = "RELEASE"; labelCol = A(Col.Lerp(th.Hit, Col.White, pulse * 0.5f)); }
            else if (active) { label = s.ReleaseReduced ? "REDUCED" : "HITSTUN"; labelCol = A(hot); }
            else { label = s.HasRelease ? "STANDBY" : "NO LINK"; labelCol = A(th.Primary, 0.55f); }
            c.Text(R(229, 0, 110, 24), label, 11 * k, labelCol, TextAlign.Left, true);

            float dh = 15 * k, dw = 9 * k, st = 2.3f * k, sp = 3 * k;
            string ro = s.ReleaseReadout;
            float w = SevenSeg.Measure(ro, dw, st, sp);
            Col digits = burst > 0.35f ? Dark : window ? A(Col.Lerp(th.Hit, Col.White, pulse * 0.5f)) : active ? A(th.Text) : A(th.Primary, 0.35f);
            SevenSeg.Draw(c, ro, ox + 400 * k - w, oy + (24 - 15) * 0.5f * k, dw, dh, st, sp, digits, burst > 0.35f ? default(Col) : A(th.Primary, 0.06f));
            if (s.ReleaseUnit.Length > 0) c.Text(R(402, 0, 16, 24), s.ReleaseUnit, 9 * k, digits, TextAlign.Left, true);
        }

        private void CompactLeft(float t)
        {
            c.Panel(R(0, 30, 112, 16), 5 * k, 0f, A(th.Accent, 0.95f), default(Col), 0f);
            c.Text(R(6, 30, 104, 16), "NOW PLAYING", 10 * k, Dark, TextAlign.Left, true);

            RectF song = R(0, 48, 212, 26);
            c.Panel(song, 7 * k, 7 * k, A(th.Background), A(th.Secondary, 0.85f), Math.Max(1f, 1.2f * k));
            c.Fill(R(5, 53, 3, 16), A(th.Secondary));
            Marquee(R(13, 48, 194, 26), 13 * k, t);
            if (th.Scanlines) c.Scanlines(song, Math.Max(2f, 3f * k), new Col(0f, 0f, 0f, 0.2f * op));

            c.Text(R(0, 79, 26, 16), "SIG", 8 * k, A(th.Primary, 0.6f), TextAlign.Left, true);
            float lvl = M.Clamp01((s.LevelDb + 60f) / 60f);
            const int segs = 16;
            int lit = (int)(lvl * segs + 0.5f);
            float sw = (186f - 2f * (segs - 1)) / segs;
            for (int i = 0; i < segs; i++)
                c.Fill(R(26 + i * (sw + 2f), 83, sw, 8), i < lit ? A(Heat(i / (segs - 1f)), 0.95f) : A(th.Primary, 0.08f));

            if (th.Spectrum) MiniSpectrum(R(0, 100, 212, 32), false);
        }

        private void CompactCore(float t, int tier, Col tierCol, float flash, bool glitch)
        {
            RectF core = R(220, 30, 200, 102);
            Col edge = Col.Lerp(tierCol, Col.White, flash * 0.8f);
            c.Panel(core, 9 * k, 9 * k, A(th.Background, Math.Min(1f, th.Background.A + 0.08f)), A(edge), Math.Max(1f, 1.6f * k));

            c.Panel(R(220, 30, 200, 16), 9 * k, 0f, A(tierCol, 0.92f), default(Col), 0f);
            if (tier == 4 && ((int)(t * 4f)) % 2 == 0) c.Stripes(R(330, 30, 90, 16), 10 * k, new Col(0f, 0f, 0f, 0.45f * op));
            c.Text(R(230, 30, 110, 16), tier == 4 ? "!! CRITICAL" : "VELOCITY", 10 * k, Dark, TextAlign.Left, true);
            c.Text(R(330, 30, 82, 16), "BALL.SPD", 8 * k, Dark, TextAlign.Right, true);

            string sp = s.SpeedText;
            float dh = 50 * k, dw = 28 * k, st = 6.5f * k, gap = 7 * k;
            float w = SevenSeg.Measure(sp, dw, st, gap);
            float maxW = 184 * k;
            if (w > maxW) { float f = maxW / w; dh *= f; dw *= f; st *= f; gap *= f; w = maxW; }
            float x = core.X + (core.W - w) * 0.5f;
            float y = oy + 49 * k + (50 * k - dh) * 0.5f;
            Col main = s.HasSpeed ? A(Col.Lerp(tierCol, Col.White, 0.35f + flash * 0.4f)) : A(th.Primary, 0.35f);
            float ca = th.Chromatic && s.HasSpeed ? (1f + 3f * flash + (glitch ? 3f : 0f)) * k : 0f;
            if (glitch)
            {
                const int slices = 4;
                float sh = dh / slices;
                uint seed = (uint)(t * 30f);
                for (int i = 0; i < slices; i++)
                {
                    float jx = (M.Hash01(seed * 7u + (uint)i) - 0.5f) * 10f * k;
                    c.PushClip(new RectF(core.X, y + i * sh, core.W, sh + 0.5f));
                    DigitsPass(sp, x + jx, y, dw, dh, st, gap, ca, main);
                    c.PopClip();
                }
            }
            else DigitsPass(sp, x, y, dw, dh, st, gap, ca, main);

            c.Text(R(228, 102, 100, 12), TierNames[tier], 8 * k, A(tierCol), TextAlign.Left, true);
            c.Text(R(320, 102, 92, 12), s.PeakLabel, 8 * k, A(th.Text, 0.7f), TextAlign.Right, false);

            const int segs = 16;
            float frac = s.HasSpeed && th.SpeedBarMax > 0f ? (float)(Math.Log(1.0 + s.Speed) / Math.Log(1.0 + th.SpeedBarMax)) : 0f;
            int lit = (int)(M.Clamp01(frac) * segs + 0.5f);
            float segW = (184f - 2f * (segs - 1)) / segs;
            for (int i = 0; i < segs; i++)
                c.Fill(R(228 + i * (segW + 2f), 117, segW, 8), i < lit ? A(Heat(i / (segs - 1f)), 0.95f) : A(th.Primary, 0.08f));

            if (th.Scanlines) c.Scanlines(core, Math.Max(2f, 3f * k), new Col(0f, 0f, 0f, 0.2f * op));
            Brackets(core.Inset(-3 * k), 8 * k, Math.Max(1f, 1.2f * k), A(tierCol, 0.8f));
        }

        private void CompactRight(float t)
        {
            c.Panel(R(528, 30, 112, 16), 5 * k, 0f, A(th.Accent, 0.95f), default(Col), 0f);
            c.Text(R(532, 30, 102, 16), "TIMER", 10 * k, Dark, TextAlign.Right, true);
            if (th.JapaneseAccents) c.Text(R(534, 30, 60, 16), "タイマー", 8 * k, Dark, TextAlign.Left, true);

            bool warn = s.HasTimer && th.TimerCountsDown && s.TimerSeconds <= th.TimerWarnSeconds;
            bool blink = warn && ((int)(t * 3f)) % 2 == 0;
            Col digits = !s.HasTimer ? A(th.Primary, 0.35f) : (warn ? A(th.Warn) : A(th.Text));
            Col edge = warn ? (blink ? A(th.Warn) : A(th.Warn, 0.4f)) : A(th.Primary, 0.9f);
            RectF tp = R(428, 48, 212, 46);
            c.Panel(tp, 8 * k, 8 * k, A(th.Background), edge, Math.Max(1f, 1.2f * k));

            float dh = 30 * k, dw = 17 * k, st = 4f * k, sp = 4 * k;
            float w = SevenSeg.Measure(s.TimerText, dw, st, sp);
            float x = tp.X + (tp.W - w) * 0.5f, y = tp.Y + (tp.H - dh) * 0.5f;
            if (th.Chromatic && s.HasTimer)
            {
                SevenSeg.Draw(c, s.TimerText, x - 0.8f * k, y, dw, dh, st, sp, A(th.Secondary, 0.45f), default(Col));
                SevenSeg.Draw(c, s.TimerText, x + 0.8f * k, y, dw, dh, st, sp, A(th.Primary, 0.45f), default(Col));
            }
            SevenSeg.Draw(c, s.TimerText, x, y, dw, dh, st, sp, digits, A(th.Primary, 0.06f));
            if (th.Scanlines) c.Scanlines(tp, Math.Max(2f, 3f * k), new Col(0f, 0f, 0f, 0.2f * op));

            if (th.Spectrum) MiniSpectrum(R(428, 100, 212, 32), true);
        }

        private void MiniSpectrum(RectF r, bool mirror)
        {
            c.Panel(r, 5 * k, 5 * k, A(th.Background, th.Background.A * 0.7f), A(th.Primary, 0.35f), Math.Max(1f, 1f * k));
            int n = HudState.Bands;
            const int segs = 6;
            float pad = 5 * k, gapX = 2.5f * k, gapY = Math.Max(1f, 1.5f * k);
            float bw = r.W - 2 * pad, bh = r.H - 2 * pad;
            float barW = (bw - gapX * (n - 1)) / n, segH = (bh - gapY * (segs - 1)) / segs;
            for (int i = 0; i < n; i++)
            {
                int band = mirror ? n - 1 - i : i;
                float v = M.Clamp01(s.Spectrum[band]), pk = M.Clamp01(s.Peaks[band]);
                float x = r.X + pad + i * (barW + gapX);
                int lit = (int)Math.Ceiling(v * segs - 0.05f);
                for (int j = 0; j < lit && j < segs; j++)
                {
                    float y = r.Y + pad + bh - (j + 1) * segH - j * gapY;
                    Col col = j >= segs - 1 ? th.Warn : Col.Lerp(th.Secondary, th.Accent, j / (float)(segs - 1));
                    c.Fill(new RectF(x, y, barW, segH), A(col, 0.9f));
                }
                if (pk > 0.02f) c.Fill(new RectF(x, r.Y + pad + bh * (1f - pk) - k, barW, Math.Max(1f, 1.5f * k)), A(th.Primary, 0.9f));
            }
        }
    }
}
