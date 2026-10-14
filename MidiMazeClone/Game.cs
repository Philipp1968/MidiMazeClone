using System.IO;
using System.Globalization;

namespace MidiMazeClone
{
    /// <summary>
    /// The game itself, independent of WPF: owns the frame buffer and the simulation,
    /// advances the simulation in fixed steps and renders frames into the buffer.
    /// </summary>
    public sealed class Game
    {
        public const int ViewWidth = 320;
        public const int ViewHeight = 200;
        public const double StepSeconds = 1.0 / 60.0;

        public readonly FrameBuffer Screen;
        public readonly Controls LocalControls;
        public bool ShowMap;

        private readonly World _world;
        private readonly Player _localPlayer;
        private readonly Renderer _renderer;

        public Game(StartupOptions options)
        {
            Screen = new FrameBuffer(ViewWidth, ViewHeight);
            ShowMap = options.ShowMap;

            int seed = options.Seed;
            if (seed == 0)
            {
                seed = Environment.TickCount;
            }

            if (options.Scene == "lineup")
            {
                _world = CreateLineupScene(seed);
                _localPlayer = _world.Players[0];
            }
            else
            {
                Maze maze = LoadMaze(options.MazePath);
                _world = new World(maze, seed);
                _localPlayer = _world.AddPlayer("YOU", Palette.PlayerColors[0], false);
                _world.AddPlayer("BOT 1", Palette.PlayerColors[1], true);
                _world.AddPlayer("BOT 2", Palette.PlayerColors[2], true);
                _world.AddPlayer("BOT 3", Palette.PlayerColors[3], true);
            }

            LocalControls = _localPlayer.Controls;
            _renderer = new Renderer(ViewWidth, ViewHeight);
        }

        /// <summary>
        /// Fixed test scene for tuning the smiley rendering: a room with a pillar, the viewer
        /// looking east, one opponent facing us, one in profile, one showing their back,
        /// and one half hidden behind the pillar corner.
        /// </summary>
        private static World CreateLineupScene(int seed)
        {
            string[] lines =
            {
                "############",
                "#..........#",
                "#..........#",
                "#....##....#",
                "#....##....#",
                "#..........#",
                "#..........#",
                "############",
            };
            Maze maze = Maze.Parse(lines);
            World world = new World(maze, seed);

            Player viewer = world.AddPlayer("YOU", Palette.PlayerColors[0], false);
            PlacePlayer(viewer, 2.5, 4.0, 0.0);

            Player facing = world.AddPlayer("FACE", Palette.PlayerColors[1], false);
            PlacePlayer(facing, 4.2, 4.0, Math.PI);

            Player profile = world.AddPlayer("SIDE", Palette.PlayerColors[2], false);
            PlacePlayer(profile, 5.5, 2.2, MathUtil.HalfPi);

            Player back = world.AddPlayer("BACK", Palette.PlayerColors[3], false);
            PlacePlayer(back, 5.5, 5.8, 0.0);

            Player corner = world.AddPlayer("CORNER", Palette.PlayerColors[1], false);
            PlacePlayer(corner, 7.5, 2.0, Math.PI);

            return world;
        }

        private static void PlacePlayer(Player player, double x, double y, double angle)
        {
            player.X = x;
            player.Y = y;
            player.Angle = angle;
            player.Shield = 0.0;
        }

        /// <summary>
        /// Maze lookup order: --maze path, then Mazes\classic.txt next to the exe, then the built-in one.
        /// </summary>
        private static Maze LoadMaze(string? requestedPath)
        {
            if (requestedPath != null)
            {
                return Maze.LoadFromFile(requestedPath);
            }

            string defaultPath = Path.Combine(AppContext.BaseDirectory, "Mazes", "classic.txt");
            if (File.Exists(defaultPath))
            {
                return Maze.LoadFromFile(defaultPath);
            }

            return Maze.CreateBuiltIn();
        }

        public void Update(double dt)
        {
            _world.Update(dt);
        }

        public void Restart()
        {
            _world.Restart();
        }

