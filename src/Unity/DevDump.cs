using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonHud
{
    internal static class DevDump
    {
        private static readonly string[] Keywords = { "speed", "velocity", "timer", "time", "clock", "song", "music", "bgm", "track", "ball" };
        private const int MaxLines = 20000;

        public static string Write()
        {
            var sb = new StringBuilder(1 << 16);
            int lines = 0;
            Action<string> L = s => { if (lines++ < MaxLines) sb.Append(s).Append('\n'); };

            L("NeonHUD dump  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            L("Scene: " + SceneManager.GetActiveScene().name + "   Screen: " + Screen.width + "x" + Screen.height + "   Unity " + Application.unityVersion);
            L("");

            var all = new List<Transform>();
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(typeof(Transform)))
            {
                var tr = o as Transform;
                if (tr != null) all.Add(tr);
            }
            var roots = new List<Transform>();
            foreach (Transform t in all) if (t.parent == null) roots.Add(t);
            roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            L("=== TEXT OBJECTS (paste a line's spec into Data.*Source) ===");
            foreach (Transform r in roots) WalkText(r, Path(r), L);
            L("");

            L("=== OBJECTS WITH KEYWORD NAMES (track:/ui: candidates) ===");
            foreach (Transform t in all)
            {
                string n = t.name.ToLowerInvariant();
                if (Matches(n)) L("track:" + Path(t) + "    components: " + Components(t.gameObject));
            }
            L("");

            L("=== PLAYING AUDIO ===");
            foreach (UnityEngine.Object o in UnityEngine.Object.FindObjectsOfType(typeof(AudioSource)))
            {
                var a = o as AudioSource;
                if (a == null || a.clip == null) continue;
                L((a.isPlaying ? "[playing] " : "[stopped] ") + a.clip.name + "  len=" + a.clip.length.ToString("0.0") + "s loop=" + a.loop + "  on " + Path(a.transform));
            }
            L("");

            L("=== LOADED FONTS (for General.FontName) ===");
            var fonts = new SortedDictionary<string, bool>();
            foreach (UnityEngine.Object f in Resources.FindObjectsOfTypeAll(typeof(Font))) fonts[f.name] = true;
            foreach (string f in fonts.Keys) L("  " + f);
            L("");

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string an = asm.GetName().Name;
                if (an != "Assembly-CSharp" && !an.StartsWith("LLB", StringComparison.OrdinalIgnoreCase)) continue;
                L("=== MEMBERS IN " + an + " (reflect: = static, find: = on a scene object) ===");
                DumpMembers(asm, L);
                L("");
            }

            L("=== FULL HIERARCHY (active objects) ===");
            foreach (Transform r in roots) WalkAll(r, 0, L);
            if (lines >= MaxLines) sb.Append("... truncated at " + MaxLines + " lines\n");

            string path = System.IO.Path.Combine(Paths.BepInExRootPath, "NeonHUD_dump.txt");
            File.WriteAllText(path, sb.ToString());
            return path;
        }

        private static bool Matches(string lower)
        {
            foreach (string k in Keywords) if (lower.IndexOf(k, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string Components(GameObject go)
        {
            var names = new List<string>();
            foreach (Component c in go.GetComponents<Component>())
                names.Add(c == null ? "<missing>" : c.GetType().Name);
            return string.Join(", ", names.ToArray());
        }

        private static void WalkText(Transform t, string path, Action<string> L)
        {
            foreach (Component c in t.GetComponents<Component>())
            {
                if (c == null) continue;
                PropertyInfo p; FieldInfo f;
                if (!TextAccess.Find(c.GetType(), out p, out f)) continue;
                object v = null;
                try { v = p != null ? p.GetValue(c, null) : f.GetValue(c); } catch { }
                string s = v as string;
                if (s == null) continue;
                if (s.Length > 60) s = s.Substring(0, 60) + "...";
                L("ui:" + path + "    [" + c.GetType().Name + "]  \"" + s.Replace("\n", "\\n") + "\"" +
                  (t.gameObject.activeInHierarchy ? "" : "  (inactive)"));
            }
            for (int i = 0; i < t.childCount; i++)
            {
                Transform ch = t.GetChild(i);
                WalkText(ch, path + "/" + ch.name, L);
            }
        }

        private static void WalkAll(Transform t, int depth, Action<string> L)
        {
            L(new string(' ', depth * 2) + t.name + (t.gameObject.activeInHierarchy ? "" : " [inactive]") + "  {" + Components(t.gameObject) + "}");
            for (int i = 0; i < t.childCount; i++) WalkAll(t.GetChild(i), depth + 1, L);
        }

        private static void DumpMembers(Assembly asm, Action<string> L)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                   BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (Type t in types)
            {
                if (t == null) continue;
                bool typeHit = Matches(t.Name.ToLowerInvariant());
                string tn = t.FullName;
                string prefixStatic = "reflect:" + tn + "::";
                string prefixFind = typeof(UnityEngine.Object).IsAssignableFrom(t) ? "find:" + tn + "::" : null;

                FieldInfo[] fields; PropertyInfo[] props; MethodInfo[] methods;
                try { fields = t.GetFields(F); props = t.GetProperties(F); methods = t.GetMethods(F); }
                catch { continue; }

                foreach (FieldInfo fi in fields)
                {
                    if (!typeHit && !Matches(fi.Name.ToLowerInvariant())) continue;
                    string pre = fi.IsStatic ? prefixStatic : prefixFind;
                    if (pre == null) continue;
                    L(pre + fi.Name + "    : " + fi.FieldType.Name);
                }
                foreach (PropertyInfo pi in props)
                {
                    MethodInfo g = pi.GetGetMethod(true);
                    if (g == null || pi.GetIndexParameters().Length > 0) continue;
                    if (!typeHit && !Matches(pi.Name.ToLowerInvariant())) continue;
                    string pre = g.IsStatic ? prefixStatic : prefixFind;
                    if (pre == null) continue;
                    L(pre + pi.Name + "    : " + pi.PropertyType.Name + " (property)");
                }
                foreach (MethodInfo mi in methods)
                {
                    if (!mi.IsStatic || mi.ReturnType == typeof(void) || mi.IsSpecialName) continue;
                    if (!Matches(mi.Name.ToLowerInvariant())) continue;
                    ParameterInfo[] ps = mi.GetParameters();
                    bool simple = true;
                    var argNames = new List<string>();
                    foreach (ParameterInfo p in ps)
                    {
                        Type pt = p.ParameterType;
                        if (!(pt.IsPrimitive || pt.IsEnum || pt == typeof(string))) { simple = false; break; }
                        argNames.Add(pt.Name);
                    }
                    if (!simple) continue;
                    L(prefixStatic + mi.Name + "(" + string.Join(",", argNames.ToArray()) + ")    : " + mi.ReturnType.Name + " (method)");
                }
            }
        }
    }
}
