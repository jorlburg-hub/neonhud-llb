# NeonHUD

A cyberpunk HUD for Lethal League Blaze. It replaces the game's bottom speed/timer/track cluster and player panels with neon seven-segment readouts, a frame-exact release meter and player cards. Everything is read from the same values the game's own HUD shows, so it works out of the box.

It's purely visual and client-side: it doesn't change gameplay, and other players in a lobby see nothing.

## What's on it

- **Ball speed:** big seven-segment digits with a chromatic split. The panel shifts colour by speed tier and glitches on every hit. It also shows peak speed, the change per hit, and a speed bar.
- **Release meter:** the ball's hitstun, read from the same timer the game's own bars use. It counts the exact frames until the ball is released and pulses in the release window. It flashes **RELEASE!** on the frame the ball leaves, and turns purple for the game's reduced hitstun.
- **Player cards:**
  - team colour, character, name, and stocks or points
  - a segmented HP bar with a **threat overlay**: the HP the ball would take if it hit you right now
  - a **LETHAL** blink when the next hit would KO you, and a ghost chunk for damage just taken
  - the special (SP) bar
- **Timer** in seven-segment digits. It blinks under 30 s on countdown timers.
- **Now playing:** the current track, with a live mirrored audio spectrum and a signal meter.
- **Decoration:** scanlines, a scrolling hex log, corner brackets and katakana accents. Every effect can be switched off on its own.

Two layouts are included: **Compact** (the default, 640 × 136 px at 1080p) and **Full** (1240 × 282 px, adds telemetry and a data log). There are also two themes: **Mono** (the default: black and white with purple) and **Neon** (cyan, magenta and yellow). A **Custom** theme takes your own colours.

## Keys

| Key | Action |
|-----|--------|
| F7  | Show / hide the HUD |
| F8  | Show / hide the game's own HUD (remembered; hidden by default) |
| F10 | Demo mode: fake data anywhere, to preview every effect |
| F6  | Reload the config without restarting |
| F9  | Write `BepInEx/NeonHUD_dump.txt`, a list of linkable game values (for tinkering) |

All keys can be rebound.

## Settings

Use the mod manager's **Config editor** (pick `NeonHUD`), or edit `BepInEx/config/NeonHUD.cfg` in your profile and press F6 in game to apply it.

| Setting | What it does |
|---|---|
| `[General] Layout` | `Compact` or `Full` |
| `[General] Scale`, `[PlayerCards] Scale` | Size of the bottom HUD and of the player cards. Try 0.8 for smaller |
| `[General] OffsetY`, `[PlayerCards] OffsetY` | Distance from the screen edges |
| `[General] HideVanillaHud` | Hide the game's own HUD while NeonHUD shows (on by default). The same as F8 |
| `[General] HideWhenPaused` | Hide NeonHUD while the pause menu, options or a dialog is open (on by default) |
| `[PlayerCards] Enabled`, `Background` | The cards, and the dark band behind them |
| `[Theme] Preset` | `Mono`, `Neon` or `Custom`. Custom uses the hex colours in `[Theme]` and `[PlayerCards] Team*` |
| `[Effects] ...` | `Scanlines`, `ChromaticAberration`, `Glitch`, `Spectrum`, `DataLog`, `ReactiveColours`, `JapaneseAccents`, `ReleaseMeter` and `ReleaseWindow` (how far through hitstun the meter turns yellow; 0.8 is the last 20%) |
| `[Data] SpeedTier*` | The speeds where the panel changes colour |

## Hiding the game's HUD

While NeonHUD shows, the game's bottom cluster (the frame, speed counter, timer, track label and hitstun bars) and its player panels, which NeonHUD's cards replace, are hidden. They're scaled to zero rather than switched off, so the game keeps updating the values NeonHUD reads. Press F8 to bring them back (remembered), or F7 to hide NeonHUD and get them back for now.

## The pause menu

NeonHUD is drawn with Unity's immediate-mode GUI, which always renders on top of the game's own menus; it can't be layered underneath them. So instead of covering the pause menu, NeonHUD hides while the pause menu, options or a dialog is open, and comes back when you resume. The game's own HUD stays hidden meanwhile. `HideWhenPaused` turns this off; `PauseScreens` lists which of the game's screens count.

## For mod developers

Other plugins can feed NeonHUD their own values without referencing it. The speed, timer, song and release sources check the feed first. The feed is a static class `NeonHud.NeonHudFeed` with these methods:
- `SetBallSpeed(float)`
- `SetTimer(float seconds)`
- `SetSong(string)`
- `SetRelease(progress, framesLeft, reduced)` and `SetNoRelease()`

Find the type through reflection and call it; it does nothing if NeonHUD isn't installed. Each source can also be re-pointed in `[Data]` at any game member, using paths from the F9 dump.

## Known issues

- **Japanese labels show as boxes.** The katakana labels need a font on your PC with those glyphs. If they show as boxes, set `JapaneseAccents = false`.
- **The spectrum stays flat.** If the bars don't move while music plays, turn `Spectrum` off.
- **Frame-time spikes.** Turn `Spectrum` off first: it's about half of what the HUD draws.
