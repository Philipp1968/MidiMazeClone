# MidiMazeClone

Hobby remake of **MidiMaze** (Hybrid Arts, Atari ST, 1987): a first-person maze shooter with
smiley opponents. Plain Windows app in C# / WPF on .NET 10. No NuGet packages, no game engine,
no 3D library: everything is drawn by hand into an `int[]` frame buffer and blitted once per frame.

Personal working style and C# code style rules live in the user-level `~\.claude\CLAUDE.md`
(simplistic, explicit C#: plain loops and `if`s, named variables, no LINQ chains, no
expression-bodied members). This file holds the project specifics. Update it after
significant work or decisions ("save game").

## Build, run, controls

```
dotnet build MidiMazeClone/MidiMazeClone.csproj
MidiMazeClone/bin/Debug/net10.0-windows/MidiMazeClone.exe
```

| Key | Action |
|---|---|
| Arrows or WASD | turn left/right, move forward/back |
| Space or Ctrl | fire |
| M | toggle mini map (also shows position/angle debug line) |
| R | new round |
| F11 | fullscreen toggle |
| Esc | quit |

Command line: `--maze <file>` (default `Mazes\classic.txt` next to the exe, then a built-in maze),
`--map` (mini map on at start), `--seed <n>` (fixed random seed, 0 = random), `--mute` (no sound).

## Architecture (one project, namespace `MidiMazeClone`)

| File | Role |
|---|---|
| `App.xaml.cs` | Entry. Parses options, opens `MainWindow`, or renders one frame to PNG in screenshot mode. |
| `MainWindow.xaml(.cs)` | WPF plumbing only: `CompositionTarget.Rendering` loop, fixed-step accumulator, keyboard into `Controls`, blit via `WriteableBitmap.WritePixels`. |
| `Game.cs` | WPF-independent game object: builds the world, `Update(dt)`, `Render(fps)` incl. HUD, mini map, lineup test scene. |
| `FrameBuffer.cs` | `int[]` pixels (0x00RRGGBB), clipped rect/line/circle/text helpers. |
| `TinyFont.cs` | 5x7 bitmap font as readable `#` rows. Upper case only. |
| `Palette.cs` | All colours in one place, meant to be tuned by eye. |
| `Maze.cs` | Grid of wall/floor cells, text parser, flood fill check. |
| `Player.cs`, `Shot.cs` | Plain data. |
| `Controls.cs` | What a player wants to do this tick. The only input into the simulation. |
| `World.cs` | The simulation: movement with wall sliding, firing, shot flight, hits, respawn, round winner. |
| `BotBrain.cs` | Computer opponent; fills `Controls` like a keyboard would. |
| `IWorldEvents.cs` | Shots and hits reported by `World` to the presentation layer (`Game`). |
| `Sounds.cs` | Sound effects synthesised in code (square waves, noise); hands them to the mixer. |
| `AudioMixer.cs` | Software mixer on the Win32 waveOut API via P/Invoke: background thread, 4 small buffers, up to 8 voices with volume. |
| `Renderer.cs` | Raycaster for walls, z-buffer, sprite pass (smileys, shots), top-down map. |
| `Mazes/*.txt` | Maze files, copied next to the exe. `classic.txt` (default): rooms, 2-3 wide corridors, pillars, loops. `tunnels.txt`: the first maze, single-tile corridors only. |

## Key decisions

- **Raycasting, not polygons.** The maze is a grid with axis-aligned walls, so one DDA ray per
  screen column gives the wall distance; slice height = focal length / perpendicular distance.
  Walls facing north-south and east-west get two shades, like the original. No depth shading.
