using System;

namespace NeonHud.Core
{
    public sealed class PlayerCard
    {
        public bool Present;
        public int Index;
        public int Team = 4;
        public string Tag = "P1";
        public string Character = "";
        public string Name = "";

        public bool HpOff;
        public float Hp;
        public float Threat;
        public bool Lethal;
        public int HpPoints, HpMax = 100;
        public string HpText = "--";
        public string HpMaxText = "/100";

        public float Energy01;
        public bool EnergyFull;

        public bool PointsMode;
        public int Stocks = -1, Points = -1;
        public string ScoreText = "";

        public float GhostFrom, GhostAt = -100f;
        private float _lastHp = -1f;
        private int _lastHpPoints = int.MinValue, _lastHpMax = int.MinValue, _lastScore = int.MinValue;
        private bool _lastPointsMode;

        public void SetIdentity(int index, int team, string character, string name)
        {
            Index = index;
            Team = team < 0 || team > 4 ? 4 : team;
            if (Tag.Length != 2 || Tag[1] != (char)('1' + index)) Tag = "P" + (index + 1);
            Character = character ?? "";
            Name = name ?? "";
        }

        public void SetHp(float hp, int hpMax, bool hpOff, float threat, bool lethal, float now)
        {
            hp = M.Clamp01(hp);
            if (_lastHp >= 0f && hp < _lastHp - 0.002f)
            {
                GhostFrom = now - GhostAt < 0.6f ? Math.Max(GhostFrom, _lastHp) : _lastHp;
                GhostAt = now;
            }
            _lastHp = hp;
            Hp = hp;
            HpOff = hpOff;
            Threat = Math.Max(0f, threat);
            Lethal = lethal;
            HpMax = hpMax;

            int pts = hp <= 0f ? 0 : hp > 0.98f ? hpMax : 1 + (int)Math.Floor(hp * hpMax);
            HpPoints = pts;
            if (pts != _lastHpPoints) { _lastHpPoints = pts; HpText = hpOff ? "--" : pts.ToString(); }
            if (hpMax != _lastHpMax) { _lastHpMax = hpMax; HpMaxText = "/" + hpMax; }
        }

        public void SetScore(bool pointsMode, int stocks, int points)
        {
            PointsMode = pointsMode;
            Stocks = stocks;
            Points = points;
            int key = pointsMode ? points : stocks;
            if (key == _lastScore && pointsMode == _lastPointsMode) return;
            _lastScore = key;
            _lastPointsMode = pointsMode;
            ScoreText = pointsMode ? (points < 0 ? "" : points + " PTS") : (stocks < 0 ? "" : "x" + stocks);
        }

        public void Reset()
        {
            Present = false;
            _lastHp = -1f;
            GhostAt = -100f;
        }
    }

    public sealed class PlayerCardsPainter
    {
        public const float CardW = 380f, CardH = 96f, Margin = 24f, Top = 16f, BandH = 132f;
        public const float CompactCardW = 260f, CompactCardH = 58f, CompactMargin = 16f, CompactTop = 10f, CompactBandH = 80f;

        private ICanvas c;
        private HudTheme th;
        private float k, op, _cw;
        private readonly int[] _order = new int[4];

        public void Paint(ICanvas canvas, PlayerCard[] cards, HudTheme theme, float screenW, float time, float scale, float offsetY)
        {
            c = canvas; th = theme; k = scale; op = M.Clamp01(theme.Opacity);
            bool compact = theme.Compact;
            _cw = compact ? CompactCardW : CardW;
            float margin0 = compact ? CompactMargin : Margin, top = compact ? CompactTop : Top, band = compact ? CompactBandH : BandH;

            int n = 0;
            for (int i = 0; i < cards.Length && n < _order.Length; i++)
                if (cards[i] != null && cards[i].Present) _order[n++] = i;
            if (n == 0) return;

            float needed = n * _cw + 2f * margin0 + (n - 1) * 12f;
            if (needed * k > screenW) k = screenW / needed;

            float y0 = offsetY * k;
            if (th.PlayerBand) DrawBand(screenW, y0, band);

            float cw = _cw * k, margin = margin0 * k;
            for (int slot = 0; slot < n; slot++)
            {
                float x = n == 1 ? margin : margin + slot * (screenW - 2f * margin - cw) / (n - 1);
                bool mirror = n > 1 && x + cw * 0.5f > screenW * 0.5f + 1f;
                if (compact) DrawCompactCard(cards[_order[slot]], x, y0 + top * k, mirror, time);
                else DrawCard(cards[_order[slot]], x, y0 + top * k, mirror, time);
            }
        }

        private Col A(Col col) { return col.WithA(col.A * op); }
        private Col A(Col col, float a) { return col.WithA(a * op); }
        private Col Dark { get { return new Col(0.03f, 0.03f, 0.07f, op); } }

