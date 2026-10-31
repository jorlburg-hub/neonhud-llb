using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using NeonHud.Core;

namespace NeonHud.Preview
{
    internal sealed class GdiCanvas : ICanvas
    {
        private readonly Graphics _g;
        private readonly Stack<GraphicsState> _clips = new Stack<GraphicsState>();
        private readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();
        private readonly string _family;

        public GdiCanvas(Graphics g, string family)
        {
            _g = g;
            _family = family;
            _g.SmoothingMode = SmoothingMode.AntiAlias;
            _g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        private static Color C(Col c)
        {
            Func<float, int> b = v => Math.Max(0, Math.Min(255, (int)Math.Round(v * 255f)));
            return Color.FromArgb(b(c.A), b(c.R), b(c.G), b(c.B));
        }

        private Font F(float size, bool bold)
        {
            string key = size.ToString("0.0") + (bold ? "b" : "");
            Font f;
            if (!_fonts.TryGetValue(key, out f))
            {
                f = new Font(_family, Math.Max(1f, size), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                _fonts[key] = f;
            }
            return f;
        }

        public void Fill(RectF r, Col c)
        {
            if (c.A <= 0f) return;
            using (var b = new SolidBrush(C(c))) _g.FillRectangle(b, r.X, r.Y, r.W, r.H);
        }

        private static GraphicsPath Chamfer(float x, float y, float w, float h, float tl, float br)
        {
            var p = new GraphicsPath();
            p.AddPolygon(new[]
            {
                new PointF(x + tl, y), new PointF(x + w, y), new PointF(x + w, y + h - br),
                new PointF(x + w - br, y + h), new PointF(x, y + h), new PointF(x, y + tl)
            });
            return p;
        }

        public void Panel(RectF r, float tl, float br, Col fill, Col edge, float ew)
        {
            if (fill.A > 0f)
                using (var p = Chamfer(r.X, r.Y, r.W, r.H, tl, br))
                using (var b = new SolidBrush(C(fill))) _g.FillPath(b, p);
            if (edge.A > 0f && ew > 0f)
            {
                float h = ew * 0.5f;
                using (var p = Chamfer(r.X + h, r.Y + h, r.W - ew, r.H - ew, Math.Max(0f, tl - h * 0.4f), Math.Max(0f, br - h * 0.4f)))
                using (var pen = new Pen(C(edge), ew)) _g.DrawPath(pen, p);
            }
        }

        public void Text(RectF r, string s, float size, Col c, TextAlign align, bool bold)
        {
            if (string.IsNullOrEmpty(s) || c.A <= 0f) return;
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            using (var b = new SolidBrush(C(c)))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                sf.LineAlignment = StringAlignment.Center;
                sf.Alignment = align == TextAlign.Left ? StringAlignment.Near : align == TextAlign.Center ? StringAlignment.Center : StringAlignment.Far;
                _g.DrawString(s, F(size, bold), b, new RectangleF(r.X, r.Y, r.W, r.H), sf);
            }
        }

        public float Measure(string s, float size, bool bold)
        {
            return _g.MeasureString(s, F(size, bold), PointF.Empty, StringFormat.GenericTypographic).Width;
        }

        public void PushClip(RectF r)
        {
            _clips.Push(_g.Save());
            _g.IntersectClip(new RectangleF(r.X, r.Y, r.W, r.H));
        }

        public void PopClip() { _g.Restore(_clips.Pop()); }

        public void Scanlines(RectF r, float period, Col c)
        {
            using (var b = new SolidBrush(C(c)))
                for (float y = r.Y; y < r.Bottom; y += period) _g.FillRectangle(b, r.X, y, r.W, 1f);
        }

        public void Stripes(RectF r, float period, Col c)
        {
            PushClip(r);
            using (var b = new SolidBrush(C(c)))
                for (float x = r.X - r.H; x < r.Right; x += period)
                    _g.FillPolygon(b, new[]
                    {
                        new PointF(x, r.Bottom), new PointF(x + period * 0.5f, r.Bottom),
                        new PointF(x + period * 0.5f + r.H, r.Y), new PointF(x + r.H, r.Y)
                    });
            PopClip();
        }
    }