- **Per-column z-buffer.** Sprites (smileys, shots) are drawn after the walls, column by column,
  only where closer than the wall. That gives the MidiMaze effect of a smiley half hidden behind
  a corner for free. Sprites are sorted far to near (painter's algorithm) for sprite overlap.
- **Smileys** are spheres at eye height drawn in sphere coordinates (u, v in -1..1). The face
  slides sideways with the opponent's heading and disappears when they look away.
- **WPF `WriteableBitmap`** (Bgr32, 320x200) shown in an `Image` with `Stretch=Uniform` and
  nearest-neighbour scaling. `CompositionTarget.Rendering` is the vsync-paced game loop; the
  simulation runs in fixed 60 Hz steps (`Game.StepSeconds`) via an accumulator.
- **Coordinates.** One maze cell = 1.0 world unit; cell (x, y) covers [x, x+1) x [y, y+1).
  Map Y points down. Heading angle 0 = +X (east), grows clockwise on the map. The camera's
  right vector is (-dirY, dirX). Eye height is half the wall height (walls 1.0 high).
- **Everything goes through `Controls`.** Keyboard, bots and (later) the network only set the
  five flags; `World` does the rest. Together with the seeded `Random` this keeps the
  simulation deterministic, which keeps lockstep networking open.
- **Collision.** Players are 0.3 radius, checked as a square against the grid, one axis at a
  time (slides along walls). Shots move in 0.1-cell sub-steps so they cannot tunnel.
- **Internal resolution 320x200** for the ST look. Higher resolutions are only a constant change
  (`Game.ViewWidth/ViewHeight`); the renderer scales with the focal length.
- **Sound** is generated at startup (no files) and mixed in software onto the Win32 waveOut API
  (`winmm.dll`, P/Invoke, still no libraries). Mono 16-bit at 22050 Hz, 4 buffers of 256 samples,
  about 46 ms latency. Several sounds overlap, each with its own volume. The headers and sample
  buffers the driver owns live in unmanaged memory (`Marshal.AllocHGlobal`). If the device cannot
  be opened the game simply runs silent. `World` raises `IWorldEvents`; `Game` decides what the
  local player hears: own shots and hits at full volume, others fading linearly to silence at
  `HearingRange`.

## Screenshot mode (visual checks without a window)

`--screenshot <file.png>` renders one frame headlessly and exits. Combine with:
`--seed <n>`, `--warmup <seconds>` (simulate before the shot), `--forward` / `--fire`
(hold those keys during the warmup), `--map`, `--scene lineup`.

The **lineup scene** is a fixed room with a pillar and four opponents: one facing the viewer,
one in profile, one showing its back, one half behind the pillar corner. Use it to tune the
smiley look and the palette:

```
MidiMazeClone.exe --screenshot lineup.png --scene lineup
MidiMazeClone.exe --screenshot shot.png --scene lineup --fire --warmup 0.05
MidiMazeClone.exe --screenshot bots.png --seed 5 --map --warmup 90
```

Generated `*.png` files are git-ignored.

`--audiotest <file.txt>` plays the three sounds (two of them overlapping) and writes the mixer
status to the file: open result, buffers written, active voices, write errors. With a healthy
mixer the buffer count grows by about 86 per second.

## Maze file format

Text file, `#` = wall, any other character = floor, lines starting with `;` are comments,
short lines are padded with wall, outside the map counts as wall. Keep the maze fully
connected (`Maze.CountReachableFrom` vs `CountFreeCells`), otherwise players spawn in pockets.

## Tuning constants

Movement and rules in `World` (`TurnSpeed`, `BotTurnSpeed`, `MoveSpeed`, `FireInterval`, `ShieldSeconds`,
`WinScore`), shot speed in `Shot`, field of view and smiley size in `Renderer`, bot behaviour
in `BotBrain`, colours in `Palette`.

## Status (2026-10-14)

Done, one commit per milestone:

1. Window, frame buffer, vsync loop, font, FPS counter
2. Maze file, movement with wall sliding, top-down map
3. Raycast first-person view, mini map
4. Smiley sprites with z-buffer clipping and facing direction
5. Shots, hits, respawn with shield, scoreboard, win banner, restart
6. Bot opponents (wander + engage)

Open / next ideas (decide together, none started):

- **LAN multiplayer** for up to four humans over UDP. Simulation is deterministic and
  input-driven, so lockstep (exchange `Controls` per tick) or host-authoritative are both open.
- **Polish:** respawn sound, wall edge lines or light distance fog for readability
  in long corridors, integer pixel scaling, a nicer HUD frame, crosshair question.
- **Gameplay:** player-player collision (currently they pass through each other), bots avoid
  standing in each other's line, time-limited rounds, maze selection in-game.
- **Tools:** maze editor, more mazes.