        private Col TeamCol(int team)
        {
            Col[] t = th.TeamColours;
            return team >= 0 && team < t.Length ? t[team] : th.Text;
        }

        private void DrawBand(float screenW, float y0, float bandH)
        {
            float h = bandH * k;
            const int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                float a = th.Background.A * 0.75f * (1f - i / (float)steps);
                c.Fill(new RectF(0f, y0 + h * i / steps, screenW, h / steps + 0.5f), A(th.Background, a));
            }
            float lineY = y0 + h - 2f * k;
            c.Fill(new RectF(0f, lineY, screenW, Math.Max(1f, 1.5f * k)), A(th.Primary, 0.35f));
            for (float x = 0f; x < screenW; x += 40f * k)
                c.Fill(new RectF(x, lineY - 3f * k, Math.Max(1f, 1.5f * k), 7f * k), A(th.Primary, 0.25f));
            if (th.Scanlines) c.Scanlines(new RectF(0f, y0, screenW, h), Math.Max(2f, 3f * k), new Col(0f, 0f, 0f, 0.18f * op));
        }

        private RectF Box(float x, float y, float lx, float ly, float w, float h, bool mirror)
        {
            float px = mirror ? _cw - lx - w : lx;
            return new RectF(x + px * k, y + ly * k, w * k, h * k);
        }

        private struct HpGeom
        {
            public float X, Y, W, H, Gap; public int Segs;
            public float NumX, NumY, NumW, NumH, DigitH, DigitW, Stroke, Spacing;
        }

        private void DrawCard(PlayerCard p, float x, float y, bool mirror, float t)
        {
            Col team = TeamCol(p.Team);
            bool blink = p.Lethal && ((int)(t * 8f)) % 2 == 0;
            Col edge = p.Lethal ? (blink ? th.Warn : th.Warn.WithA(0.5f)) : team;

            RectF card = new RectF(x, y, CardW * k, CardH * k);
            c.Panel(card, 14f * k, 14f * k, A(th.Background), A(edge, 0.9f), Math.Max(1f, 1.5f * k));
            c.Fill(Box(x, y, 6, 8, 4, CardH - 16, mirror), A(team));
            if (th.Scanlines) c.Scanlines(card, Math.Max(2f, 3f * k), new Col(0f, 0f, 0f, 0.2f * op));

            RectF tab = Box(x, y, 14, 6, 44, 22, mirror);
            c.Panel(tab, 6f * k, 0f, A(team, 0.95f), default(Col), 0f);
            c.Text(tab, p.Tag, 14 * k, Dark, TextAlign.Center, true);
            TextAlign near = mirror ? TextAlign.Right : TextAlign.Left, far = mirror ? TextAlign.Left : TextAlign.Right;
            c.Text(Box(x, y, 64, 6, 170, 22, mirror), p.Character, 15 * k, A(th.Text), near, true);
            if (p.Name.Length > 0 && p.Name != p.Tag)
                c.Text(Box(x, y, 64, 26, 200, 14, mirror), p.Name, 10 * k, A(th.Primary, 0.6f), near, false);
            if (p.ScoreText.Length > 0)
                c.Text(Box(x, y, 250, 6, 116, 22, mirror), p.ScoreText, 14 * k, A(th.Accent), far, true);

            var g = new HpGeom { X = 18, Y = 44, W = 260, H = 28, Gap = 2.5f, Segs = 24,
                                 NumX = 290, NumY = 42, NumW = 56, NumH = 30, DigitH = 26, DigitW = 15, Stroke = 3.6f, Spacing = 4 };
            DrawHp(p, x, y, mirror, t, g);
            c.Text(Box(x, y, 348, 50, 30, 20, mirror), p.HpOff ? "" : p.HpMaxText, 9 * k, A(th.Primary, 0.5f), near, false);
            if (p.Lethal)
                c.Text(Box(x, y, 250, 26, 116, 16, mirror), "!! LETHAL", 11 * k, blink ? A(th.Warn) : A(th.Warn, 0.4f), far, true);

            DrawEnergy(p, Box(x, y, 18, 80, 260, 7, mirror), mirror, t);
            c.Text(Box(x, y, 284, 74, 82, 18, mirror), p.EnergyFull ? "SP READY" : "SP", 10 * k, p.EnergyFull ? A(th.Hit) : A(th.Primary, 0.55f), far, true);
        }

