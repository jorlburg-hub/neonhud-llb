using BepInEx.Configuration;
using NeonHud.Core;
using UnityEngine;

namespace NeonHud
{
    public enum HudAnchor { Bottom, Top }
    public enum HudLayout { Compact, Full }

    internal sealed class HudConfig
    {
        public const string LlbSpeed = "feed|find:LLScreen.ScreenGameHud::shownSpeedTarget";
        public const string LlbTimer = "feed|reflect:World::GetTimeLeftSecs()|elapsed";
        public const string LlbSong = "feed|find:LLScreen.ScreenGameHud::lbTrack.text|reflect:LLHandlers.AudioHandler::GetCurrentMusic()|audio|scene";
        public const string LlbVanillaHud = "find:LLScreen.ScreenGameHud::rtBoomBox";
        public const string LlbVanillaPlayers =
            "find:LLScreen.ScreenGameHud::playerInfos[0]; find:LLScreen.ScreenGameHud::playerInfos[1]; find:LLScreen.ScreenGameHud::playerInfos[2]; find:LLScreen.ScreenGameHud::playerInfos[3]";
        private const string V11VanillaHud =
            "find:LLScreen.ScreenGameHud::imtextSpeed; find:LLScreen.ScreenGameHud::imtextTime; find:LLScreen.ScreenGameHud::lbTrack";
        private const int CurrentConfigVersion = 4;
        public const string DefaultPauseScreens = "GAME_PAUSE, OPTIONS, MENU_OPTIONS, DIALOG, DIALOG_CONFIRMATION";

        public readonly ConfigEntry<bool> Enabled, OnlyInMatch, HideVanillaHud, HideWhenPaused;
        public readonly ConfigEntry<KeyboardShortcut> ToggleKey, DemoKey, DumpKey, ReloadKey, VanillaKey;
        public readonly ConfigEntry<HudAnchor> Anchor;
        public readonly ConfigEntry<HudLayout> Layout;
        public readonly ConfigEntry<float> Scale, OffsetX, OffsetY, Opacity;
        public readonly ConfigEntry<string> FontName, ShowInScenes, HideInScenes, MatchScreenType, VanillaHudElements, HideVanillaObjects, VanillaPlayerElements, PauseScreens;
        public readonly ConfigEntry<bool> PlayerCards, PlayerBand;
        public readonly ConfigEntry<float> PlayerCardsOffsetY, PlayerCardsScale;
        public readonly ConfigEntry<string> TeamRed, TeamBlue, TeamYellow, TeamGreen;

        public readonly ConfigEntry<ThemePreset> Preset;
        public readonly ConfigEntry<string> Background, Primary, Secondary, Accent, Warn, TextColour, Title, HitColour, GhostColour;
        public readonly ConfigEntry<bool> Scanlines, Chromatic, Glitch, Spectrum, DataLog, ReactiveColours, JapaneseAccents, ReleaseMeter;
        public readonly ConfigEntry<float> SpectrumGainDb, ReleaseWindow;
        public readonly ConfigEntry<string> ReducedColour;

        public readonly ConfigEntry<string> SpeedSource, TimerSource, SongSource, ReleaseSource;
        public readonly ConfigEntry<float> SpeedMultiplier, TimerMultiplier;
        public readonly ConfigEntry<float> Tier1, Tier2, Tier3, SpeedBarMax, TimerWarnSeconds;
        public readonly ConfigEntry<bool> TimerCountsDown;
        private readonly ConfigEntry<int> ConfigVersion;

        private const string SourceHelp =
            "Where the value comes from. Try several with '|', first that gives a usable value wins.\n" +
            "  feed                     - pushed by another plugin via NeonHud.NeonHudFeed\n" +
            "  reflect:Type::a.b[0].c   - static member path (fields, properties, Method(1) calls, [i] indexers)\n" +
            "  find:Type::a.b           - member path on Object.FindObjectOfType(Type)\n" +
            "  ui:Path/To/TextObject    - read the text of a vanilla HUD label\n" +
            "  track:Path/To/Object     - (speed) how fast that object moves\n" +
            "  elapsed                  - (timer) seconds since the scene loaded\n" +
            "  audio / scene            - (song) longest playing music clip / scene name\n" +
            "  static:TEXT | none\n" +
            "Press the dump key in a match to get copy-paste candidates in BepInEx/NeonHUD_dump.txt.";

        private static readonly KeyCode[] NoModifiers = new KeyCode[0];

        private static KeyboardShortcut Key(KeyCode key) { return new KeyboardShortcut(key, NoModifiers); }

        private static ConfigDescription Range(string description, float min, float max)
        {
            return new ConfigDescription(description, new AcceptableValueRange<float>(min, max));
        }

