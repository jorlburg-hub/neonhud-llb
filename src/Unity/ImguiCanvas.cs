using System;
using System.Collections.Generic;
using NeonHud.Core;
using UnityEngine;

namespace NeonHud
{
    internal sealed class ImguiCanvas : ICanvas
    {
        private Texture2D _white, _scan, _stripes;
        private readonly Dictionary<int, Texture2D> _fillTex = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, Texture2D> _edgeTex = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, RectOffset> _borders = new Dictionary<int, RectOffset>();
        private readonly Dictionary<int, GUIStyle> _textStyles = new Dictionary<int, GUIStyle>();
        private readonly GUIStyle _panelStyle = new GUIStyle();
        private readonly GUIContent _content = new GUIContent();
        private readonly Stack<Vector2> _groups = new Stack<Vector2>();
        private float _ox, _oy;
        private Color _savedColor, _savedBg;
        private Font _font;

        public Font Font
        {
            get { return _font; }
            set { _font = value; _textStyles.Clear(); }
        }

        public void Begin()
        {
            EnsureBasics();
            _ox = 0f; _oy = 0f;
            _savedColor = GUI.color;
            _savedBg = GUI.backgroundColor;
        }

        public void End()
        {
            while (_groups.Count > 0) PopClip();
            GUI.color = _savedColor;
            GUI.backgroundColor = _savedBg;
        }

        public void Fill(RectF r, Col c)
        {
            if (c.A <= 0f || r.W <= 0f || r.H <= 0f) return;
            GUI.color = ToColor(c);
            GUI.DrawTexture(ToRect(r), _white);
        }

        public void Panel(RectF r, float chamferTL, float chamferBR, Col fill, Col edge, float edgeWidth)
        {
            int tl = Math.Max(0, (int)Math.Round(chamferTL));
            int br = Math.Max(0, (int)Math.Round(chamferBR));
            int ew = edge.A > 0f && edgeWidth > 0f ? Math.Max(1, (int)Math.Round(edgeWidth)) : 0;
            int key = tl | (br << 10) | (ew << 20);

            Texture2D fillTex, edgeTex;
            RectOffset border;
            if (!_fillTex.TryGetValue(key, out fillTex) || fillTex == null)
            {
                BuildPanelTextures(tl, br, Math.Max(1, ew), out fillTex, out edgeTex, out border);
                _fillTex[key] = fillTex;
                _edgeTex[key] = edgeTex;
                _borders[key] = border;
            }
            edgeTex = _edgeTex[key];
            border = _borders[key];

            Rect rect = ToRect(r);
            _panelStyle.border = border;
            GUI.backgroundColor = Color.white;
            if (fill.A > 0f)
            {
                GUI.color = ToColor(fill);
                _panelStyle.normal.background = fillTex;
                _panelStyle.Draw(rect, false, false, false, false);
            }
            if (ew > 0)
            {
                GUI.color = ToColor(edge);
                _panelStyle.normal.background = edgeTex;
                _panelStyle.Draw(rect, false, false, false, false);
            }
        }

        public void Text(RectF r, string s, float size, Col c, TextAlign align, bool bold)
        {
            if (string.IsNullOrEmpty(s) || c.A <= 0f) return;
            GUIStyle st = Style(size, bold);
            st.alignment = align == TextAlign.Left ? TextAnchor.MiddleLeft
                         : align == TextAlign.Center ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
            st.normal.textColor = ToColor(c);
            GUI.color = Color.white;
            _content.text = s;
            GUI.Label(ToRect(r), _content, st);
        }

        public float Measure(string s, float size, bool bold)
        {
            if (string.IsNullOrEmpty(s)) return 0f;
            _content.text = s;
            return Style(size, bold).CalcSize(_content).x;
        }

        public void PushClip(RectF r)
        {
            GUI.BeginGroup(ToRect(r));
            _groups.Push(new Vector2(_ox, _oy));
            _ox = r.X; _oy = r.Y;
        }

        public void PopClip()
        {
            if (_groups.Count == 0) return;
            GUI.EndGroup();
            Vector2 o = _groups.Pop();
            _ox = o.x; _oy = o.y;
        }

        public void Scanlines(RectF r, float period, Col c)
        {
            if (c.A <= 0f || period <= 0f) return;
            GUI.color = ToColor(c);
            GUI.DrawTextureWithTexCoords(ToRect(r), _scan, new Rect(0f, 0f, 1f, r.H / period));
        }