        private void DrawCompactCard(PlayerCard p, float x, float y, bool mirror, float t)
        {
            Col team = TeamCol(p.Team);
            bool blink = p.Lethal && ((int)(t * 8f)) % 2 == 0;
            Col edge = p.Lethal ? (blink ? th.Warn : th.Warn.WithA(0.5f)) : team;
            TextAlign near = mirror ? TextAlign.Right : TextAlign.Left, far = mirror ? TextAlign.Left : TextAlign.Right;

            RectF card = new RectF(x, y, CompactCardW * k, CompactCardH * k);
            c.Panel(card, 9f * k, 9f * k, A(th.Background), A(edge, 0.9f), Math.Max(1f, 1.2f * k));
            c.Fill(Box(x, y, 4, 6, 3, CompactCardH - 12, mirror), A(team));
            if (th.Scanlines) c.Scanlines(card, Math.Max(2f, 3f * k), new Col(0f, 0f, 0f, 0.2f * op));

            RectF tab = Box(x, y, 11, 5, 28, 15, mirror);
            c.Panel(tab, 4f * k, 0f, A(team, 0.95f), default(Col), 0f);
            c.Text(tab, p.Tag, 10 * k, Dark, TextAlign.Center, true);
            c.Text(Box(x, y, 45, 5, 140, 15, mirror), p.Character, 10 * k, A(th.Text), near, true);
            if (p.Lethal) c.Text(Box(x, y, 168, 5, 84, 15, mirror), "LETHAL", 10 * k, blink ? A(th.Warn) : A(th.Warn, 0.4f), far, true);
            else if (p.ScoreText.Length > 0) c.Text(Box(x, y, 168, 5, 84, 15, mirror), p.ScoreText, 10 * k, A(th.Accent), far, true);

            var g = new HpGeom { X = 11, Y = 24, W = 186, H = 17, Gap = 2f, Segs = 20,
                                 NumX = 201, NumY = 22, NumW = 51, NumH = 21, DigitH = 17, DigitW = 10, Stroke = 2.5f, Spacing = 3 };
            DrawHp(p, x, y, mirror, t, g);

            DrawEnergy(p, Box(x, y, 11, 46, 186, 4, mirror), mirror, t);
            if (p.EnergyFull) c.Text(Box(x, y, 201, 42, 51, 12, mirror), "SP", 8 * k, A(th.Hit), far, true);
        }

        private void DrawEnergy(PlayerCard p, RectF eb, bool mirror, float t)
        {
            c.Fill(eb, A(th.Primary, 0.08f));
            float ew = eb.W * M.Clamp01(p.Energy01);
            float pulse = p.EnergyFull ? 0.6f + 0.4f * (float)Math.Sin(t * 10f) : 1f;
            Col ec = p.EnergyFull ? Col.Lerp(th.Hit, Col.White, 0.3f * pulse) : th.Primary;
            if (ew > 0f) c.Fill(mirror ? new RectF(eb.Right - ew, eb.Y, ew, eb.H) : new RectF(eb.X, eb.Y, ew, eb.H), A(ec, 0.9f * pulse));
        }

        private void DrawHp(PlayerCard p, float x, float y, bool mirror, float t, HpGeom g)
        {
            float segW = (g.W - g.Gap * (g.Segs - 1)) / g.Segs;
            float hp = p.HpOff ? 1f : p.Hp;
            float threatFrom = Math.Max(0f, hp - p.Threat);
            float ghostAge = t - p.GhostAt;
            float ghostTop = ghostAge < 0.7f ? Math.Max(hp, p.GhostFrom) : hp;
            float ghostA = M.Clamp01(1f - ghostAge / 0.7f);
            bool blink = p.Lethal && ((int)(t * 8f)) % 2 == 0;
            Col full = hp > 0.5f ? th.Primary : Col.Lerp(th.Warn, th.Accent, hp / 0.5f);

            for (int i = 0; i < g.Segs; i++)
            {
                float mid = (i + 0.5f) / g.Segs;
                RectF seg = Box(x, y, g.X + i * (segW + g.Gap), g.Y, segW, g.H, mirror);
                Col col; float a;
                if (mid <= hp)
                {
                    if (p.Lethal) { col = th.Warn; a = blink ? 1f : 0.45f; }
                    else if (!p.HpOff && mid > threatFrom) { col = Col.Lerp(th.Warn, th.Secondary, 0.3f); a = 0.55f + 0.35f * (float)Math.Sin(t * 12f); }
                    else { col = full; a = 0.95f; }
                }
                else if (mid <= ghostTop) { col = th.Ghost; a = 0.85f * ghostA; }
                else { col = th.Primary; a = 0.07f; }
                if (a > 0f) c.Fill(seg, A(col, a));
            }

            float dh = g.DigitH * k, dw = g.DigitW * k, st = g.Stroke * k, sp = g.Spacing * k;
            Col dc = p.Lethal ? (blink ? A(th.Warn) : A(th.Warn, 0.5f)) : A(th.Text);
            RectF num = Box(x, y, g.NumX, g.NumY, g.NumW, g.NumH, mirror);
            float w = SevenSeg.Measure(p.HpText, dw, st, sp);
            float nx = mirror ? num.X : num.Right - w;
            SevenSeg.Draw(c, p.HpText, nx, num.Y + (num.H - dh) * 0.5f, dw, dh, st, sp, dc, A(th.Primary, 0.06f));
        }
    }
}
