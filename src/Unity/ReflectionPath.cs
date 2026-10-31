using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace NeonHud
{
    internal sealed class ReflectionPath
    {
        private sealed class Seg
        {
            public string Name;
            public object[] Args;
            public int[] Indexes;
            public Type CachedFor;
            public FieldInfo Field;
            public PropertyInfo Prop;
            public MethodInfo Method;
            public object[] ConvertedArgs;
        }

        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                         BindingFlags.Instance | BindingFlags.Static;

        private readonly string _text;
        private readonly string _typeName;
        private readonly Seg[] _segs;
        private readonly bool _findRoot;
        private readonly ManualLogSource _log;

        private Type _type;
        private float _nextTypeLookup;
        private float _typeRetry = 2f;
        private UnityEngine.Object _root;
        private float _nextFind;
        private string _lastError;

        private ReflectionPath(string text, string typeName, Seg[] segs, bool findRoot, ManualLogSource log)
        {
            _text = text; _typeName = typeName; _segs = segs; _findRoot = findRoot; _log = log;
        }

        public static ReflectionPath Parse(string text, bool findRoot, ManualLogSource log)
        {
            int sep = text.IndexOf("::", StringComparison.Ordinal);
            string typeName = (sep < 0 ? text : text.Substring(0, sep)).Trim();
            string rest = sep < 0 ? "" : text.Substring(sep + 2).Trim();
            if (typeName.Length == 0) throw new FormatException("missing type name before '::'");
            if (rest.Length == 0 && !findRoot) throw new FormatException("missing member after '::'");

            var segs = new List<Seg>();
            foreach (string raw in SplitMembers(rest))
            {
                string part = raw.Trim();
                if (part.Length == 0) continue;
                var seg = new Seg();
                int i = 0;
                while (i < part.Length && part[i] != '(' && part[i] != '[') i++;
                seg.Name = part.Substring(0, i).Trim();
                if (i < part.Length && part[i] == '(')
                {
                    int close = part.IndexOf(')', i);
                    if (close < 0) throw new FormatException("unclosed '(' in " + part);
                    seg.Args = ParseArgs(part.Substring(i + 1, close - i - 1));
                    i = close + 1;
                }
                var idx = new List<int>();
                while (i < part.Length && part[i] == '[')
                {
                    int close = part.IndexOf(']', i);
                    if (close < 0) throw new FormatException("unclosed '[' in " + part);
                    idx.Add(int.Parse(part.Substring(i + 1, close - i - 1).Trim(), CultureInfo.InvariantCulture));
                    i = close + 1;
                }
                seg.Indexes = idx.ToArray();
                segs.Add(seg);
            }
            return new ReflectionPath(text, typeName, segs.ToArray(), findRoot, log);
        }

        private static IEnumerable<string> SplitMembers(string s)
        {
            int depth = 0, start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                else if (ch == '.' && depth == 0)
                {
                    yield return s.Substring(start, i - start);
                    start = i + 1;
                }
            }
            if (start < s.Length) yield return s.Substring(start);
        }

        private static object[] ParseArgs(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return new object[0];
            string[] parts = s.Split(',');
            var args = new object[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                int iv; double dv; bool bv;
                if (p.StartsWith("\"") && p.EndsWith("\"") && p.Length >= 2) args[i] = p.Substring(1, p.Length - 2);
                else if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv)) args[i] = iv;
                else if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out dv)) args[i] = dv;
                else if (bool.TryParse(p, out bv)) args[i] = bv;
                else args[i] = p;
            }
            return args;
        }

        public bool TryEvaluate(out object value)
        {
            value = null;
            try
            {
                if (!ResolveType()) return false;

                object cur = null;
                if (_findRoot)
                {
                    if (_root == null)
                    {
                        float now = Time.unscaledTime;
                        if (now < _nextFind) return false;
                        _nextFind = now + 0.5f;
                        _root = UnityEngine.Object.FindObjectOfType(_type);
                        if (_root == null) return false;
                    }
                    cur = _root;
                }

                for (int i = 0; i < _segs.Length; i++)
                {
                    Seg seg = _segs[i];
                    Type lookup = cur != null ? cur.GetType() : _type;
                    if (seg.CachedFor != lookup && !Bind(seg, lookup)) return false;
                    if (cur == null && !IsStaticSeg(seg))
                    {
                        Warn("'" + seg.Name + "' is an instance member; use find: instead of reflect:, or start from a static member");
                        return false;
                    }

                    object next;
                    if (seg.Method != null) next = seg.Method.Invoke(seg.Method.IsStatic ? null : cur, seg.ConvertedArgs);
                    else if (seg.Field != null) next = seg.Field.GetValue(seg.Field.IsStatic ? null : cur);
                    else
                    {
                        MethodInfo getter = seg.Prop.GetGetMethod(true);
                        next = getter.Invoke(getter.IsStatic ? null : cur, null);
                    }

                    for (int j = 0; j < seg.Indexes.Length && next != null; j++)
                        next = Index(next, seg.Indexes[j]);

                    if (next == null) return false;
                    cur = next;
                }

                value = cur;
                _lastError = null;
                return cur != null;
            }
            catch (Exception ex)
            {
                Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                Warn(inner.GetType().Name + ": " + inner.Message);
                return false;
            }
        }

        private static bool IsStaticSeg(Seg seg)
        {
            if (seg.Method != null) return seg.Method.IsStatic;
            if (seg.Field != null) return seg.Field.IsStatic;
            MethodInfo g = seg.Prop.GetGetMethod(true);
            return g != null && g.IsStatic;
        }

        private static object Index(object o, int i)
        {
            var arr = o as Array;
            if (arr != null) return i >= 0 && i < arr.Length ? arr.GetValue(i) : null;
            var list = o as IList;
            if (list != null) return i >= 0 && i < list.Count ? list[i] : null;
            return null;
        }

        private bool Bind(Seg seg, Type type)
        {
            seg.Field = null; seg.Prop = null; seg.Method = null; seg.ConvertedArgs = null;
            for (Type t = type; t != null; t = t.BaseType)
            {
                if (seg.Args != null)
                {
                    foreach (MethodInfo m in t.GetMethods(All | BindingFlags.DeclaredOnly))
                    {
                        if (m.Name != seg.Name) continue;
                        ParameterInfo[] ps = m.GetParameters();
                        if (ps.Length != seg.Args.Length) continue;
                        object[] conv = new object[ps.Length];
                        bool ok = true;
                        for (int i = 0; i < ps.Length && ok; i++)
                        {
                            try { conv[i] = ps[i].ParameterType.IsEnum
                                    ? Enum.ToObject(ps[i].ParameterType, seg.Args[i])
                                    : Convert.ChangeType(seg.Args[i], ps[i].ParameterType, CultureInfo.InvariantCulture); }
                            catch { ok = false; }
                        }
                        if (!ok) continue;
                        seg.Method = m; seg.ConvertedArgs = conv;
                        break;
                    }
                    if (seg.Method != null) break;
                }
                else
                {
                    seg.Field = t.GetField(seg.Name, All | BindingFlags.DeclaredOnly);
                    if (seg.Field != null) break;
                    seg.Prop = t.GetProperty(seg.Name, All | BindingFlags.DeclaredOnly);
                    if (seg.Prop != null && seg.Prop.GetIndexParameters().Length == 0 && seg.Prop.GetGetMethod(true) != null) break;
                    seg.Prop = null;
                }
            }
            if (seg.Field == null && seg.Prop == null && seg.Method == null)
            {
                Warn("no member '" + seg.Name + (seg.Args != null ? "(" + seg.Args.Length + " args)" : "") + "' on " + type.FullName);
                seg.CachedFor = null;
                return false;
            }
            seg.CachedFor = type;
            return true;
        }

        private bool ResolveType()
        {
            if (_type != null) return true;
            float now = Time.unscaledTime;
            if (now < _nextTypeLookup) return false;
            _type = FindType(_typeName);
            if (_type == null)
            {
                _nextTypeLookup = now + _typeRetry;
                _typeRetry = Math.Min(30f, _typeRetry * 2f);
                Warn("type '" + _typeName + "' not found (will retry)");
                return false;
            }
            _log.LogInfo("Linked '" + _text + "' -> " + _type.AssemblyQualifiedName);
            return true;
        }

        internal static Type FindType(string name)
        {
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly a in asms)
            {
                Type t = a.GetType(name, false);
                if (t != null) return t;
            }
            foreach (Assembly a in asms)
            {
                string an;
                try { an = a.GetName().Name; }
                catch { continue; }
                if (an == null || !an.StartsWith("Assembly-CSharp", StringComparison.Ordinal)) continue;
                Type[] types;
                try { types = a.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                catch { continue; }
                foreach (Type t in types)
                    if (t != null && (t.Name == name || t.FullName == name)) return t;
            }
            return null;
        }

        private void Warn(string msg)
        {
            if (msg == _lastError) return;
            _lastError = msg;
            _log.LogWarning("[" + _text + "] " + msg);
        }
    }
}