    internal static class Program
    {
        private static void Backdrop(Graphics g, int w, int h, int seed)
        {
            using (var lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(70, 70, 76), Color.FromArgb(28, 28, 34), 90f))
                g.FillRectangle(lg, 0, 0, w, h);
            var rng = new Random(seed);
            for (int i = 0; i < 60; i++)
            {
                int x = rng.Next(-80, w), y = rng.Next(-40, h);
                int s = rng.Next(60, 200);
                int v = rng.Next(40, 105);
                using (var b = new SolidBrush(Color.FromArgb(200, v, v, v + 4)))
                    g.FillPolygon(b, new[]
                    {
                        new Point(x, y + s / 2), new Point(x + s / 3, y), new Point(x + s, y + s / 5),
                        new Point(x + s + s / 4, y + s / 2), new Point(x + s / 2, y + s)
                    });
            }
        }

        private static Bitmap Frame(string family, HudTheme theme, Action<HudState> setup, float time, string caption)
        {
            float bw, bh;
            HudPainter.Size(theme, out bw, out bh);
            const int W = 1920;
            int H = (int)bh + 50;
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Backdrop(g, W, H, (int)(time * 10));
                var s = new HudState();
                s.Time = time;
                s.FpsLabel = "FPS 144";
                s.SceneLabel = "SCN OUTSKIRTS";
                s.ClockLabel = "LOCAL 00:28";
                s.SourceLabel = "SRC:AUDIO";
                DemoFeed.Spectrum(s, time);
                setup(s);
                var canvas = new GdiCanvas(g, family);
                new HudPainter().Paint(canvas, s, theme, (W - bw) / 2f, H - bh - 12f, 1f);
                using (var f = new Font(family, 18f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                    g.DrawString(caption, f, b, 16f, 10f);
            }
            return bmp;
        }

        private static Bitmap CardsFrame(string family, HudTheme theme, int players, float time, string caption, bool screen = false)
        {
            const int W = 1920;
            int H = theme.Compact ? 130 : 200;
            if (screen) H = 1080;
            var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                Backdrop(g, W, H, players * 7);
                var s = new HudState();
                string[] chars = { "SWITCH", "CANDYMAN", "LATCH", "DUST & ASHES" };
                string[] names = { "GLITCH", "P2", "NETRUNNER", "P4" };
                for (int i = 0; i < players; i++)
                {
                    PlayerCard p = s.Players[i];
                    p.Present = true;
                    p.SetIdentity(i, i, chars[i], names[i]);
                    p.SetScore(false, 3 - i % 3, -1);
                }
                s.Players[0].SetHp(0.82f, 100, false, 0.3f, false, time);
                s.Players[0].Energy01 = 0.55f;
                s.Players[1].SetHp(0.9f, 100, false, 0.5f, false, time - 0.3f);
                s.Players[1].SetHp(0.41f, 100, false, 0.5f, true, time - 0.25f);
                s.Players[1].Energy01 = 1f; s.Players[1].EnergyFull = true;
                if (players > 2)
                {
                    s.Players[2].SetHp(0.63f, 150, false, 0.12f, false, time); s.Players[2].Energy01 = 0.2f;
                    s.Players[3].SetHp(1f, 100, false, 0.12f, false, time); s.Players[3].Energy01 = 0.8f;
                }
                var canvas = new GdiCanvas(g, family);
                new PlayerCardsPainter().Paint(canvas, s.Players, theme, W, time, 1f, 34f);
                if (players == 2 && screen)
                {
                    s.SetSpeed(120f, 0f); s.SetSpeed(164f, 3f); s.SpeedChangedAt = 2.7f;
                    s.SetTimer(171f, true, "MATCH"); s.SetSong("FRANK KLEPACKI - DECIBLE", 0f);
                    s.SetRelease(true, 0.6f, 15, false, time);
                    DemoFeed.Spectrum(s, time); s.Time = time;
                    float bw, bh; HudPainter.Size(theme, out bw, out bh);
                    new HudPainter().Paint(canvas, s, theme, (W - bw) / 2f, H - bh - 12f, 1f);
                }
                using (var f = new Font(family, 18f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                    g.DrawString(caption, f, b, 16f, 6f);
            }
            return bmp;
        }

        private static string PickFamily(params string[] wanted)
        {
            var installed = new HashSet<string>();
            foreach (FontFamily f in FontFamily.Families) installed.Add(f.Name);
            foreach (string w in wanted) if (installed.Contains(w)) return w;
            return FontFamily.GenericSansSerif.Name;
        }

        private static int Main(string[] args)
        {
            string outPath = args.Length > 0 ? args[0] : "neonhud_preview.png";
            string family = args.Length > 1 ? args[1] : PickFamily("Bahnschrift", "Segoe UI", "DejaVu Sans", "Arial");
            var theme = new HudTheme { TimerCountsDown = true };
            var full = new HudTheme { TimerCountsDown = true, Compact = false };
            ThemePreset preset = args.Length > 2 ? (ThemePreset)Enum.Parse(typeof(ThemePreset), args[2], true) : ThemePreset.Mono;
            Palettes.Apply(theme, preset);
            Palettes.Apply(full, preset);

            using (var a = CardsFrame(family, theme, 2, 5f, "COMPACT LAYOUT at 1920x1080", true))
                a.Save(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(outPath)), "screen_compact.png"), ImageFormat.Png);
            using (var b = CardsFrame(family, full, 2, 5f, "FULL LAYOUT at 1920x1080", true))
                b.Save(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(outPath)), "screen_full.png"), ImageFormat.Png);

