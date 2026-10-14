namespace MidiMazeClone
{
    /// <summary>
    /// Draws the world into a FrameBuffer. Milestone 2: top-down map only.
    /// </summary>
    public sealed class Renderer
    {
        private readonly int _width;
        private readonly int _height;

        public Renderer(int width, int height)
        {
            _width = width;
            _height = height;
        }

        /// <summary>
        /// Top-down map with the given pixel size per cell, drawn at (originX, originY).
        /// Used large as the debug view and small as the mini map.
        /// </summary>
        public void DrawMap(World world, FrameBuffer fb, int originX, int originY, int cellSize)
        {
            Maze maze = world.Maze;

            for (int y = 0; y < maze.Height; y++)
            {
                for (int x = 0; x < maze.Width; x++)
                {
                    int color = Palette.MapFloor;
                    if (maze.IsWall(x, y))
                    {
                        color = Palette.MapWall;
                    }
                    fb.FillRect(originX + x * cellSize, originY + y * cellSize, cellSize, cellSize, color);
                }
            }

            for (int i = 0; i < world.Players.Count; i++)
            {
                Player player = world.Players[i];

                int px = originX + (int)(player.X * cellSize);
                int py = originY + (int)(player.Y * cellSize);
                int radius = (int)(Player.Radius * cellSize);
                if (radius < 1)
                {
                    radius = 1;
                }
                fb.FillCircle(px, py, radius, player.Color);

                int headingX = originX + (int)((player.X + player.DirectionX() * 0.7) * cellSize);
                int headingY = originY + (int)((player.Y + player.DirectionY() * 0.7) * cellSize);
                fb.DrawLine(px, py, headingX, headingY, Palette.White);
            }
        }
    }
}
