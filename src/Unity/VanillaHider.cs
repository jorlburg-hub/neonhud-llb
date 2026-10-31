using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace NeonHud
{
    internal sealed class VanillaHider
    {
        private sealed class Target
        {
            public string Path;
            public ReflectionPath Member;
            public Transform Found;
            public Vector3 Orig;
        }

        private readonly List<Target> _targets = new List<Target>();
        private float _nextFind;

        public void Configure(IEnumerable<string> entries, ManualLogSource log)
        {
            Restore();
            _targets.Clear();
            foreach (string raw in entries)
            {
                string e = raw.Trim();
                if (e.Length == 0) continue;
                var t = new Target { Path = e };
                try
                {
                    if (e.StartsWith("find:", StringComparison.OrdinalIgnoreCase)) t.Member = ReflectionPath.Parse(e.Substring(5), true, log);
                    else if (e.StartsWith("reflect:", StringComparison.OrdinalIgnoreCase)) t.Member = ReflectionPath.Parse(e.Substring(8), false, log);
                }
                catch (Exception ex) { log.LogWarning("Bad vanilla HUD entry '" + e + "': " + ex.Message); continue; }
                _targets.Add(t);
            }
            _nextFind = 0f;
        }

        public static IEnumerable<string> Split(string list)
        {
            if (string.IsNullOrEmpty(list)) yield break;
            int depth = 0, start = 0;
            for (int i = 0; i < list.Length; i++)
            {
                char c = list[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if ((c == ';' || c == ',') && depth == 0)
                {
                    yield return list.Substring(start, i - start);
                    start = i + 1;
                }
            }
            yield return list.Substring(start);
        }

        public void Apply(bool hide)
        {
            if (_targets.Count == 0) return;
            if (!hide) { Restore(); return; }

            float now = Time.unscaledTime;
            if (now >= _nextFind)
            {
                _nextFind = now + 0.5f;
                for (int i = 0; i < _targets.Count; i++)
                {
                    Target t = _targets[i];
                    if (t.Found != null) continue;
                    Transform tf = Resolve(t);
                    if (tf == null) continue;
                    t.Found = tf;
                    t.Orig = tf.localScale;
                }
            }
            for (int i = 0; i < _targets.Count; i++)
            {
                Transform tf = _targets[i].Found;
                if (tf != null && tf.localScale != Vector3.zero) tf.localScale = Vector3.zero;
            }
        }

        private static Transform Resolve(Target t)
        {
            if (t.Member == null)
            {
                GameObject go = GameObject.Find(t.Path);
                return go != null ? go.transform : null;
            }
            object v;
            if (!t.Member.TryEvaluate(out v)) return null;
            var c = v as Component;
            if (c != null) return c.transform;
            var g = v as GameObject;
            return g != null ? g.transform : null;
        }

        public void Restore()
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                Target t = _targets[i];
                if (t.Found != null) t.Found.localScale = t.Orig;
                t.Found = null;
            }
        }
    }
}
