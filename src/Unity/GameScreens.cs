using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NeonHud
{
    internal static class GameScreens
    {
        private static FieldInfo _current, _type;
        private static int _tries;
        private static float _next;
        private static readonly List<string> Names = new List<string>();
        private static int _namesFrame = -1;

        private static bool Ready()
        {
            if (_current != null) return true;
            if (_tries >= 15 || Time.unscaledTime < _next) return false;
            _tries++;
            _next = Time.unscaledTime + 2f;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = a.GetType("LLScreen.UIScreen", false); }
                catch (Exception) { continue; }
                if (t == null) continue;
                FieldInfo cur = t.GetField("currentScreens", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Type el = cur != null && cur.FieldType.IsArray ? cur.FieldType.GetElementType() : null;
                FieldInfo type = el != null ? el.GetField("screenType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;
                if (type == null) continue;
                _type = type;
                _current = cur;
                return true;
            }
            return false;
        }

        public static List<string> Open()
        {
            if (!Ready()) return null;
            if (_namesFrame == Time.frameCount) return Names;
            Names.Clear();
            try
            {
                var screens = _current.GetValue(null) as Array;
                if (screens != null)
                    foreach (object s in screens)
                    {
                        var o = s as UnityEngine.Object;
                        if (o == null) continue;
                        Names.Add(Convert.ToString(_type.GetValue(s)));
                    }
            }
            catch (Exception) { Names.Clear(); return null; }
            _namesFrame = Time.frameCount;
            return Names;
        }
    }
}
