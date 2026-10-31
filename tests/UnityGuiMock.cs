using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static Vector2 one { get { return new Vector2(1, 1); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public override string ToString() { return "(" + x + ", " + y + ")"; }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMin { get { return x; } }
        public float yMin { get { return y; } }
        public float xMax { get { return x + width; } }
        public float yMax { get { return y + height; } }
        public Vector2 center { get { return new Vector2(x + width / 2f, y + height / 2f); } }
        public Vector2 position { get { return new Vector2(x, y); } }
        public Vector2 size { get { return new Vector2(width, height); } }
        public bool Contains(Vector2 p) { return p.x >= x && p.x < x + width && p.y >= y && p.y < y + height; }
        public override string ToString() { return string.Format("(x:{0}, y:{1}, width:{2}, height:{3})", x, y, width, height); }
    }

    public enum KeyCode
    {
        None = 0, Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32,
        Alpha0 = 48, A = 97, B = 98, C = 99, D = 100, P = 112, Delete = 127,
        F1 = 282, F2 = 283, F3 = 284, F4 = 285, F5 = 286, F6 = 287, F7 = 288, F8 = 289, F9 = 290, F10 = 291, F11 = 292, F12 = 293,
        RightShift = 303, LeftShift = 304, RightControl = 305, LeftControl = 306, RightAlt = 307, LeftAlt = 308,
        Mouse0 = 323, Mouse1 = 324, Mouse2 = 325, Mouse3 = 326, Mouse4 = 327, Mouse5 = 328, Mouse6 = 329
    }

    public sealed class Input
    {
        public static readonly HashSet<KeyCode> Down = new HashSet<KeyCode>(), Held = new HashSet<KeyCode>();
        public static readonly HashSet<int> MouseHeld = new HashSet<int>();
        public static bool GetKeyDown(KeyCode key) { return Down.Contains(key); }
        public static bool GetKey(KeyCode key) { return Held.Contains(key) || Down.Contains(key); }
        public static bool GetKeyUp(KeyCode key) { return false; }
        public static bool GetMouseButton(int button) { return MouseHeld.Contains(button); }
        public static bool anyKeyDown { get { return Down.Count > 0; } }
    }

    public sealed class Time
    {
        public static float realtimeSinceStartup { get { return (float)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency; } }
        public static float unscaledTime { get; set; }
        public static float time { get { return unscaledTime; } }
        public static float deltaTime { get; set; }
        public static float unscaledDeltaTime { get; set; }
        public static float timeSinceLevelLoad { get { return unscaledTime; } }
        public static int frameCount { get; set; }
    }

    public sealed class Screen
    {
        public static int width { get { return W; } }
        public static int height { get { return H; } }
        public static int W = 1920, H = 1080;
    }

    public enum CursorLockMode { None = 0, Locked = 1, Confined = 2 }
    public class Cursor
    {
        public static bool visible { get; set; }
        public static CursorLockMode lockState { get; set; }
    }

    public class Font : Object { }
    public enum FontStyle { Normal = 0, Bold = 1, Italic = 2, BoldAndItalic = 3 }
    public enum TextAnchor { UpperLeft = 0, UpperCenter = 1, UpperRight = 2, MiddleLeft = 3, MiddleCenter = 4, MiddleRight = 5, LowerLeft = 6, LowerCenter = 7, LowerRight = 8 }
    public enum TextClipping { Overflow = 0, Clip = 1 }
    public enum EventType { MouseDown = 0, MouseUp = 1, MouseMove = 2, MouseDrag = 3, KeyDown = 4, KeyUp = 5, ScrollWheel = 6, Repaint = 7, Layout = 8, Used = 12 }

    public sealed class Event
    {
        public static Event current { get; set; }
        public EventType type { get; set; }
        public int button { get; set; }
        public Vector2 mousePosition { get; set; }
        public void Use() { type = EventType.Used; }
    }

    public class RectOffset
    {
        public RectOffset() { }
        public RectOffset(int left, int right, int top, int bottom) { this.left = left; this.right = right; this.top = top; this.bottom = bottom; }
        public int left { get; set; }
        public int right { get; set; }
        public int top { get; set; }
        public int bottom { get; set; }
    }

    public class GUIContent
    {
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
        public string text { get; set; }
    }

    public class GUIStyleState
    {
        public Texture2D background { get; set; }
        public Color textColor { get; set; }
    }

    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other)
        {
            if (other == null) return;
            fontSize = other.fontSize; fontStyle = other.fontStyle; alignment = other.alignment; wordWrap = other.wordWrap;
            richText = other.richText; clipping = other.clipping; font = other.font; fixedWidth = other.fixedWidth; fixedHeight = other.fixedHeight;
            normal.textColor = other.normal.textColor; normal.background = other.normal.background;
        }
        public static GUIStyle none { get { return new GUIStyle(); } }
        private readonly GUIStyleState _n = new GUIStyleState(), _h = new GUIStyleState(), _a = new GUIStyleState(), _f = new GUIStyleState(),
            _on = new GUIStyleState(), _oh = new GUIStyleState(), _oa = new GUIStyleState();
        public GUIStyleState normal { get { return _n; } set { } }
        public GUIStyleState hover { get { return _h; } set { } }
        public GUIStyleState active { get { return _a; } set { } }
        public GUIStyleState focused { get { return _f; } set { } }
        public GUIStyleState onNormal { get { return _on; } set { } }
        public GUIStyleState onHover { get { return _oh; } set { } }
        public GUIStyleState onActive { get { return _oa; } set { } }
        public RectOffset border { get; set; }
        public RectOffset margin { get; set; }
        public RectOffset padding { get; set; }
        public RectOffset overflow { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public bool wordWrap { get; set; }
        public bool richText { get; set; }
        public TextClipping clipping { get; set; }
        public TextAnchor alignment { get; set; }
        public float fixedWidth { get; set; }
        public float fixedHeight { get; set; }
        public bool stretchWidth { get; set; }
        public bool stretchHeight { get; set; }
        private float Fs { get { return fontSize > 0 ? fontSize : 13; } }
        public Vector2 CalcSize(GUIContent content)
        {
            string t = content != null ? content.text ?? "" : "";
            return new Vector2(t.Length * Fs * 0.55f + 8f, Fs * 1.4f);
        }
        public float CalcHeight(GUIContent content, float width)
        {
            float w = CalcSize(content).x;
            int lines = width > 0 ? Math.Max(1, (int)Math.Ceiling(w / width)) : 1;
            return lines * Fs * 1.3f;
        }
        public void Draw(Rect position, bool isHover, bool isActive, bool on, bool hasKeyboardFocus) { }
    }

    public sealed class GUISkin : Object
    {
        public GUIStyle label { get; set; }
        public GUIStyle button { get; set; }
        public GUIStyle textField { get; set; }
        public GUIStyle horizontalSlider { get; set; }
        public GUIStyle horizontalSliderThumb { get; set; }
        public GUIStyle box { get; set; }
        public GUIStyle window { get; set; }
        public GUISkin()
        {
            label = new GUIStyle(); button = new GUIStyle(); textField = new GUIStyle(); horizontalSlider = new GUIStyle();
            horizontalSliderThumb = new GUIStyle(); box = new GUIStyle(); window = new GUIStyle();
        }
    }

    public class GUI
    {
        private static readonly GUISkin _skin = new GUISkin();
        public static GUISkin skin { get { return _skin; } set { } }
        public static Color color { get; set; }
        public static Color backgroundColor { get; set; }
        public static Color contentColor { get; set; }
        public static int depth { get; set; }
        public static bool enabled { get; set; }
        public delegate void WindowFunction(int id);

        public static Func<string, bool> ClickHook;
        public static Func<float, float> SliderHook;
        public static Func<string, string> TextHook;
        public static string LastLabel;
        public static readonly List<string> Drawn = new List<string>();
        public static readonly List<int> Windows = new List<int>();
        public static int Front;

        public struct Item { public string Kind, Text; public Rect R; public Color C; }
        public static List<Item> Record;
        private static readonly Stack<Vector2> _origins = new Stack<Vector2>();
        private static Vector2 _o;
        private static void Rec(string kind, Rect r, string text)
        {
            if (Record == null) return;
            Record.Add(new Item { Kind = kind, Text = text, R = new Rect(r.x + _o.x, r.y + _o.y, r.width, r.height), C = color });
        }
        private static void Push(float x, float y) { _origins.Push(_o); _o = new Vector2(_o.x + x, _o.y + y); }
        private static void Pop() { _o = _origins.Count > 0 ? _origins.Pop() : new Vector2(0, 0); }

        public static void Reset() { ClickHook = null; SliderHook = null; TextHook = null; LastLabel = null; Drawn.Clear(); Windows.Clear(); }

        public static void Label(Rect position, string text) { LastLabel = text; Drawn.Add(text); Rec("label", position, text); }
        public static void Label(Rect position, string text, GUIStyle style) { Label(position, text); }
        public static void Label(Rect position, GUIContent content, GUIStyle style) { Label(position, content != null ? content.text : null); }
        public static void Box(Rect position, string text, GUIStyle style) { Drawn.Add(text); }
        public static bool Button(Rect position, string text, GUIStyle style)
        {
            Drawn.Add("[" + text + "]");
            Rec("button", position, text);
            return ClickHook != null && ClickHook(text);
        }
        public static float HorizontalSlider(Rect position, float value, float leftValue, float rightValue, GUIStyle slider, GUIStyle thumb)
        {
            Rec("slider", position, ((rightValue - leftValue) != 0 ? (value - leftValue) / (rightValue - leftValue) : 0f).ToString(System.Globalization.CultureInfo.InvariantCulture));
            return SliderHook != null ? SliderHook(value) : value;
        }
        public static string TextField(Rect position, string text, int maxLength, GUIStyle style)
        {
            Rec("field", position, text);
            string t = TextHook != null ? TextHook(text) : text;
            if (t != null && maxLength >= 0 && t.Length > maxLength) t = t.Substring(0, maxLength);
            return t;
        }
        public static void DrawTexture(Rect position, Texture image) { Rec("tex", position, image != null ? image.name : null); }
        public static void DrawTextureWithTexCoords(Rect position, Texture image, Rect texCoords) { }
        public static Rect Window(int id, Rect clientRect, WindowFunction func, string text, GUIStyle style)
        {
            Windows.Add(id);
            Rec("window", clientRect, text);
            Push(clientRect.x, clientRect.y);
            try { func(id); } finally { Pop(); if (Record != null) Record.Add(new Item { Kind = "end" }); }
            return clientRect;
        }
        public static void BringWindowToFront(int windowID) { Front = windowID; }
        public static Vector2 BeginScrollView(Rect position, Vector2 scrollPosition, Rect viewRect)
        {
            Rec("scroll", position, null);
            Push(position.x - scrollPosition.x, position.y - scrollPosition.y);
            return scrollPosition;
        }
        public static void EndScrollView() { Pop(); if (Record != null) Record.Add(new Item { Kind = "end" }); }
        private static int _groups;
        public static void BeginGroup(Rect position) { _groups++; Rec("group", position, null); Push(position.x, position.y); }
        public static void EndGroup() { if (--_groups < 0) throw new InvalidOperationException("EndGroup without BeginGroup"); Pop(); if (Record != null) Record.Add(new Item { Kind = "end" }); }
        public static int OpenGroups { get { return _groups; } }
    }
}
