# Selene's Hollow

> *She woke up by a fountain she had never seen, in a place she almost remembered.*

A small top-down 2D exploration RPG written in C# on top of [MonoGame](https://www.monogame.net/).
You play as **Selene** — wandering through ancient ruins, an overgrown garden, a quiet
forest, and a moonlit clearing, trying to piece together where you came from and why.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![MonoGame](https://img.shields.io/badge/MonoGame-3.8.4-E73C00)
![Language](https://img.shields.io/badge/C%23-12-239120)
![Platform](https://img.shields.io/badge/Platform-DesktopGL-2D2D2D)
![Status](https://img.shields.io/badge/status-prototype-blue)

---

## The Vibe

Wake up. Stand. Walk into the world.

The intro takes its time: a black hold, then Selene fades in **asleep on the
fountain pedestal**. A typewriter line — *"Where am I?"* — drops into a letterbox
panel. Press space, the world dips back to black, and when it returns Selene is
on her feet on the path below the fountain. Now it's yours to explore.

## Features

- **Cinematic intro** — black hold, scene fade-in, letterbox bars, typewriter
  dialog, post-dismiss fade-to-black, camera handoff, fade-in to gameplay.
- **Hand-built 30 x 20 world** with five biomes:
  - the **fountain courtyard** flanked by pillars
  - the **temple ruin** at the top of the central axis
  - a soft **western garden** of bushes, tiny trees, and bush columns
  - the **eastern ruins** of half-fallen pillars and broken walls
  - the **southern grove** that frames a chalice clearing
- **Selene** — full state machine over five sprite sheets (idle bob, run-up,
  run-down, run-left/right, jump-once) with a procedural drop shadow under
  her feet and a 5-frame breathing idle stitched out of two source rows.
- **Continuous WASD movement** in pixel space (260 px/s, normalized
  diagonals). No grid-snap teleporting.
- **Per-prop collision** with axis-split sliding — bump a tree, slide along it.
- **Smooth camera** follow with exponential ease + post-intro snap handoff so
  the gameplay fade-in starts already centred on the player.
- **Self-talk dialog system** — `AmbientDialog` panel with typewriter, fade
  envelope, and a multi-line `ShowSequence` queue.
- **Cell-based dialog triggers** — sixteen one-shot lines scattered across
  the map so Selene narrates her own discoveries.
- **Seven collectible memory orbs** with procedural radial-glow textures,
  per-orb pulse + bob, and a HUD counter.
- **Chalice interaction** — proximity prompt `[E]` with a multi-line monologue.
- **Idle musings** — random Selene thoughts after 22 s of standing still.
- **F5 restart** — wipes player, intro, triggers, orbs, and dialog state and
  replays the opening from black.

## Controls

| Key      | Action                                |
|----------|---------------------------------------|
| `W A S D` | Run                                  |
| `Space`  | Jump (in-place) / dismiss intro line |
| `E`      | Interact (when prompt is shown)      |
| `F5`     | Restart from the intro               |
| `Esc`    | Quit                                 |

## Build & Run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download). MonoGame
content tooling is restored automatically as a `dotnet tool`.

```bash
dotnet tool restore
dotnet build
dotnet run
```

The first build compiles all of `Content/Content.mgcb` (terrain, props,
character sheets, font) into `.xnb` assets and then links the C# project.

## Project Layout

```
SelenesHollow/
  Game1.cs                MonoGame entry — split into world + UI draw passes
  Globals.cs              Static singletons: SpriteBatch, Camera, Pixel, time
  Program.cs              Bootstraps Game1
  _Managers/
    GameManager.cs        Owns the world, runs intro/play state machine
  _Models/
    Map.cs                Tile grid + decoration list + collision AABBs
    Tile.cs               Source-rect quad with per-instance origin
    Player.cs             Animation state + continuous movement + collision
    Camera.cs             Smooth-follow camera with map clamping
    OrbManager.cs         Procedural-glow collectible orbs
  _Sequences/
    IntroSequence.cs      Scripted opening: hold → fade → dialog → handoff
    AmbientDialog.cs      Typewriter dialog with sequencing
    DialogTriggers.cs     Cell-coordinate one-shot self-talk lines
    ChaliceInteraction.cs Proximity prompt + multi-line monologue
    HudOverlay.cs         Top-right orb counter
  Content/
    Content.mgcb          MonoGame Content Pipeline manifest
    *.png / .spritefont   Tilesets, character sheets, props, dialog font
```

## Architecture Notes

The game runs two SpriteBatch passes per frame:

1. **World pass** — `samplerState: PointClamp`, `transformMatrix: Camera.Transform`.
   Map tiles, decorations, the player, orbs, and the chalice prompt all draw here.
   Painter's algorithm sorts decorations by row index, with the player slotted
   in by foot-Y so she walks behind/in front of objects naturally.
2. **UI pass** — screen-space, untransformed. Intro overlays (black fade,
   letterbox bars, dialog panel, hint), the orb HUD, and ambient dialog all
   draw here so they don't get distorted by camera scrolling.

The intro state lives in `IntroSequence` and exposes two flags:
`PlayerVisible` (gameplay should be drawn beneath the fading-in black) and
`Finished` (intro has fully handed off). The camera snaps from fountain to
player on the frame `PlayerVisible` flips so the post-dismiss fade-in begins
already centred on Selene.

## Credits

- Code, design, animation logic — *Ishmam Ahmed*
- Tilesets and prop atlas — *EPIC RPG World Pack ([FREE Demo] Ancient Ruins)*
  by Pixel Hole Games
- Character sprites — custom

## License

Source code is released under the MIT license. Asset packs retain their
original licenses from the asset authors.
