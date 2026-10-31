using System;
using System.Reflection;
using BepInEx.Logging;

namespace NeonHud
{
    internal sealed class LlbRelease
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const long DefaultFrame = (1L << 32) / 60;

        private readonly ManualLogSource _log;
        private bool _bound, _broken;
        private float _nextBind;
        private float _retry = 2f;
        private FieldInfo _entity, _reduced, _data, _timer, _duration, _raw, _delta;

        public LlbRelease(ManualLogSource log) { _log = log; }

        public bool TryRead(float now, out bool active, out float progress, out int framesLeft, out bool reduced)
        {
            active = false; progress = 0f; framesLeft = -1; reduced = false;
            if (!Bind(now)) return false;
            try
            {
                var ent = _entity.GetValue(null) as UnityEngine.Object;
                if (ent == null) return true;

                object data = _data.GetValue(ent);
                if (data == null) return true;
                long timer = Raw(_timer.GetValue(data));
                long dur = Raw(_duration.GetValue(data));
                if (dur <= 0 || timer >= dur) return true;

                active = true;
                progress = timer <= 0 ? 0f : (float)((double)timer / dur);
                long frame = _delta != null ? Raw(_delta.GetValue(null)) : DefaultFrame;
                if (frame <= 0) frame = DefaultFrame;
                framesLeft = (int)((dur - timer + frame - 1) / frame);
                reduced = _reduced != null && (bool)_reduced.GetValue(null);
                return true;
            }
            catch (Exception ex)
            {
                _broken = true;
                _log.LogWarning("Release meter: reading the game's hitstun failed, meter disabled until config reload: " + ex.Message);
                return false;
            }
        }

        private long Raw(object fixedPoint) { return (long)_raw.GetValue(fixedPoint); }

        private bool Bind(float now)
        {
            if (_bound) return true;
            if (_broken || now < _nextBind) return false;

            Type hud = ReflectionPath.FindType("LLScreen.ScreenGameHud");
            if (hud == null)
            {
                _nextBind = now + _retry;
                _retry = Math.Min(30f, _retry * 2f);
                return false;
            }

            _entity = hud.GetField("hitStunEntity", Static);
            _reduced = hud.GetField("hitStunReduced", Static);
            _data = _entity != null ? FindField(_entity.FieldType, "hitableData", Inst) : null;
            _timer = _data != null ? FindField(_data.FieldType, "hitstunTimer", Inst) : null;
            _duration = _data != null ? FindField(_data.FieldType, "hitstunDuration", Inst) : null;
            _raw = _timer != null ? RawField(_timer.FieldType) : null;

            Type world = ReflectionPath.FindType("World");
            _delta = world != null ? world.GetField("FDELTA_TIME", Static) : null;
            if (_delta != null && _timer != null && _delta.FieldType != _timer.FieldType) _delta = null;

            string missing = _entity == null ? "ScreenGameHud.hitStunEntity"
                           : _data == null ? "HitableEntity.hitableData"
                           : _timer == null ? "HitableData.hitstunTimer"
                           : _duration == null ? "HitableData.hitstunDuration"
                           : _raw == null ? "fixed-point raw value"
                           : null;
            if (missing != null)
            {
                _broken = true;
                _log.LogWarning("Release meter: couldn't find " + missing + " in this game version; meter disabled.");
                return false;
            }

            _bound = true;
            _log.LogInfo("Release meter linked to the game's ball hitstun" + (_delta == null ? " (assuming 60 fps frames)" : "") + ".");
            return true;
        }

        private static FieldInfo FindField(Type t, string name, BindingFlags flags)
        {
            for (; t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        private static FieldInfo RawField(Type fixedType)
        {
            if (fixedType == typeof(long)) return null;
            FieldInfo found = null;
            foreach (FieldInfo f in fixedType.GetFields(Inst))
            {
                if (f.FieldType != typeof(long)) continue;
                if (found != null) return null;
                found = f;
            }
            return found;
        }
    }
}
