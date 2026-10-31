using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonHud
{
    public enum ValueKind { Speed, Timer, Song }

    public static class NeonHudFeed
    {
        private static float _speed, _speedAt = -100f, _timer, _timerAt = -100f;
        private static string _song;

        private static float _relProgress, _relAt = -100f;
        private static int _relFrames = -1;
        private static bool _relActive, _relReduced;

        public static void SetBallSpeed(float speed) { _speed = speed; _speedAt = Time.unscaledTime; }
        public static void SetTimer(float seconds) { _timer = seconds; _timerAt = Time.unscaledTime; }
        public static void SetSong(string title) { _song = title; }

        public static void SetRelease(float progress, int framesLeft, bool reduced)
        {
            _relActive = true; _relProgress = progress; _relFrames = framesLeft; _relReduced = reduced; _relAt = Time.unscaledTime;
        }

        public static void SetNoRelease() { _relActive = false; _relAt = Time.unscaledTime; }

        internal static bool TryGetRelease(out bool active, out float progress, out int framesLeft, out bool reduced)
        {
            active = _relActive; progress = _relProgress; framesLeft = _relFrames; reduced = _relReduced;
            return Time.unscaledTime - _relAt < 0.5f;
        }

        internal static bool TryGet(ValueKind kind, out object value)
        {
            float now = Time.unscaledTime;
            switch (kind)
            {
                case ValueKind.Speed: value = _speed; return now - _speedAt < 1f;
                case ValueKind.Timer: value = _timer; return now - _timerAt < 1f;
                default: value = _song; return !string.IsNullOrEmpty(_song);
            }
        }
    }

    internal abstract class Source
    {
        public virtual string Label { get { return "CFG"; } }
        public abstract bool TryGet(out object value);
    }

    internal sealed class NullSource : Source
    {
        public override string Label { get { return "NO LINK"; } }
        public override bool TryGet(out object value) { value = null; return false; }
    }

    internal sealed class ChainSource : Source
    {
        private readonly Source[] _items;
        private readonly ValueKind _kind;
        private string _label;

        public ChainSource(Source[] items, ValueKind kind)
        {
            _items = items;
            _kind = kind;
            _label = items.Length > 0 ? items[0].Label : "NO LINK";
        }

        public override string Label { get { return _label; } }

        public override bool TryGet(out object value)
        {
            for (int i = 0; i < _items.Length; i++)
            {
                if (_items[i].TryGet(out value) && Usable(_kind, value))
                {
                    _label = _items[i].Label;
                    return true;
                }
            }
            value = null;
            _label = "NO LINK";
            return false;
        }

        internal static bool Usable(ValueKind kind, object v)
        {
            if (v == null) return false;
            float f;
            switch (kind)
            {
                case ValueKind.Speed: return ValueConv.ToFloat(v, out f) && f >= 0f && !float.IsNaN(f) && !float.IsInfinity(f);
                case ValueKind.Timer: return ValueConv.ToSeconds(v, out f) && f >= 0f && f < 360000f;
                default:
                    var s = v as string ?? v.ToString();
                    return s != null && s.Trim().Length > 0;
            }
        }
    }

    internal sealed class StaticSource : Source
    {
        private readonly string _text;
        public StaticSource(string text) { _text = text; }
        public override bool TryGet(out object value) { value = _text; return true; }
    }

    internal sealed class FeedSource : Source
    {
        private readonly ValueKind _kind;
        public FeedSource(ValueKind kind) { _kind = kind; }
        public override string Label { get { return "FEED"; } }
        public override bool TryGet(out object value) { return NeonHudFeed.TryGet(_kind, out value); }
    }

    internal sealed class ReflectSource : Source
    {
        private readonly ReflectionPath _path;
        public ReflectSource(ReflectionPath path) { _path = path; }
        public override string Label { get { return "MATCH"; } }
        public override bool TryGet(out object value) { return _path.TryEvaluate(out value); }
    }

    internal sealed class ElapsedSource : Source
    {
        public override string Label { get { return "ELAPSED"; } }
        public override bool TryGet(out object value) { value = Time.timeSinceLevelLoad; return true; }
    }

    internal sealed class SceneSource : Source
    {
        private string _scene, _pretty;
        public override string Label { get { return "SCENE"; } }
        public override bool TryGet(out object value)
        {
            string name = SceneManager.GetActiveScene().name;
            if (name != _scene) { _scene = name; _pretty = Pretty.Name(name); }
            value = _pretty;
            return !string.IsNullOrEmpty(_pretty);
        }
    }

    internal sealed class UiTextSource : Source
    {
        private readonly string _path;
        private Component _comp;
        private PropertyInfo _prop;
        private FieldInfo _field;
        private float _nextFind;

        public UiTextSource(string path) { _path = path; }
        public override string Label { get { return "MATCH"; } }

        public override bool TryGet(out object value)
        {
            value = null;
            if (_comp == null)
            {
                float now = Time.unscaledTime;
                if (now < _nextFind) return false;
                _nextFind = now + 1f;
                GameObject go = GameObject.Find(_path);
                if (go == null) return false;
                foreach (Component c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    PropertyInfo p; FieldInfo f;
                    if (TextAccess.Find(c.GetType(), out p, out f)) { _comp = c; _prop = p; _field = f; break; }
                }
                if (_comp == null) return false;
            }
            value = _prop != null ? _prop.GetValue(_comp, null) : _field.GetValue(_comp);
            return value != null;
        }
    }

    internal sealed class TrackSource : Source
    {
        private readonly string _name;
        private Transform _t;
        private float _nextFind, _lastMoving, _speed;
        private Vector3 _last;
        private bool _hasLast;
        private int _lastFrame = -1;

        public TrackSource(string name) { _name = name; }
        public override string Label { get { return "TRACK"; } }

        public override bool TryGet(out object value)
        {
            value = null;
            if (_t == null)
            {
                float now0 = Time.unscaledTime;
                if (now0 < _nextFind) return false;
                _nextFind = now0 + 1f;
                GameObject go = GameObject.Find(_name);
                if (go == null) return false;
                _t = go.transform;
                _hasLast = false;
            }
            if (Time.frameCount == _lastFrame) { value = _speed; return true; }
            _lastFrame = Time.frameCount;

            Vector3 p = _t.position;
            float dt = Time.deltaTime;
            if (!_hasLast || dt <= 0f) { _last = p; _hasLast = true; value = _speed; return true; }

            float raw = (p - _last).magnitude / dt;
            _last = p;
            float now = Time.unscaledTime;
            if (raw > 0.01f) { _speed = _speed <= 0f ? raw : _speed + (raw - _speed) * 0.35f; _lastMoving = now; }
            else if (now - _lastMoving > 0.75f) _speed = 0f;
            value = _speed;
            return true;
        }
    }

    internal sealed class AudioSongSource : Source
    {
        private float _next;
        private AudioClip _clip;
        private string _title;

        public override string Label { get { return "AUDIO"; } }

        public override bool TryGet(out object value)
        {
            float now = Time.unscaledTime;
            if (now >= _next)
            {
                _next = now + 1f;
                AudioSource best = null;
                float bestScore = 0f;
                foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(typeof(AudioSource)))
                {
                    var a = o as AudioSource;
                    if (a == null || !a.isPlaying || a.clip == null) continue;
                    float len = a.clip.length;
                    if (len < 30f) continue;
                    float score = len * (a.loop ? 2f : 1f);
                    if (score > bestScore) { best = a; bestScore = score; }
                }
                if (best == null) { _clip = null; _title = null; }
                else if (best.clip != _clip) { _clip = best.clip; _title = Pretty.Name(_clip.name); }
            }
            value = _title;
            return _title != null;
        }
    }

    internal static class TextAccess
    {
        private static readonly Dictionary<Type, MemberInfo> Cache = new Dictionary<Type, MemberInfo>();

        public static bool Find(Type t, out PropertyInfo prop, out FieldInfo field)
        {
            prop = null; field = null;
            MemberInfo m;
            if (!Cache.TryGetValue(t, out m))
            {
                const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                PropertyInfo p = t.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(string) && p.CanRead && p.GetIndexParameters().Length == 0) m = p;
                else
                {
                    FieldInfo fi = t.GetField("text", f) ?? t.GetField("m_text", f) ?? t.GetField("m_Text", f);
                    if (fi != null && fi.FieldType == typeof(string)) m = fi;
                }
                Cache[t] = m;
            }
            prop = m as PropertyInfo;
            field = m as FieldInfo;
            return m != null;
        }
    }

    internal sealed class SongText
    {
        private string _raw, _clean;

        public string Clean(string raw)
        {
            if (raw == null) return null;
            if (raw == _raw) return _clean;
            _raw = raw;
            _clean = Convert(raw);
            return _clean;
        }

        internal static string Convert(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            bool inTag = false;
            foreach (char ch in raw)
            {
                if (ch == '<') { inTag = true; continue; }
                if (inTag) { if (ch == '>') inTag = false; continue; }
                sb.Append(ch);
            }
            string s = sb.ToString().Trim();
            if (s.Length == 0) return null;

            int slash = s.LastIndexOfAny(new[] { '/', '\\' });
            if (slash >= 0 && slash < s.Length - 1 && s.IndexOf(' ') < 0) s = s.Substring(slash + 1);

            if (s.IndexOf(' ') < 0) return Pretty.Name(s);

            sb.Length = 0;
            bool space = false;
            foreach (char ch in s)
            {
                if (ch == '\r') continue;
                if (ch == '\n') { sb.Append(" // "); space = true; continue; }
                if (char.IsWhiteSpace(ch)) { if (!space) sb.Append(' '); space = true; continue; }
                sb.Append(ch);
                space = false;
            }
            return sb.ToString().Trim().ToUpperInvariant();
        }
    }

    internal static class Pretty
    {
        private static readonly string[] Prefixes = { "bgm_", "bgm", "music_", "mus_", "song_", "track_", "snd_" };

        public static string Name(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            string s = raw.Trim();
            string lower = s.ToLowerInvariant();
            foreach (string p in Prefixes)
                if (lower.StartsWith(p) && s.Length > p.Length) { s = s.Substring(p.Length); break; }
            if (s.EndsWith("_loop", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 5);

            var sb = new StringBuilder(s.Length + 4);
            char prev = ' ';
            foreach (char ch in s)
            {
                char c = ch == '_' || ch == '-' ? ' ' : ch;
                if (c == ' ' && prev == ' ') continue;
                if (char.IsUpper(c) && char.IsLower(prev)) sb.Append(' ');
                sb.Append(c);
                prev = c;
            }
            return sb.ToString().Trim().ToUpperInvariant();
        }
    }

    internal static class ValueConv
    {
        public static bool ToFloat(object o, out float f)
        {
            f = 0f;
            if (o == null) return false;
            if (o is float) { f = (float)o; return true; }
            if (o is int) { f = (int)o; return true; }
            if (o is double) { f = (float)(double)o; return true; }
            if (o is long) { f = (long)o; return true; }
            if (o is bool) { f = (bool)o ? 1f : 0f; return true; }
            if (o is Vector3) { f = ((Vector3)o).magnitude; return true; }
            if (o is Vector2) { f = ((Vector2)o).magnitude; return true; }
            var s = o as string;
            if (s != null) return Number(s, out f);
            var conv = o as IConvertible;
            if (conv != null)
            {
                try { f = Convert.ToSingle(o, CultureInfo.InvariantCulture); return true; }
                catch {  }
            }
            return Number(o.ToString(), out f);
        }

        public static bool ToSeconds(object o, out float secs)
        {
            var s = o as string;
            if (s != null && s.IndexOf(':') >= 0)
            {
                secs = 0f;
                string[] parts = s.Trim().Split(':');
                foreach (string part in parts)
                {
                    float v;
                    if (!Number(part, out v)) return false;
                    secs = secs * 60f + v;
                }
                return true;
            }
            return ToFloat(o, out secs);
        }

        public static bool Number(string s, out float f)
        {
            f = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            int i = 0;
            while (i < s.Length && !char.IsDigit(s[i])) i++;
            if (i >= s.Length) return false;
            bool neg = i > 0 && s[i - 1] == '-';
            var sb = new StringBuilder(12);
            bool dot = false;
            for (; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsDigit(c)) sb.Append(c);
                else if (c == '.' && !dot) { dot = true; sb.Append(c); }
                else if (c == ',') continue;
                else break;
            }
            float v;
            if (!float.TryParse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return false;
            f = neg ? -v : v;
            return true;
        }
    }

    internal static class SourceFactory
    {
        public static Source Create(string spec, ValueKind kind, ManualLogSource log)
        {
            var list = new List<Source>();
            foreach (string part in (spec ?? "").Split('|'))
            {
                Source s = CreateOne(part, kind, log);
                if (!(s is NullSource)) list.Add(s);
            }
            return list.Count == 0 ? (Source)new NullSource() : new ChainSource(list.ToArray(), kind);
        }

        private static Source CreateOne(string spec, ValueKind kind, ManualLogSource log)
        {
            spec = spec == null ? "" : spec.Trim();
            if (spec.Length == 0 || spec.Equals("none", StringComparison.OrdinalIgnoreCase)) return new NullSource();

            int colon = spec.IndexOf(':');
            string head = (colon < 0 ? spec : spec.Substring(0, colon)).Trim().ToLowerInvariant();
            string arg = colon < 0 ? "" : spec.Substring(colon + 1).Trim();
            try
            {
                switch (head)
                {
                    case "feed": return new FeedSource(kind);
                    case "elapsed": return new ElapsedSource();
                    case "audio": return new AudioSongSource();
                    case "scene": return new SceneSource();
                    case "static": return new StaticSource(arg);
                    case "reflect": return new ReflectSource(ReflectionPath.Parse(arg, false, log));
                    case "find": return new ReflectSource(ReflectionPath.Parse(arg, true, log));
                    case "ui": return new UiTextSource(arg);
                    case "track": return new TrackSource(arg);
                }
                log.LogWarning("Unknown " + kind + " source '" + spec + "'");
            }
            catch (Exception ex)
            {
                log.LogWarning("Bad " + kind + " source '" + spec + "': " + ex.Message);
            }
            return new NullSource();
        }
    }
}