        public HudConfig(ConfigFile cfg)
        {
            Enabled = cfg.Bind("General", "Enabled", true, new ConfigDescription("Master switch."));
            OnlyInMatch = cfg.Bind("General", "OnlyInMatch", true, new ConfigDescription("Only show the HUD during a match (while MatchScreenType exists). Demo mode always shows."));
            MatchScreenType = cfg.Bind("General", "MatchScreenType", "LLScreen.ScreenGameHud", new ConfigDescription("Type whose presence means a match is running."));
            ToggleKey = cfg.Bind("General", "ToggleKey", Key(KeyCode.F7), new ConfigDescription("Show/hide the HUD."));
            VanillaKey = cfg.Bind("General", "VanillaHudKey", Key(KeyCode.F8), new ConfigDescription("Toggle hiding the game's own speed/timer/track display (saved to HideVanillaHud)."));
            DemoKey = cfg.Bind("General", "DemoKey", Key(KeyCode.F10), new ConfigDescription("Toggle demo data (preview the HUD without linking anything)."));
            DumpKey = cfg.Bind("General", "DumpKey", Key(KeyCode.F9), new ConfigDescription("Write BepInEx/NeonHUD_dump.txt with linkable objects and members."));
            ReloadKey = cfg.Bind("General", "ReloadKey", Key(KeyCode.F6), new ConfigDescription("Reload this config file from disk without restarting."));
            Anchor = cfg.Bind("General", "Anchor", HudAnchor.Bottom, new ConfigDescription("Screen edge the HUD sits on."));
            Layout = cfg.Bind("General", "Layout", HudLayout.Compact,
                new ConfigDescription("Compact = small HUD (release meter, song, speed, timer, slim spectrum) and slim player cards. Full = the big layout with telemetry and decoration."));
            Scale = cfg.Bind("General", "Scale", 1f, Range("Size of the bottom HUD (1 = designed for 1080p, scales with resolution). Try 0.8 for smaller.", 0.3f, 3f));
            OffsetX = cfg.Bind("General", "OffsetX", 0f, new ConfigDescription("Horizontal nudge in 1080p pixels."));
            OffsetY = cfg.Bind("General", "OffsetY", 12f, new ConfigDescription("Distance from the anchored edge in 1080p pixels."));
            Opacity = cfg.Bind("General", "Opacity", 1f, Range("Overall HUD opacity.", 0.1f, 1f));
            FontName = cfg.Bind("General", "FontName", "", new ConfigDescription("Font for HUD text: a font the game has loaded (see dump) or an installed OS font. Empty = Unity default."));
            ShowInScenes = cfg.Bind("General", "ShowInScenes", "", new ConfigDescription("Comma list of scene names to show the HUD in. Empty = all scenes."));
            HideInScenes = cfg.Bind("General", "HideInScenes", "", new ConfigDescription("Comma list of scene names to never show the HUD in (e.g. menus)."));
            HideVanillaHud = cfg.Bind("General", "HideVanillaHud", true, new ConfigDescription("Hide the game's own HUD while NeonHUD shows: the bottom cluster (speed, timer, track, hitstun bars), and the player panels while NeonHUD's player cards are on. F8 toggles."));
            HideWhenPaused = cfg.Bind("General", "HideWhenPaused", true, new ConfigDescription("Hide NeonHUD while the pause menu, options or a dialog is open. NeonHUD is drawn on top of the game's menus, so this keeps them readable."));
            PauseScreens = cfg.Bind("General", "PauseScreens", DefaultPauseScreens, new ConfigDescription("The game's screens (ScreenType names) that count as paused for HideWhenPaused."));
            VanillaHudElements = cfg.Bind("General", "VanillaHudElements", LlbVanillaHud, new ConfigDescription("What HideVanillaHud hides: ';'-separated member paths (find:/reflect:) or GameObject paths."));
            HideVanillaObjects = cfg.Bind("General", "HideVanillaObjects", "", new ConfigDescription("Extra GameObject paths (from the dump) hidden along with VanillaHudElements."));
            VanillaPlayerElements = cfg.Bind("General", "VanillaPlayerElements", LlbVanillaPlayers, new ConfigDescription("The game's player panels, hidden by F8 while NeonHUD's player cards are on."));

            PlayerCards = cfg.Bind("PlayerCards", "Enabled", true, new ConfigDescription("Neon player cards along the top: HP (with ball-threat overlay and LETHAL warning), energy, stocks/points."));
            PlayerBand = cfg.Bind("PlayerCards", "Background", true, new ConfigDescription("Dark background band behind the player cards."));
            PlayerCardsOffsetY = cfg.Bind("PlayerCards", "OffsetY", 0f, new ConfigDescription("Move the cards down from the top edge (1080p pixels)."));
            PlayerCardsScale = cfg.Bind("PlayerCards", "Scale", 1f, Range("Size of the player cards, separate from the bottom HUD's Scale.", 0.3f, 3f));
            TeamRed = cfg.Bind("PlayerCards", "TeamRed", "#FF3B4E", new ConfigDescription("Custom preset only. Team colours, in the game's team order."));
            TeamBlue = cfg.Bind("PlayerCards", "TeamBlue", "#2FA8FF", new ConfigDescription(""));
            TeamYellow = cfg.Bind("PlayerCards", "TeamYellow", "#FCEE0A", new ConfigDescription(""));
            TeamGreen = cfg.Bind("PlayerCards", "TeamGreen", "#39FF5A", new ConfigDescription(""));

            Preset = cfg.Bind("Theme", "Preset", ThemePreset.Mono,
                new ConfigDescription("Mono = black & white with purple highlights. Neon = cyan / magenta / yellow. Custom = your own colours below."));
            Background = cfg.Bind("Theme", "Background", "#07070DE0", new ConfigDescription("Custom preset only. Panel fill, #RRGGBB or #RRGGBBAA."));
            Primary = cfg.Bind("Theme", "Primary", "#00F0FF", new ConfigDescription("Main neon (cyan)."));
            Secondary = cfg.Bind("Theme", "Secondary", "#FF2A6D", new ConfigDescription("Second neon (magenta)."));
            Accent = cfg.Bind("Theme", "Accent", "#FCEE0A", new ConfigDescription("Label tabs / highlights (yellow)."));
            Warn = cfg.Bind("Theme", "Warn", "#FF003C", new ConfigDescription("Critical speed / timer warning (red)."));
            TextColour = cfg.Bind("Theme", "Text", "#E8FBFF", new ConfigDescription("Main text colour."));
            ReducedColour = cfg.Bind("Theme", "Reduced", "#B026FF", new ConfigDescription("Custom preset only. Release meter colour during the game's 'reduced' hitstun."));
            HitColour = cfg.Bind("Theme", "Hit", "#FCEE0A", new ConfigDescription("Custom preset only. Release window and SP READY ('act now')."));
            GhostColour = cfg.Bind("Theme", "DamageGhost", "#FFFFFF", new ConfigDescription("Custom preset only. Chunk of HP just lost on the player cards."));
            Title = cfg.Bind("Theme", "Title", "NEON//LEAGUE", new ConfigDescription("Logo text under the speed panel."));

            Scanlines = cfg.Bind("Effects", "Scanlines", true, new ConfigDescription("CRT scanlines over panels."));
            Chromatic = cfg.Bind("Effects", "ChromaticAberration", true, new ConfigDescription("Magenta/cyan split on digits and title."));
            Glitch = cfg.Bind("Effects", "Glitch", true, new ConfigDescription("Slice glitch on hits plus rare idle flicker."));
            Spectrum = cfg.Bind("Effects", "Spectrum", true, new ConfigDescription("Live audio spectrum bars."));
            DataLog = cfg.Bind("Effects", "DataLog", true, new ConfigDescription("Scrolling hex log behind the speed digits."));
            ReactiveColours = cfg.Bind("Effects", "ReactiveColours", true, new ConfigDescription("Speed panel changes colour with ball speed tier."));
            JapaneseAccents = cfg.Bind("Effects", "JapaneseAccents", true, new ConfigDescription("Small katakana/kanji labels. Turn off if they show as boxes."));
            SpectrumGainDb = cfg.Bind("Effects", "SpectrumGainDb", 0f, Range("Raise if the bars look too low, lower if they're pinned.", -30f, 30f));
            ReleaseMeter = cfg.Bind("Effects", "ReleaseMeter", true, new ConfigDescription("Show the release (ball hitstun) meter above the HUD."));
            ReleaseWindow = cfg.Bind("Effects", "ReleaseWindow", 0.8f, Range("How far through the hitstun the meter turns yellow ('hit now'). 0.8 = last 20%.", 0.3f, 0.99f));

            SpeedSource = cfg.Bind("Data", "SpeedSource", LlbSpeed, new ConfigDescription(SourceHelp));
            SpeedMultiplier = cfg.Bind("Data", "SpeedMultiplier", 1f, new ConfigDescription("Speed value is multiplied by this (calibrate track: or fixed-point values)."));
            TimerSource = cfg.Bind("Data", "TimerSource", LlbTimer, new ConfigDescription("Seconds (number) or \"m:ss\" text. See SpeedSource for the syntax."));
            TimerMultiplier = cfg.Bind("Data", "TimerMultiplier", 1f, new ConfigDescription("Timer value is multiplied by this (e.g. 0.016667 if the game stores frames)."));
            TimerCountsDown = cfg.Bind("Data", "TimerCountsDown", true, new ConfigDescription("The linked timer counts down (enables the low-time warning). The 'elapsed' fallback always counts up."));
            TimerWarnSeconds = cfg.Bind("Data", "TimerWarnSeconds", 30f, new ConfigDescription("Blink red below this many seconds (countdown timers only)."));
            SongSource = cfg.Bind("Data", "SongSource", LlbSong, new ConfigDescription("Now-playing text. See SpeedSource for the syntax."));
            ReleaseSource = cfg.Bind("Data", "ReleaseSource", "feed|llb",
                new ConfigDescription("Release meter data. 'llb' = the ball's hitstun timer exactly as LLB's own HUD reads it; 'feed' = NeonHudFeed.SetRelease from another plugin; 'none' = off. '|' tries in order."));
            Tier1 = cfg.Bind("Data", "SpeedTierHeating", 40f, new ConfigDescription("Speed where the panel turns yellow."));
            Tier2 = cfg.Bind("Data", "SpeedTierOverdrive", 100f, new ConfigDescription("Speed where it turns magenta."));
            Tier3 = cfg.Bind("Data", "SpeedTierCritical", 200f, new ConfigDescription("Speed where it turns red with hazard stripes."));
            SpeedBarMax = cfg.Bind("Data", "SpeedBarMax", 600f, new ConfigDescription("Speed that fills the segment bar (log scale)."));

            ConfigVersion = cfg.Bind("Internal", "ConfigVersion", 0, new ConfigDescription("Used to upgrade old settings. Don't edit."));
            Migrate();
        }

