namespace NeonHud.Core
{
    public static class SevenSeg
    {
        private static readonly byte[] DigitMask = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };
        private const byte Dash = 0x40;

        public static float CharWidth(char ch, float dw, float t)
        {
            return ch == ':' || ch == '.' ? t * 1.6f : dw;
        }

        public static float Measure(string s, float dw, float t, float spacing)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            float w = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                w += CharWidth(s[i], dw, t);
                if (i < s.Length - 1) w += spacing;
            }
            return w;
        }

        public static void Draw(ICanvas c, string s, float x, float y, float dw, float dh, float t, float spacing, Col on, Col ghost)
        {
            if (string.IsNullOrEmpty(s)) return;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == ':')
                {
                    float sq = t * 1.1f;
                    float cx = x + (t * 1.6f - sq) * 0.5f;
                    c.Fill(new RectF(cx, y + dh * 0.30f - sq * 0.5f, sq, sq), on);
                    c.Fill(new RectF(cx, y + dh * 0.70f - sq * 0.5f, sq, sq), on);
                }
                else if (ch == '.')
                {
                    float sq = t * 1.1f;
                    c.Fill(new RectF(x + (t * 1.6f - sq) * 0.5f, y + dh - sq, sq, sq), on);
                }
                else
                {
                    byte mask = 0;
                    if (ch >= '0' && ch <= '9') mask = DigitMask[ch - '0'];
                    else if (ch == '-') mask = Dash;
                    DrawDigit(c, mask, x, y, dw, dh, t, on, ghost);
                }
                x += CharWidth(ch, dw, t) + spacing;
            }
        }

        private static void DrawDigit(ICanvas c, byte mask, float x, float y, float w, float h, float t, Col on, Col ghost)
        {
            float gap = t * 0.15f;
            float half = h * 0.5f;
            float vLen = half - t * 0.5f - gap;
            float hLen = w - 2f * t - gap * 2f;
            float hx = x + t + gap;

            for (int seg = 0; seg < 7; seg++)
            {
                bool lit = (mask & (1 << seg)) != 0;
                if (!lit && ghost.A <= 0f) continue;
                RectF r;
                switch (seg)
                {
                    case 0: r = new RectF(hx, y, hLen, t); break;
                    case 1: r = new RectF(x + w - t, y, t, vLen); break;
                    case 2: r = new RectF(x + w - t, y + half + t * 0.5f + gap, t, vLen); break;
                    case 3: r = new RectF(hx, y + h - t, hLen, t); break;
                    case 4: r = new RectF(x, y + half + t * 0.5f + gap, t, vLen); break;
                    case 5: r = new RectF(x, y, t, vLen); break;
                    default: r = new RectF(hx, y + half - t * 0.5f, hLen, t); break;
                }
                c.Fill(r, lit ? on : ghost);
            }
        }
    }
}
