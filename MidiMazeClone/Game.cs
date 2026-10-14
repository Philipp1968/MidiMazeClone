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

            Maze maze = LoadMaze(options.MazePath);

            int seed = options.Seed;
            if (seed == 0)
            {
                seed = Environment.TickCount;
            }

            _world = new World(maze, seed);
            _localPlayer = _world.AddPlayer("YOU", Palette.PlayerColors[0], false);
            LocalControls = _localPlayer.Controls;

            _renderer = new Renderer(ViewWidth, ViewHeight);
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

        public void Render(int fps)
        {
            Screen.Clear(Palette.Black);

            // Milestone 2: the big top-down map is the main view.
            Maze maze = _world.Maze;
            int cellSize = Math.Min(ViewWidth / maze.Width, (ViewHeight - 20) / maze.Height);
            if (cellSize < 1)
            {
                cellSize = 1;
            }
            int mapX = (ViewWidth - maze.Width * cellSize) / 2;
            int mapY = 12;
            _renderer.DrawMap(_world, Screen, mapX, mapY, cellSize);

            string position = "X " + _localPlayer.X.ToString("0.00", CultureInfo.InvariantCulture)
                + "  Y " + _localPlayer.Y.ToString("0.00", CultureInfo.InvariantCulture)
                + "  ANGLE " + (_localPlayer.Angle * 180.0 / Math.PI).ToString("0", CultureInfo.InvariantCulture);
            Screen.DrawTextShadowed(2, 2, position, Palette.White, 1);

            string hint = "ARROWS/WASD MOVE   ESC QUIT";
            Screen.DrawTextShadowed(2, ViewHeight - 9, hint, Palette.Grey, 1);

            string fpsText = fps + " FPS";
            Screen.DrawTextShadowed(ViewWidth - FrameBuffer.TextWidth(fpsText, 1) - 2, 2, fpsText, Palette.White, 1);
        }
    }
}
