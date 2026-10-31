using System;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using NeonHud;
using NeonHud.Core;
using UnityEngine;

namespace LLScreen
{
    public class ScreenGameHud : ScreenBase { public Transform rtBoomBox; public object[] playerInfos = new object[0]; }
}

public static class Tests
{
    static int fails, passes;
    static void Check(bool ok, string what) { if (ok) passes++; else { fails++; Console.WriteLine("  FAIL: " + what); } }
    static bool LogHas(string s) { foreach (var l in ManualLogSource.Lines) if (l.Contains(s)) return true; return false; }
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static NeonHudPlugin plugin;

    static NeonHudPlugin Boot()
    {
        var p = new NeonHudPlugin();
        Call(p, "Awake");
        return p;
    }
    static void Call(object o, string m) { o.GetType().GetMethod(m, Any).Invoke(o, null); }
    static T Field<T>(object o, string f) { return (T)o.GetType().GetField(f, Any).GetValue(o); }
    static HudConfig Cfg { get { return Field<HudConfig>(plugin, "_cfg"); } }

    static void Frame(params KeyCode[] keys)
    {
        Time.frameCount++;
        Time.unscaledTime += 0.6f;
        Input.Down.Clear(); foreach (var k in keys) Input.Down.Add(k);
        Call(plugin, "Update");
        Call(plugin, "LateUpdate");
        Input.Down.Clear();
        Event.current = new Event { type = EventType.Repaint };
        GUI.Drawn.Clear();
        Call(plugin, "OnGUI");
    }

    static bool HudDrawn { get { return GUI.Drawn.Count > 0; } }

    public static int Main()
    {
        Console.WriteLine("NeonHUD");
        ConfigTests();
        HudTests();
        ConfigChangeTests();
        Check(!LogHas("[err]"), "no errors logged");
        Console.WriteLine(fails == 0 ? "ALL PASSED (" + passes + " checks)" : fails + " FAILED, " + passes + " passed");
        return fails == 0 ? 0 : 1;
    }

    static void ConfigTests()
    {
        plugin = Boot();
        Check(LogHas("NeonHUD " + NeonHudPlugin.Version + " loaded"), "loads");
        int entries = 0;
        foreach (var e in plugin.Config.Entries) entries++;
        Check(entries > 50, "all settings bound (" + entries + ")");
        Check(Cfg.HideVanillaHud.Value, "new install: the game's HUD is hidden by default");
        Check(Cfg.HideWhenPaused.Value && Cfg.PauseScreens.Value.Contains("GAME_PAUSE"), "hides while paused by default");

        ConfigFile.Saved["General.HideVanillaHud"] = false;
        ConfigFile.Saved["Internal.ConfigVersion"] = 3;
        plugin = Boot();
        Check(Cfg.HideVanillaHud.Value, "1.5 config: game HUD now hidden");
        ConfigFile.Saved["Internal.ConfigVersion"] = 4;
        plugin = Boot();
        Check(!Cfg.HideVanillaHud.Value, "1.6 config that shows the game HUD keeps it shown");
        ConfigFile.Saved.Clear();
    }

    static GameObject boomBox;
    static LLScreen.ScreenGameHud gameHud;

    static void HudTests()
    {
        plugin = Boot();
        Frame();
        Check(!HudDrawn, "menus: no HUD");

        boomBox = new GameObject("BoomBox");
        var hudGo = new GameObject("ScreenGameHud");
        gameHud = hudGo.AddComponent<LLScreen.ScreenGameHud>();
        gameHud.rtBoomBox = boomBox.transform;
        LLScreen.UIScreen.Show(0, ScreenType.GAME_HUD);
        Frame(); Frame();
        Check(HudDrawn, "match: HUD drawn");
        Check(boomBox.transform.localScale == Vector3.zero, "match: the game's HUD cluster hidden");

        LLScreen.UIScreen.Show(1, ScreenType.GAME_PAUSE);
        Frame();
        Check(!HudDrawn, "paused: HUD not drawn over the pause menu");
        Check(boomBox.transform.localScale == Vector3.zero, "paused: the game's HUD stays hidden");
        LLScreen.UIScreen.Show(1, ScreenType.OPTIONS);
        Frame();
        Check(!HudDrawn, "options opened from the pause menu: still hidden");
        LLScreen.UIScreen.Clear(1);
        Frame();
        Check(HudDrawn, "resumed: HUD back");

        Cfg.HideWhenPaused.Value = false;
        LLScreen.UIScreen.Show(1, ScreenType.GAME_PAUSE);
        Frame();
        Check(HudDrawn, "HideWhenPaused off: drawn while paused");
        Cfg.HideWhenPaused.Value = true;
        Cfg.PauseScreens.Value = "DIALOG";
        Frame();
        Check(HudDrawn, "PauseScreens decides what counts as paused");
        Cfg.PauseScreens.Value = HudConfig.DefaultPauseScreens;
        Frame();
        Check(!HudDrawn, "back to the default list");
        LLScreen.UIScreen.Clear(1);
        Frame();

        Frame(KeyCode.F7);
        Check(!HudDrawn && boomBox.transform.localScale != Vector3.zero, "F7: HUD off, game HUD restored");
        Frame(KeyCode.F7);
        Check(HudDrawn, "F7 again: HUD on");
        Frame(KeyCode.F8);
        Check(!Cfg.HideVanillaHud.Value && boomBox.transform.localScale != Vector3.zero, "F8: game HUD shown again");
        Frame(KeyCode.F8);
        Check(Cfg.HideVanillaHud.Value && boomBox.transform.localScale == Vector3.zero, "F8 again: hidden");
    }

    static void ConfigChangeTests()
    {
        Console.WriteLine("NeonHUD config changes");
        var c = Cfg;
        var theme = Field<HudTheme>(plugin, "_theme");
        object links = Field<object>(plugin, "_llbPlayers");
        c.Preset.Value = ThemePreset.Custom;
        c.Primary.Value = "#FF00FF";
        Check(Math.Abs(theme.Primary.R - 1f) < 1e-3 && Math.Abs(theme.Primary.G) < 1e-3 && Math.Abs(theme.Primary.B - 1f) < 1e-3, "colour change applies straight away");
        Check(ReferenceEquals(links, Field<object>(plugin, "_llbPlayers")), "a colour change doesn't re-link the game data");
        c.Background.Value = "#10203080";
        Check(Math.Abs(theme.Background.A - 128 / 255f) < 1e-3, "background with alpha applied");
        c.Layout.Value = HudLayout.Full;
        Check(!theme.Compact, "layout switched to Full");
        Frame();
        Check(GUI.Drawn.Count > 0, "full layout draws");
        c.HideVanillaHud.Value = false;
        Check(!ReferenceEquals(links, Field<object>(plugin, "_llbPlayers")), "a linking setting re-links");
    }
}