        public void Render(int fps)
        {
            _renderer.DrawView(_world, _localPlayer, Screen);

            if (_localPlayer.HitFlash > 0.0)
            {
                DrawHitFlash();
            }

            if (ShowMap)
            {
                DrawMiniMap();

                string position = "X " + _localPlayer.X.ToString("0.00", CultureInfo.InvariantCulture)
                    + "  Y " + _localPlayer.Y.ToString("0.00", CultureInfo.InvariantCulture)
                    + "  ANGLE " + (_localPlayer.Angle * 180.0 / Math.PI).ToString("0", CultureInfo.InvariantCulture);
                Screen.DrawTextShadowed(2, ViewHeight - 18, position, Palette.Grey, 1);
            }

            DrawScoreboard();

            if (_localPlayer.Shield > 0.0)
            {
                int blinkPhase = (int)(_world.Time * 4.0) % 2;
                if (blinkPhase == 0)
                {
                    string shieldText = "SHIELD";
                    int shieldX = (ViewWidth - FrameBuffer.TextWidth(shieldText, 1)) / 2;
                    Screen.DrawTextShadowed(shieldX, ViewHeight - 30, shieldText, Palette.White, 1);
                }
            }

            if (_world.Winner != null)
            {
                DrawWinnerBanner(_world.Winner);
            }

            string hint = "ARROWS/WASD MOVE  SPACE FIRE  M MAP  R RESTART  ESC QUIT";
            Screen.DrawTextShadowed(2, ViewHeight - 9, hint, Palette.Grey, 1);

            string fpsText = fps + " FPS";
            Screen.DrawTextShadowed(ViewWidth - FrameBuffer.TextWidth(fpsText, 1) - 2, 2, fpsText, Palette.White, 1);
        }

        /// <summary>One line per player in the top left: name and hits dealt.</summary>
        private void DrawScoreboard()
        {
            int y = 2;
            for (int i = 0; i < _world.Players.Count; i++)
            {
                Player player = _world.Players[i];
                string line = player.Name + " " + player.Score;
                Screen.DrawTextShadowed(2, y, line, player.Color, 1);
                y += 9;
            }
        }

        /// <summary>Tint the whole view red, fading out as the flash timer runs down.</summary>
        private void DrawHitFlash()
        {
            double amount = _localPlayer.HitFlash / World.HitFlashSeconds * 0.6;
            int[] pixels = Screen.Pixels;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Palette.Blend(pixels[i], Palette.Red, amount);
            }
        }

        private void DrawWinnerBanner(Player winner)
        {
            string line1 = winner.Name + " WINS!";
            string line2 = "PRESS R FOR A NEW ROUND";

            int boxWidth = FrameBuffer.TextWidth(line2, 1) + 16;
            int boxHeight = 40;
            int boxX = (ViewWidth - boxWidth) / 2;
            int boxY = (ViewHeight - boxHeight) / 2;
            Screen.FillRect(boxX, boxY, boxWidth, boxHeight, Palette.Black);
            Screen.FillRect(boxX + 1, boxY + 1, boxWidth - 2, boxHeight - 2, Palette.DarkGrey);

            int line1X = (ViewWidth - FrameBuffer.TextWidth(line1, 2)) / 2;
            Screen.DrawTextShadowed(line1X, boxY + 6, line1, winner.Color, 2);

            int line2X = (ViewWidth - FrameBuffer.TextWidth(line2, 1)) / 2;
            Screen.DrawTextShadowed(line2X, boxY + 26, line2, Palette.White, 1);
        }

        /// <summary>Small map in the top right corner, below the FPS counter.</summary>
        private void DrawMiniMap()
        {
            const int cellSize = 3;
            Maze maze = _world.Maze;

            int mapWidth = maze.Width * cellSize;
            int mapHeight = maze.Height * cellSize;
            int mapX = ViewWidth - mapWidth - 4;
            int mapY = 12;

            Screen.FillRect(mapX - 1, mapY - 1, mapWidth + 2, mapHeight + 2, Palette.MapBorder);
            _renderer.DrawMap(_world, Screen, mapX, mapY, cellSize);
        }
    }
}