            var frames = new List<Bitmap>();
            foreach (var th in new[] { theme, full })
            {
                string tag = th.Compact ? "COMPACT" : "FULL";
                frames.Add(CardsFrame(family, th, 2, 5f, tag + " CARDS (1v1)  -  P1: ball threatens 30%   P2: just hit (white ghost) + next hit LETHAL"));
                frames.Add(CardsFrame(family, th, 4, 5f, tag + " CARDS (4 players)"));
                frames.Add(Frame(family, th, s =>
                {
                    s.SetSpeed(24f, 0f); s.SetSpeed(28f, 0f); s.SpeedChangedAt = -10f;
                    s.SetTimer(297f, true, "MATCH"); s.SetSong("OUTSKIRTS", 0f);
                    s.SetRelease(false, 0f, -1, false, 0f);
                }, 3.3f, tag + "  -  standby, speed 28"));
                frames.Add(Frame(family, th, s =>
                {
                    s.SetSpeed(120f, 0f); s.SetSpeed(164f, 8.0f); s.SpeedChangedAt = 7.8f;
                    s.SetTimer(171f, true, "MATCH"); s.SetSong("FRANK KLEPACKI - DECIBLE", 0f);
                    s.SetRelease(true, 0.45f, 22, false, 8f);
                }, 8.0f, tag + "  -  hitstun charging, 22 frames to release"));
                frames.Add(Frame(family, th, s =>
                {
                    s.SetSpeed(480f, 0f); s.SetSpeed(612f, 10f); s.SpeedChangedAt = 10f - 0.6f;
                    s.SetTimer(24f, true, "MATCH"); s.SetSong("A VERY LONG TRACK NAME THAT NEEDS THE MARQUEE TO SCROLL ACROSS", 0f);
                    s.SetRelease(true, 0.9f, 4, false, 12f);
                }, 12.0f, tag + "  -  release window (4F), critical speed, timer warning"));
                frames.Add(Frame(family, th, s =>
                {
                    s.SetSpeed(480f, 0f); s.SetSpeed(612f, 10f); s.SpeedChangedAt = 10f - 1.2f;
                    s.SetTimer(23f, true, "MATCH"); s.SetSong("FRANK KLEPACKI - DECIBLE", 0f);
                    s.SetRelease(true, 0.6f, 15, true, 12.9f);
                }, 13.0f, tag + "  -  reduced hitstun"));
            }

            int total = 0;
            foreach (var f in frames) total += f.Height;
            using (var sheet = new Bitmap(frames[0].Width, total))
            using (var g = Graphics.FromImage(sheet))
            {
                int y = 0;
                foreach (var f in frames) { g.DrawImage(f, 0, y); y += f.Height; f.Dispose(); }
                sheet.Save(outPath, ImageFormat.Png);
            }
            Console.WriteLine("wrote " + outPath);
            return 0;
        }
    }
}
