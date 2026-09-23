# Changelog

## v1.3.0 (2026-09-23)

- New: **rejoin after a crash**. If Roblox closes unexpectedly while you're in a game, NeuzStrap offers to put you
  straight back into the same server. The offer also waits on the Home page for a few hours in case you dismissed it.
  A normal quit never asks.
- New: **automatic clean-up**. Every month (or weekly / every 3 months, your choice) NeuzStrap clears Roblox's logs,
  temp files, old versions, download caches and - if you want - the big asset cache. It runs *after* you finish
  playing, so it never slows a launch down, and it never touches the official launcher's own copy of Roblox.

## v1.2.0 (2026-09-23)

- New **On-screen** page.
  - **FPS counter**: switches on Roblox's own FPS / ping / memory panel (the Shift+F5 one). NeuzStrap can't measure
    Roblox's frame rate itself without injecting into the game, so it uses Roblox's counter instead.
  - **Overlay** on top of the game with a **CPS counter**, a **KPS counter** and a **key display** for the keys you
    choose (any letters, numbers, F-keys, Space, Shift, Ctrl, Alt, arrows or mouse buttons - up to 10).
    Keys are drawn **as a mini keyboard** (W above A S D, wide Space and Shift) or in a single row, in your accent
    color or as a **moving rainbow gradient**, in any of six positions and three sizes, with a live preview in the
    settings. It's click-through, only shows while Roblox is in front, and can't appear over exclusive fullscreen.
- New: **automatic updates**. NeuzStrap quietly installs new releases from GitHub in the background while Roblox
  starts; the new version is used from your next launch. Turn it off under Settings.
- New: Discord Rich Presence **"Join server" button**, so friends land in the exact server you're in
  (hidden automatically in private servers).

## v1.1.0 (2026-09-22)

- New: **Close Roblox**. While Roblox is open, the Play buttons turn into a Close Roblox button. It closes every Roblox
  window (force-closing any that hang) and removes the crash handler Roblox leaves behind. Roblox Studio isn't touched.
  Also available from the tray icon while you play.
- New: **Custom cursor** on the Mods page. Pick Classic, Sakura, Big arrow, Dot or Crosshair (drawn by NeuzStrap), or
  use your own image. Roblox's own cursor comes back when you switch to Default.
- New: **RGB cursor**. Roblox's own arrow, hand pointer and shift-lock circle, recolored as a rainbow.

## v1.0.1 (2026-09-22)

- Fixed: the **Choose font…** button on the Mods page didn't show up.
- Fixed: the FPS cap and other in-game settings weren't applied if Roblox was already open. NeuzStrap now closes
  the open Roblox first (starting a new one replaces it anyway), so your settings always stick.
- Fixed: the "new version available" banner could never appear.
- FPS cap choices now match Roblox's own menu (60, 120, 144, 240) and show your screen's refresh rate.
- New: the official "Roblox Player" shortcuts can open through NeuzStrap (installer option, or **Fix shortcuts** in the
  Home check-up), so your tweaks apply no matter which icon you click. Uninstalling puts them back.

## v1.0.0 (2026-09-22)

First release.

- Installs and updates Roblox from the official CDN: MD5 verification, parallel + resumable downloads, mirror fallback,
  disk-space check, offline fallback and automatic clean-up of old versions.
- One-click performance profiles (Roblox Default, Balanced, Potato, Ultra Potato) with a hardware scan that recommends one.
- Allowlist-only FastFlags, plus in-game graphics level, optimization mode and FPS cap applied before every launch.
- Game Booster: CPU priority, RAM clean-up, dual-GPU selection, calm background apps, power boost, fullscreen optimizations, Game Bar toggle.
- PC check-up for common lag causes.
- Server location notices, game history with rejoin, invite links, Discord Rich Presence (built-in app, no setup).
- Mods folder with automatic backup/restore, custom font, FastFlag editor with Bloxstrap import.
- Cleaner, Repair, dark UI with accent colors, installer/uninstaller that restores the official launcher.
