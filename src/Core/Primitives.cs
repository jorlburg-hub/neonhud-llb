using System;

namespace NeonHud.Core
{
    public struct RectF
    {
        public float X, Y, W, H;

        public RectF(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

        public float Right { get { return X + W; } }
        public float Bottom { get { return Y + H; } }

        public RectF Inset(float d) { return new RectF(X + d, Y + d, W - 2f * d, H - 2f * d); }
        public RectF Shift(float dx, float dy) { return new RectF(X + dx, Y + dy, W, H); }
    }

    public struct Col
    {
        public float R, G, B, A;

        public Col(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }

        public Col WithA(float a) { return new Col(R, G, B, a); }

        public static readonly Col White = new Col(1f, 1f, 1f, 1f);
        public static readonly Col Black = new Col(0f, 0f, 0f, 1f);

        public static Col Lerp(Col a, Col b, float t)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return new Col(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        public static Col FromHex(string hex, Col fallback)
        {
            if (hex == null) return fallback;
            string h = hex.Trim();
            if (h.StartsWith("#")) h = h.Substring(1);
            if (h.Length != 6 && h.Length != 8) return fallback;
            try
            {
                float r = Convert.ToInt32(h.Substring(0, 2), 16) / 255f;
                float g = Convert.ToInt32(h.Substring(2, 2), 16) / 255f;
                float b = Convert.ToInt32(h.Substring(4, 2), 16) / 255f;
                float a = h.Length == 8 ? Convert.ToInt32(h.Substring(6, 2), 16) / 255f : 1f;
                return new Col(r, g, b, a);
            }
            catch (FormatException) { return fallback; }
        }
    }

    public enum TextAlign { Left, Center, Right }

    public interface ICanvas
    {
        void Fill(RectF r, Col c);

        void Panel(RectF r, float chamferTL, float chamferBR, Col fill, Col edge, float edgeWidth);

        void Text(RectF r, string s, float size, Col c, TextAlign align, bool bold);

        float Measure(string s, float size, bool bold);

        void PushClip(RectF r);
        void PopClip();

        void Scanlines(RectF r, float period, Col c);

        void Stripes(RectF r, float period, Col c);
    }

    internal static class M
    {
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU;
            x ^= x >> 15; x *= 0x846ca68bU;
            x ^= x >> 16;
            return x;
        }

        public static float Hash01(uint x) { return (Hash(x) & 0xFFFFFF) / 16777215f; }
    }
}
