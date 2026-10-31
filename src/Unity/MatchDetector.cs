using System;
using BepInEx.Logging;
using UnityEngine;

namespace NeonHud
{
    internal sealed class MatchDetector
    {
        private readonly ManualLogSource _log;
        private string _typeName;
        private Type _type;
        private bool _typeMissing;
        private UnityEngine.Object _obj;
        private float _next, _typeRetry = 2f;

        public MatchDetector(ManualLogSource log) { _log = log; }

        public void Configure(string typeName)
        {
            typeName = (typeName ?? "").Trim();
            if (typeName == _typeName) return;
            _typeName = typeName;
            _type = null; _obj = null; _typeMissing = false; _next = 0f; _typeRetry = 2f;
        }

        public bool Poll(float now)
        {
            if (string.IsNullOrEmpty(_typeName)) return true;
            if (_obj != null) return true;
            if (now < _next) return _typeMissing;
            _next = now + 0.5f;

            if (_type == null)
            {
                _type = ReflectionPath.FindType(_typeName);
                if (_type == null)
                {
                    if (!_typeMissing) _log.LogWarning("Match type '" + _typeName + "' not found; showing the HUD everywhere.");
                    _typeMissing = true;
                    _next = now + _typeRetry;
                    _typeRetry = Math.Min(30f, _typeRetry * 2f);
                    return true;
                }
                _typeMissing = false;
            }

            _obj = UnityEngine.Object.FindObjectOfType(_type);
            return _obj != null;
        }
    }
}
