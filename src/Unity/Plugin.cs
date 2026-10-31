using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using NeonHud.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonHud
{
    [BepInPlugin(Guid, PluginName, Version)]
    public sealed class NeonHudPlugin : BaseUnityPlugin
    {
        public const string Guid = "NeonHUD";
        public const string PluginName = "NeonHUD";
        public const string Version = "1.6.0";

        internal static ManualLogSource Log;

        private HudConfig _cfg;
        private readonly HudTheme _theme = new HudTheme();
        private readonly HudState _state = new HudState();
        private readonly HudPainter _painter = new HudPainter();
        private readonly DemoFeed _demo = new DemoFeed();
        private readonly VanillaHider _hider = new VanillaHider();
        private readonly SongText _songText = new SongText();
        private MatchDetector _match;
        private LlbRelease _llbRelease;
        private LlbPlayers _llbPlayers;
        private readonly PlayerCardsPainter _cardsPainter = new PlayerCardsPainter();
        private float _nextPlayerRead;
        private bool _releaseFeed, _releaseLlb;
        private ImguiCanvas _canvas;
        private SpectrumAnalyzer _spectrum;
        private Source _speedSrc, _timerSrc, _songSrc;

        private bool _ready, _visible = true, _demoOn, _sceneAllowed = true, _fontPending, _inMatch, _wasInMatch, _paused;
        private readonly HashSet<string> _pauseScreens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<ConfigEntryBase> _linkSettings = new HashSet<ConfigEntryBase>();
        private string _scene;
        private float _nextScenePoll, _nextClock, _fpsTime;
        private int _fpsFrames;
        private readonly HashSet<string> _showScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _hideScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo(PluginName + " " + Version + " starting...");
            try
            {
                _cfg = new HudConfig(Config);
                _canvas = new ImguiCanvas();
                _spectrum = new SpectrumAnalyzer();
                _match = new MatchDetector(Log);
                _llbRelease = new LlbRelease(Log);
                useGUILayout = false;
                ApplyConfig();
                foreach (ConfigEntryBase e in new ConfigEntryBase[] {
                    _cfg.SpeedSource, _cfg.TimerSource, _cfg.SongSource, _cfg.ReleaseSource, _cfg.HideVanillaHud, _cfg.VanillaHudElements,
                    _cfg.VanillaPlayerElements, _cfg.HideVanillaObjects, _cfg.PlayerCards, _cfg.OnlyInMatch, _cfg.MatchScreenType,
                    _cfg.ShowInScenes, _cfg.HideInScenes, _cfg.FontName, _cfg.PauseScreens })
                    _linkSettings.Add(e);
                Config.SettingChanged += (s, e) =>
                {
                    if (e != null && e.ChangedSetting != null && !_linkSettings.Contains(e.ChangedSetting)) _cfg.ApplyTo(_theme);
                    else ApplyConfig();
                };
                _ready = true;
                Log.LogInfo(PluginName + " " + Version + " loaded. F7 toggle, F8 game HUD, F10 demo, F9 dump, F6 reload config.");
            }
            catch (Exception ex)
            {
                Log.LogError(PluginName + " failed to start: " + ex);
                enabled = false;
            }
        }

        private void ApplyConfig()
        {
            _cfg.ApplyTo(_theme);
            _speedSrc = SourceFactory.Create(_cfg.SpeedSource.Value, ValueKind.Speed, Log);
            _timerSrc = SourceFactory.Create(_cfg.TimerSource.Value, ValueKind.Timer, Log);
            _songSrc = SourceFactory.Create(_cfg.SongSource.Value, ValueKind.Song, Log);
            _releaseFeed = _releaseLlb = false;
            foreach (string part in (_cfg.ReleaseSource.Value ?? "").Split('|'))
            {
                string p = part.Trim().ToLowerInvariant();
                if (p == "feed") _releaseFeed = true;
                else if (p == "llb") _releaseLlb = true;
            }
            _llbRelease = new LlbRelease(Log);
            _llbPlayers = new LlbPlayers(Log);

            var hide = new List<string>();
            if (_cfg.HideVanillaHud.Value)
            {
                hide.AddRange(VanillaHider.Split(_cfg.VanillaHudElements.Value));
                if (_cfg.PlayerCards.Value) hide.AddRange(VanillaHider.Split(_cfg.VanillaPlayerElements.Value));
                hide.AddRange(VanillaHider.Split(_cfg.HideVanillaObjects.Value));
            }
            _hider.Configure(hide, Log);

            _match.Configure(_cfg.OnlyInMatch.Value ? _cfg.MatchScreenType.Value : "");
            FillSet(_showScenes, _cfg.ShowInScenes.Value);
            FillSet(_hideScenes, _cfg.HideInScenes.Value);
            _sceneAllowed = SceneAllowed(_scene);
            FillSet(_pauseScreens, _cfg.PauseScreens.Value);
            _canvas.Font = null;
            _fontPending = !string.IsNullOrEmpty(_cfg.FontName.Value);
        }

        private void WriteDump()
        {
            try { Log.LogInfo("Dump written to " + DevDump.Write()); }
            catch (Exception ex) { Log.LogError("Dump failed: " + ex); }
        }

        private static void FillSet(HashSet<string> set, string csv)
        {
            set.Clear();
            if (string.IsNullOrEmpty(csv)) return;
            foreach (string s in csv.Split(','))
                if (s.Trim().Length > 0) set.Add(s.Trim());
        }

        private bool SceneAllowed(string scene)
        {
            if (scene == null) return true;
            if (_hideScenes.Contains(scene)) return false;
            return _showScenes.Count == 0 || _showScenes.Contains(scene);
        }

        private void Update()
        {
            if (!_ready) return;
            float now = Time.unscaledTime;

            if (_cfg.ToggleKey.Value.IsDown()) _visible = !_visible;
            if (_cfg.VanillaKey.Value.IsDown())
            {
                _cfg.HideVanillaHud.Value = !_cfg.HideVanillaHud.Value;
                Log.LogInfo("Vanilla HUD " + (_cfg.HideVanillaHud.Value ? "hidden" : "shown"));
            }
            if (_cfg.DemoKey.Value.IsDown())
            {
                _demoOn = !_demoOn;
                _state.ResetMatch();
                Log.LogInfo("Demo mode " + (_demoOn ? "on" : "off"));
            }
            if (_cfg.ReloadKey.Value.IsDown())
            {
                Config.Reload();
                ApplyConfig();
                Log.LogInfo("Config reloaded");
            }
            if (_cfg.DumpKey.Value.IsDown()) WriteDump();

            PollScene(now);
            _inMatch = _match.Poll(now);
            _paused = _cfg.HideWhenPaused.Value && Active && PauseScreenOpen();
            if (_inMatch && !_wasInMatch) _state.ResetMatch();
            _wasInMatch = _inMatch;
            _state.Time = now;

            if (!Showing) return;

            if (_demoOn) _demo.Step(_state, now);
            else UpdateData(now);

            UpdateTelemetry(now);
        }

        private void UpdateData(float now)
        {
            object v;
            float f;

            if (_speedSrc.TryGet(out v) && ValueConv.ToFloat(v, out f)) _state.SetSpeed(f * _cfg.SpeedMultiplier.Value, now);
            else _state.ClearSpeed();

            if (_timerSrc.TryGet(out v) && ValueConv.ToSeconds(v, out f))
            {
                bool countdown = _theme.TimerCountsDown && _timerSrc.Label != "ELAPSED";
                _state.SetTimer(f * _cfg.TimerMultiplier.Value, countdown, _timerSrc.Label);
            }
            else _state.ClearTimer();

            string song = null;
            if (_songSrc.TryGet(out v) && v != null) song = _songText.Clean(v as string ?? v.ToString());
            _state.SetSong(song, now);
            _state.SourceLabel = SourceLabel(_songSrc.Label);

            UpdateRelease(now);
            UpdatePlayers(now);

            if (_theme.Spectrum) _spectrum.Update(_state, Time.unscaledDeltaTime, _cfg.SpectrumGainDb.Value);
            else SpectrumAnalyzer.Clear(_state);
        }

        private void UpdatePlayers(float now)
        {
            PlayerCard[] cards = _state.Players;
            if (!_theme.PlayerCards) { for (int i = 0; i < cards.Length; i++) cards[i].Present = false; return; }
            if (now < _nextPlayerRead) return;
            _nextPlayerRead = now + 1f / 30f;
            _llbPlayers.Read(cards, now);
        }

        private void UpdateRelease(float now)
        {
            if (!_theme.ShowRelease) { _state.ClearRelease(); return; }
            bool active, reduced;
            float progress;
            int frames;
            if (_releaseFeed && NeonHudFeed.TryGetRelease(out active, out progress, out frames, out reduced))
                _state.SetRelease(active, progress, frames, reduced, now);
            else if (_releaseLlb && _llbRelease.TryRead(now, out active, out progress, out frames, out reduced))
                _state.SetRelease(active, progress, frames, reduced, now);
            else _state.ClearRelease();
        }

        private static string SourceLabel(string label)
        {
            switch (label)
            {
                case "AUDIO": return "SRC:AUDIO";
                case "SCENE": return "SRC:SCENE";
                case "FEED": return "SRC:FEED";
                case "NO LINK": return "SRC:NONE";
                default: return "SRC:GAME";
            }
        }

        private void UpdateTelemetry(float now)
        {
            _fpsFrames++;
            if (now - _fpsTime >= 0.5f)
            {
                _state.FpsLabel = "FPS " + (int)Math.Round(_fpsFrames / (now - _fpsTime));
                _fpsFrames = 0;
                _fpsTime = now;
            }
            if (now >= _nextClock)
            {
                _nextClock = now + 1f;
                _state.ClockLabel = "LOCAL " + DateTime.Now.ToString("HH:mm");
            }
        }

        private void PollScene(float now)
        {
            if (now < _nextScenePoll) return;
            _nextScenePoll = now + 0.5f;

            string name = SceneManager.GetActiveScene().name;
            if (name != _scene)
            {
                _scene = name;
                _state.ResetMatch();
                string pretty = Pretty.Name(name) ?? "";
                if (pretty.Length > 18) pretty = pretty.Substring(0, 18);
                _state.SceneLabel = "SCN " + pretty;
                _sceneAllowed = SceneAllowed(name);
                if (!string.IsNullOrEmpty(_cfg.FontName.Value) && _canvas.Font == null) _fontPending = true;
            }

            if (_fontPending)
            {
                _fontPending = false;
                _canvas.Font = FindFont(_cfg.FontName.Value);
                if (_canvas.Font == null) Log.LogWarning("Font '" + _cfg.FontName.Value + "' not found; using Unity default (will retry next scene).");
            }
        }

        private static Font FindFont(string name)
        {
            foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(typeof(Font)))
                if (string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase)) return (Font)o;

            MethodInfo os = typeof(Font).GetMethod("CreateDynamicFontFromOSFont", new[] { typeof(string), typeof(int) });
            if (os == null) return null;
            try
            {
                var f = os.Invoke(null, new object[] { name, 32 }) as Font;
                if (f != null) f.hideFlags = HideFlags.HideAndDontSave;
                return f;
            }
            catch { return null; }
        }

        private bool Active
        {
            get { return _ready && _cfg.Enabled.Value && _visible && _sceneAllowed && (_demoOn || _inMatch); }
        }

        private bool Showing
        {
            get { return Active && !_paused; }
        }

        private bool PauseScreenOpen()
        {
            if (_pauseScreens.Count == 0) return false;
            List<string> open = GameScreens.Open();
            if (open == null) return false;
            for (int i = 0; i < open.Count; i++)
                if (_pauseScreens.Contains(open[i])) return true;
            return false;
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            _hider.Apply(Active && !_demoOn);
        }

        private void OnGUI()
        {
            if (!Showing) return;
            Event e = Event.current;
            if (e == null || e.type != EventType.Repaint) return;

            float bw, bh;
            HudPainter.Size(_theme, out bw, out bh);
            float k = Screen.height / 1080f * _cfg.Scale.Value;
            float maxW = Screen.width * 0.98f;
            if (bw * k > maxW) k = maxW / bw;
            float w = bw * k, h = bh * k;
            float x = (Screen.width - w) * 0.5f + _cfg.OffsetX.Value * k;
            float y = _cfg.Anchor.Value == HudAnchor.Bottom
                ? Screen.height - h - _cfg.OffsetY.Value * k
                : _cfg.OffsetY.Value * k;

            GUI.depth = -1000;
            _canvas.Begin();
            try
            {
                _painter.Paint(_canvas, _state, _theme, x, y, k);
                if (_theme.PlayerCards)
                    _cardsPainter.Paint(_canvas, _state.Players, _theme, Screen.width, _state.Time,
                                        Screen.height / 1080f * _cfg.PlayerCardsScale.Value, _cfg.PlayerCardsOffsetY.Value);
            }
            catch (Exception ex)
            {
                _visible = false;
                Log.LogError("HUD paint failed, hiding HUD (press toggle key to retry): " + ex);
            }
            finally { _canvas.End(); }
        }

        private void OnDestroy()
        {
            _hider.Restore();
            if (_canvas != null) _canvas.Release();
        }
    }
}