        private void Migrate()
        {
            if (ConfigVersion.Value >= CurrentConfigVersion) return;
            if (ConfigVersion.Value < 2)
            {
                if (SpeedSource.Value == "feed") SpeedSource.Value = LlbSpeed;
                if (TimerSource.Value == "feed|elapsed") TimerSource.Value = LlbTimer;
                if (SongSource.Value == "feed|audio|scene") SongSource.Value = LlbSong;
                if (!TimerCountsDown.Value) TimerCountsDown.Value = true;
            }
            if (ConfigVersion.Value < 4 && !HideVanillaHud.Value)
            {
                HideVanillaHud.Value = true;
            }
            if (ConfigVersion.Value < 3)
            {
                if (VanillaHudElements.Value == V11VanillaHud) VanillaHudElements.Value = LlbVanillaHud;
                string extra = (HideVanillaObjects.Value ?? "").Trim();
                if (extra == "find:LLScreen.ScreenGameHud" || extra == LlbVanillaHud) HideVanillaObjects.Value = "";
            }
            ConfigVersion.Value = CurrentConfigVersion;
        }

        public void ApplyTo(HudTheme t)
        {
            if (Preset.Value == ThemePreset.Custom)
            {
                t.Background = Col.FromHex(Background.Value, t.Background);
                t.Primary = Col.FromHex(Primary.Value, t.Primary);
                t.Secondary = Col.FromHex(Secondary.Value, t.Secondary);
                t.Accent = Col.FromHex(Accent.Value, t.Accent);
                t.Warn = Col.FromHex(Warn.Value, t.Warn);
                t.Text = Col.FromHex(TextColour.Value, t.Text);
                t.Hit = Col.FromHex(HitColour.Value, t.Hit);
                t.Ghost = Col.FromHex(GhostColour.Value, t.Ghost);
                t.Reduced = Col.FromHex(ReducedColour.Value, t.Reduced);
                t.TeamColours[0] = Col.FromHex(TeamRed.Value, t.TeamColours[0]);
                t.TeamColours[1] = Col.FromHex(TeamBlue.Value, t.TeamColours[1]);
                t.TeamColours[2] = Col.FromHex(TeamYellow.Value, t.TeamColours[2]);
                t.TeamColours[3] = Col.FromHex(TeamGreen.Value, t.TeamColours[3]);
            }
            else Palettes.Apply(t, Preset.Value);
            t.Title = Title.Value ?? "";
            t.Opacity = Opacity.Value;
            t.Compact = Layout.Value == HudLayout.Compact;

            t.Scanlines = Scanlines.Value;
            t.Chromatic = Chromatic.Value;
            t.Glitch = Glitch.Value;
            t.Spectrum = Spectrum.Value;
            t.DataLog = DataLog.Value;
            t.ReactiveColours = ReactiveColours.Value;
            t.JapaneseAccents = JapaneseAccents.Value;
            t.ShowRelease = ReleaseMeter.Value;
            t.ReleaseWindow = ReleaseWindow.Value;
            t.PlayerCards = PlayerCards.Value;
            t.PlayerBand = PlayerBand.Value;

            t.Tier1 = Tier1.Value;
            t.Tier2 = Tier2.Value;
            t.Tier3 = Tier3.Value;
            t.SpeedBarMax = SpeedBarMax.Value;
            t.TimerCountsDown = TimerCountsDown.Value;
            t.TimerWarnSeconds = TimerWarnSeconds.Value;
        }
    }
}