        public void Stripes(RectF r, float period, Col c)
        {
            if (c.A <= 0f || period <= 0f) return;
            GUI.color = ToColor(c);
            GUI.DrawTextureWithTexCoords(ToRect(r), _stripes, new Rect(0f, 0f, r.W / period, r.H / period));
        }

        private Rect ToRect(RectF r) { return new Rect(r.X - _ox, r.Y - _oy, r.W, r.H); }
        private static Color ToColor(Col c) { return new Color(c.R, c.G, c.B, c.A); }

        private GUIStyle Style(float size, bool bold)
        {
            int px = Math.Max(1, (int)Math.Round(size));
            int key = px * 2 + (bold ? 1 : 0);
            GUIStyle st;
            if (!_textStyles.TryGetValue(key, out st))
            {
                st = new GUIStyle();
                st.font = _font;
                st.fontSize = px;
                st.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
                st.wordWrap = false;
                st.richText = false;
                st.clipping = TextClipping.Overflow;
                _textStyles[key] = st;
            }
            return st;
        }

        private static Texture2D NewTex(int w, int h, FilterMode filter, TextureWrapMode wrap)
        {
            var t = new Texture2D(w, h, TextureFormat.ARGB32, false);
            t.filterMode = filter;
            t.wrapMode = wrap;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        private void EnsureBasics()
        {
            if (_white == null)
            {
                _white = NewTex(1, 1, FilterMode.Point, TextureWrapMode.Clamp);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            if (_scan == null)
            {
                _scan = NewTex(1, 3, FilterMode.Point, TextureWrapMode.Repeat);
                _scan.SetPixel(0, 0, new Color(1f, 1f, 1f, 1f));
                _scan.SetPixel(0, 1, new Color(1f, 1f, 1f, 0f));
                _scan.SetPixel(0, 2, new Color(1f, 1f, 1f, 0f));
                _scan.Apply();
            }
            if (_stripes == null)
            {
                const int n = 32;
                _stripes = NewTex(n, n, FilterMode.Bilinear, TextureWrapMode.Repeat);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        _stripes.SetPixel(x, y, new Color(1f, 1f, 1f, ((x + y) % n) < n / 2 ? 1f : 0f));
                _stripes.Apply();
            }
        }

        private static void BuildPanelTextures(int tl, int br, int ew, out Texture2D fill, out Texture2D edge, out RectOffset border)
        {
            int lead = Math.Max(tl, ew) + 2;
            int trail = Math.Max(br, ew) + 2;
            int size = lead + trail + 2;
            fill = NewTex(size, size, FilterMode.Bilinear, TextureWrapMode.Clamp);
            edge = NewTex(size, size, FilterMode.Bilinear, TextureWrapMode.Clamp);
            const float invSqrt2 = 0.70710678f;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = px + 0.5f, y = py + 0.5f;
                    float dLeft = x, dTop = y, dRight = size - x, dBottom = size - y;
                    float dTL = tl > 0 ? (x + y - tl) * invSqrt2 : 1e6f;
                    float dBR = br > 0 ? (dRight + dBottom - br) * invSqrt2 : 1e6f;

                    float inside = Math.Min(dTL, dBR);
                    float cover = inside >= 0.5f ? 1f : (inside <= -0.5f ? 0f : inside + 0.5f);

                    float dist = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
                    dist = Math.Min(dist, inside);
                    float e = ew + 0.5f - dist;
                    e = e < 0f ? 0f : (e > 1f ? 1f : e);

                    int ty = size - 1 - py;
                    fill.SetPixel(px, ty, new Color(1f, 1f, 1f, cover));
                    edge.SetPixel(px, ty, new Color(1f, 1f, 1f, cover * e));
                }
            }
            fill.Apply();
            edge.Apply();
            border = new RectOffset(lead, trail, lead, trail);
        }

        public void Release()
        {
            foreach (var t in _fillTex.Values) if (t != null) UnityEngine.Object.Destroy(t);
            foreach (var t in _edgeTex.Values) if (t != null) UnityEngine.Object.Destroy(t);
            _fillTex.Clear(); _edgeTex.Clear(); _borders.Clear();
            if (_white != null) UnityEngine.Object.Destroy(_white);
            if (_scan != null) UnityEngine.Object.Destroy(_scan);
            if (_stripes != null) UnityEngine.Object.Destroy(_stripes);
        }
    }
}
